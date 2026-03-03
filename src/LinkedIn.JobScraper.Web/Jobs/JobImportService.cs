using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobImportService : IJobImportService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly ILinkedInJobSearchService _linkedInJobSearchService;

    public JobImportService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ILinkedInJobSearchService linkedInJobSearchService)
    {
        _dbContextFactory = dbContextFactory;
        _linkedInJobSearchService = linkedInJobSearchService;
    }

    public async Task<JobImportResult> ImportCurrentSearchAsync(CancellationToken cancellationToken)
    {
        var searchResult = await _linkedInJobSearchService.FetchCurrentSearchAsync(cancellationToken);

        if (!searchResult.Success)
        {
            return JobImportResult.Failed(searchResult.Message, searchResult.StatusCode);
        }

        if (searchResult.Jobs.Count == 0)
        {
            return JobImportResult.Succeeded(
                searchResult.PagesFetched,
                searchResult.ReturnedCount,
                searchResult.TotalCount,
                0,
                0,
                0,
                searchResult.Message);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var seenAtUtc = DateTimeOffset.UtcNow;
        var jobIds = searchResult.Jobs
            .Select(static job => job.LinkedInJobId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingJobs = await dbContext.Jobs
            .Where(job => jobIds.Contains(job.LinkedInJobId))
            .ToListAsync(cancellationToken);

        var existingById = existingJobs.ToDictionary(
            static job => job.LinkedInJobId,
            StringComparer.Ordinal);

        var importedCount = 0;
        var updatedExistingCount = 0;
        var skippedCount = 0;

        foreach (var job in searchResult.Jobs)
        {
            if (existingById.TryGetValue(job.LinkedInJobId, out var existing))
            {
                existing.LastSeenAtUtc = seenAtUtc;

                if (string.IsNullOrWhiteSpace(existing.LocationName) &&
                    !string.IsNullOrWhiteSpace(job.LocationName))
                {
                    existing.LocationName = job.LocationName;
                }

                if (string.IsNullOrWhiteSpace(existing.CompanyName) &&
                    !string.IsNullOrWhiteSpace(job.CompanyName))
                {
                    existing.CompanyName = job.CompanyName;
                }

                updatedExistingCount++;
                skippedCount++;
                continue;
            }

            var record = new JobRecord
            {
                LinkedInJobId = job.LinkedInJobId,
                LinkedInJobPostingUrn = job.LinkedInJobPostingUrn,
                LinkedInJobCardUrn = job.LinkedInJobCardUrn,
                Title = job.Title,
                CompanyName = job.CompanyName,
                LocationName = job.LocationName,
                ListedAtUtc = job.ListedAtUtc,
                FirstDiscoveredAtUtc = seenAtUtc,
                LastSeenAtUtc = seenAtUtc,
                CurrentStatus = JobWorkflowStatus.New
            };

            dbContext.Jobs.Add(record);
            existingById[job.LinkedInJobId] = record;
            importedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return JobImportResult.Succeeded(
            searchResult.PagesFetched,
            searchResult.ReturnedCount,
            searchResult.TotalCount,
            importedCount,
            updatedExistingCount,
            skippedCount,
            searchResult.Message);
    }
}
