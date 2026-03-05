using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobImportServiceTests
{
    [Fact]
    public async Task ImportCurrentSearchAsyncStopsAfterKnownStreakWhenThresholdReached()
    {
        var options = CreateDatabaseOptions();
        await SeedJobsAsync(options, ["1", "2", "3", "4", "5", "6", "7", "8"]);

        var searchService = new FakePagedLinkedInJobSearchService(
            CreatePage(["1", "2", "3"]),
            CreatePage(["4", "5", "6"]),
            CreatePage(["7", "8", "9"]));
        var service = CreateService(
            options,
            searchService,
            new LinkedInIncrementalFetchOptions
            {
                Enabled = true,
                OverlapPageCount = 1,
                MinimumPagesBeforeStop = 1,
                KnownStreakThreshold = 3
            });

        var result = await service.ImportCurrentSearchAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(6, result.FetchedCount);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(6, result.UpdatedExistingCount);
        Assert.Equal(6, result.SkippedCount);
        Assert.Equal(2, searchService.RequestedPages);
    }

    [Fact]
    public async Task ImportCurrentSearchAsyncDoesNotStopWhenNewJobBreaksKnownStreak()
    {
        var options = CreateDatabaseOptions();
        await SeedJobsAsync(options, ["1", "2", "3", "4", "5", "6", "7", "8"]);

        var searchService = new FakePagedLinkedInJobSearchService(
            CreatePage(["1", "2", "3"]),
            CreatePage(["4", "9000", "5"]),
            CreatePage(["6", "7", "8"]));
        var service = CreateService(
            options,
            searchService,
            new LinkedInIncrementalFetchOptions
            {
                Enabled = true,
                OverlapPageCount = 1,
                MinimumPagesBeforeStop = 1,
                KnownStreakThreshold = 3
            });

        var result = await service.ImportCurrentSearchAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.PagesFetched);
        Assert.Equal(9, result.FetchedCount);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(8, result.UpdatedExistingCount);
        Assert.Equal(8, result.SkippedCount);
        Assert.Equal(3, searchService.RequestedPages);
    }

    private static JobImportService CreateService(
        DbContextOptions<LinkedInJobScraperDbContext> dbContextOptions,
        ILinkedInJobSearchService linkedInJobSearchService,
        LinkedInIncrementalFetchOptions incrementalFetchOptions)
    {
        return new JobImportService(
            new TestDbContextFactory(dbContextOptions),
            linkedInJobSearchService,
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(incrementalFetchOptions),
            NullLogger<JobImportService>.Instance);
    }

    private static DbContextOptions<LinkedInJobScraperDbContext> CreateDatabaseOptions()
    {
        return new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    private static async Task SeedJobsAsync(
        DbContextOptions<LinkedInJobScraperDbContext> dbContextOptions,
        IReadOnlyList<string> linkedInJobIds)
    {
        await using var dbContext = new LinkedInJobScraperDbContext(dbContextOptions);

        foreach (var linkedInJobId in linkedInJobIds)
        {
            dbContext.Jobs.Add(
                new JobRecord
                {
                    LinkedInJobId = linkedInJobId,
                    LinkedInJobPostingUrn = $"urn:li:fsd_jobPosting:{linkedInJobId}",
                    LinkedInJobCardUrn = $"urn:li:fsd_jobCard:{linkedInJobId}",
                    Title = $"Job {linkedInJobId}",
                    CompanyName = "Acme",
                    LocationName = "Limassol",
                    FirstDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
                    LastSeenAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });
        }

        await dbContext.SaveChangesAsync();
    }

    private static LinkedInJobSearchItem[] CreatePage(IReadOnlyList<string> linkedInJobIds)
    {
        return linkedInJobIds
            .Select(
                id => new LinkedInJobSearchItem(
                    id,
                    $"urn:li:fsd_jobPosting:{id}",
                    $"urn:li:fsd_jobCard:{id}",
                    $"Job {id}",
                    "Acme",
                    "Limassol",
                    DateTimeOffset.UtcNow.AddDays(-7)))
            .ToArray();
    }

    private sealed class FakePagedLinkedInJobSearchService : ILinkedInJobSearchService
    {
        private readonly LinkedInJobSearchItem[][] _pages;

        public FakePagedLinkedInJobSearchService(params LinkedInJobSearchItem[][] pages)
        {
            _pages = pages;
        }

        public int RequestedPages { get; private set; }

        public Task<LinkedInJobSearchFetchResult> FetchCurrentSearchAsync(
            CancellationToken cancellationToken,
            LinkedInJobSearchFetchRequest? request = null)
        {
            RequestedPages = 0;

            var aggregated = new List<LinkedInJobSearchItem>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var totalAvailable = _pages.Sum(static page => page.Length);

            for (var pageIndex = 0; pageIndex < _pages.Length; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = _pages[pageIndex];
                var uniqueJobsAdded = new List<LinkedInJobSearchItem>(page.Length);

                foreach (var job in page)
                {
                    if (!seenIds.Add(job.LinkedInJobId))
                    {
                        continue;
                    }

                    aggregated.Add(job);
                    uniqueJobsAdded.Add(job);
                }

                RequestedPages++;

                if (request?.ShouldStopAfterPage is null)
                {
                    continue;
                }

                var context = new LinkedInJobSearchPageContext(
                    pageIndex,
                    RequestedPages,
                    page.Length,
                    page.Length,
                    totalAvailable,
                    aggregated.Count,
                    uniqueJobsAdded);

                if (request.ShouldStopAfterPage(context))
                {
                    break;
                }
            }

            return Task.FromResult(
                LinkedInJobSearchFetchResult.Succeeded(
                    StatusCodes.Status200OK,
                    RequestedPages,
                    aggregated.Count,
                    totalAvailable,
                    aggregated,
                    request?.EarlyStopMessage ?? "LinkedIn search fetch succeeded."));
        }
    }
}
