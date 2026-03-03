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

    public async Task<JobImportResult> ImportCurrentSearchAsync(
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null)
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
        var insertedRecords = new List<JobRecord>(searchResult.Jobs.Count);

        var importedCount = 0;
        var updatedExistingCount = 0;
        var skippedCount = 0;
        var processedCount = 0;
        var originalAutoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"LinkedIn returned {searchResult.Jobs.Count} jobs. Reconciling them with the local store...",
                    searchResult.Jobs.Count,
                    0,
                    0,
                    0),
                cancellationToken);
        }

        try
        {
            foreach (var job in searchResult.Jobs)
            {
                string progressMessage;

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
                    progressMessage = $"Reconciled {processedCount + 1} of {searchResult.Jobs.Count}: refreshed '{job.Title}'.";
                }
                else
                {
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

                    insertedRecords.Add(record);
                    existingById[job.LinkedInJobId] = record;
                    importedCount++;
                    progressMessage = $"Reconciled {processedCount + 1} of {searchResult.Jobs.Count}: added '{job.Title}'.";
                }

                processedCount++;

                if (progressCallback is not null)
                {
                    await progressCallback(
                        new JobStageProgress(
                            progressMessage,
                            searchResult.Jobs.Count,
                            processedCount,
                            importedCount + updatedExistingCount,
                            0),
                        cancellationToken);
                }
            }

            if (insertedRecords.Count > 0)
            {
                dbContext.Jobs.AddRange(insertedRecords);
            }

            if (progressCallback is not null)
            {
                await progressCallback(
                    new JobStageProgress(
                        $"Persisting {importedCount + updatedExistingCount} reconciled records to the local database...",
                        searchResult.Jobs.Count,
                        processedCount,
                        importedCount + updatedExistingCount,
                        0),
                    cancellationToken);
            }

            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
        }

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
