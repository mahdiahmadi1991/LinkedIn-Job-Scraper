using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Details;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Authentication;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobEnrichmentServiceTests
{
    [Fact]
    public async Task EnrichIncompleteJobsAsyncPrioritizesIncompleteAndBackfillsWithStaleCompleteJobs()
    {
        var now = DateTimeOffset.UtcNow;
        var options = CreateDatabaseOptions();

        Guid incompleteJobId;
        Guid staleCompleteJobId;
        Guid freshCompleteJobId;

        await using (var dbContext = new LinkedInJobScraperDbContext(options))
        {
            var incompleteJob = CreateJob(
                linkedInJobId: "incomplete-job",
                lastSeenAtUtc: now.AddMinutes(-5),
                description: null,
                companyApplyUrl: null,
                employmentStatus: null,
                lastDetailSyncedAtUtc: null);
            var staleCompleteJob = CreateJob(
                linkedInJobId: "stale-complete-job",
                lastSeenAtUtc: now.AddHours(-2),
                description: "Has description",
                companyApplyUrl: "https://example.com/apply-stale",
                employmentStatus: "Full-time",
                lastDetailSyncedAtUtc: now.AddHours(-72));
            var freshCompleteJob = CreateJob(
                linkedInJobId: "fresh-complete-job",
                lastSeenAtUtc: now.AddHours(-1),
                description: "Has description",
                companyApplyUrl: "https://example.com/apply-fresh",
                employmentStatus: "Full-time",
                lastDetailSyncedAtUtc: now.AddHours(-2));

            dbContext.Jobs.AddRange(incompleteJob, staleCompleteJob, freshCompleteJob);
            await dbContext.SaveChangesAsync();

            incompleteJobId = incompleteJob.Id;
            staleCompleteJobId = staleCompleteJob.Id;
            freshCompleteJobId = freshCompleteJob.Id;
        }

        var detailService = new RecordingLinkedInJobDetailService();
        var service = CreateService(options, detailService, detailResyncAfterHours: 24);

        var result = await service.EnrichIncompleteJobsAsync(2, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.AttemptedJobIds.Count);
        Assert.Equal(2, detailService.RequestedLinkedInJobIds.Count);
        Assert.Equal("incomplete-job", detailService.RequestedLinkedInJobIds[0]);
        Assert.Equal("stale-complete-job", detailService.RequestedLinkedInJobIds[1]);
        Assert.Contains(incompleteJobId, result.AttemptedJobIds);
        Assert.Contains(staleCompleteJobId, result.AttemptedJobIds);
        Assert.DoesNotContain(freshCompleteJobId, result.AttemptedJobIds);
    }

    [Fact]
    public async Task EnrichIncompleteJobsAsyncHonorsExcludedIdsForStaleRefreshCandidates()
    {
        var now = DateTimeOffset.UtcNow;
        var options = CreateDatabaseOptions();

        Guid staleJobIdA;
        Guid staleJobIdB;

        await using (var dbContext = new LinkedInJobScraperDbContext(options))
        {
            var staleJobA = CreateJob(
                linkedInJobId: "stale-a",
                lastSeenAtUtc: now.AddHours(-4),
                description: "Desc A",
                companyApplyUrl: "https://example.com/a",
                employmentStatus: "Full-time",
                lastDetailSyncedAtUtc: now.AddHours(-48));
            var staleJobB = CreateJob(
                linkedInJobId: "stale-b",
                lastSeenAtUtc: now.AddHours(-3),
                description: "Desc B",
                companyApplyUrl: "https://example.com/b",
                employmentStatus: "Full-time",
                lastDetailSyncedAtUtc: now.AddHours(-49));

            dbContext.Jobs.AddRange(staleJobA, staleJobB);
            await dbContext.SaveChangesAsync();

            staleJobIdA = staleJobA.Id;
            staleJobIdB = staleJobB.Id;
        }

        var detailService = new RecordingLinkedInJobDetailService();
        var service = CreateService(options, detailService, detailResyncAfterHours: 24);

        var result = await service.EnrichIncompleteJobsAsync(
            2,
            CancellationToken.None,
            excludedJobIds: new HashSet<Guid> { staleJobIdA });

        Assert.True(result.Success);
        Assert.Single(result.AttemptedJobIds);
        Assert.DoesNotContain(staleJobIdA, result.AttemptedJobIds);
        Assert.Contains(staleJobIdB, result.AttemptedJobIds);
        Assert.Single(detailService.RequestedLinkedInJobIds);
        Assert.Equal("stale-b", detailService.RequestedLinkedInJobIds[0]);
    }

    private static DbContextOptions<LinkedInJobScraperDbContext> CreateDatabaseOptions()
    {
        return new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    private static JobRecord CreateJob(
        string linkedInJobId,
        DateTimeOffset lastSeenAtUtc,
        string? description,
        string? companyApplyUrl,
        string? employmentStatus,
        DateTimeOffset? lastDetailSyncedAtUtc)
    {
        return new JobRecord
        {
            AppUserId = 1,
            LinkedInJobId = linkedInJobId,
            LinkedInJobPostingUrn = $"urn:li:fsd_jobPosting:{linkedInJobId}",
            LinkedInJobCardUrn = $"urn:li:fsd_jobCard:{linkedInJobId}",
            Title = linkedInJobId,
            CompanyName = "Acme",
            LocationName = "Limassol",
            Description = description,
            CompanyApplyUrl = companyApplyUrl,
            EmploymentStatus = employmentStatus,
            FirstDiscoveredAtUtc = lastSeenAtUtc.AddDays(-1),
            LastSeenAtUtc = lastSeenAtUtc,
            LastDetailSyncedAtUtc = lastDetailSyncedAtUtc
        };
    }

    private static JobEnrichmentService CreateService(
        DbContextOptions<LinkedInJobScraperDbContext> dbContextOptions,
        ILinkedInJobDetailService linkedInJobDetailService,
        int detailResyncAfterHours)
    {
        return new JobEnrichmentService(
            new TestCurrentAppUserContext(),
            new TestDbContextFactory(dbContextOptions),
            linkedInJobDetailService,
            Options.Create(
                new JobsWorkflowOptions
                {
                    DetailResyncAfterHours = detailResyncAfterHours
                }),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            NullLogger<JobEnrichmentService>.Instance);
    }

    private sealed class RecordingLinkedInJobDetailService : ILinkedInJobDetailService
    {
        public List<string> RequestedLinkedInJobIds { get; } = [];

        public Task<LinkedInJobDetailFetchResult> FetchAsync(
            string linkedInJobId,
            CancellationToken cancellationToken)
        {
            RequestedLinkedInJobIds.Add(linkedInJobId);

            var detail = new LinkedInJobDetailData(
                linkedInJobId,
                $"urn:li:fsd_jobPosting:{linkedInJobId}",
                $"Title {linkedInJobId}",
                "Acme",
                "Limassol",
                "Full-time",
                $"Description {linkedInJobId}",
                $"https://example.com/apply/{linkedInJobId}",
                DateTimeOffset.UtcNow.AddDays(-3),
                DateTimeOffset.UtcNow.AddHours(-1));

            return Task.FromResult(
                LinkedInJobDetailFetchResult.Succeeded(
                    StatusCodes.Status200OK,
                    detail,
                    warnings: Array.Empty<string>()));
        }
    }
}
