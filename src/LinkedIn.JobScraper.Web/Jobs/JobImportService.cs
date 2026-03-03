using System.Diagnostics;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobImportService : IJobImportService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly ILinkedInJobSearchService _linkedInJobSearchService;
    private readonly ILogger<JobImportService> _logger;

    public JobImportService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ILinkedInJobSearchService linkedInJobSearchService,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        ILogger<JobImportService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _linkedInJobSearchService = linkedInJobSearchService;
        _fetchDiagnosticsOptions = fetchDiagnosticsOptions.Value;
        _logger = logger;
    }

    public async Task<JobImportResult> ImportCurrentSearchAsync(
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null)
    {
        var diagnosticsEnabled = _fetchDiagnosticsOptions.Enabled;
        var canLogDiagnostics = diagnosticsEnabled && _logger.IsEnabled(LogLevel.Information);
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

        if (canLogDiagnostics)
        {
            Log.JobImportDiagnosticsStarted(
                _logger,
                searchResult.Jobs.Count,
                jobIds.Length,
                existingJobs.Count);
        }

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

                    if (canLogDiagnostics)
                    {
                        var sanitizedTitle = SensitiveDataRedaction.SanitizeForMessage(job.Title, 256);
                        Log.JobImportDiagnosticsReconciledExistingJob(
                            _logger,
                            processedCount + 1,
                            job.LinkedInJobId,
                            sanitizedTitle);
                    }
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

                    if (canLogDiagnostics)
                    {
                        var sanitizedTitle = SensitiveDataRedaction.SanitizeForMessage(job.Title, 256);
                        Log.JobImportDiagnosticsReconciledNewJob(
                            _logger,
                            processedCount + 1,
                            job.LinkedInJobId,
                            sanitizedTitle);
                    }
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

            if (canLogDiagnostics)
            {
                Log.JobImportDiagnosticsPersistingReconciliationResults(
                    _logger,
                    processedCount,
                    importedCount,
                    updatedExistingCount,
                    insertedRecords.Count);
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

            var saveStopwatch = Stopwatch.StartNew();
            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
            saveStopwatch.Stop();

            if (canLogDiagnostics)
            {
                Log.JobImportDiagnosticsCompletedPersistence(
                    _logger,
                    processedCount,
                    importedCount,
                    updatedExistingCount,
                    skippedCount,
                    saveStopwatch.ElapsedMilliseconds);
            }
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

internal static partial class Log
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Job import diagnostics started. SearchReturnedCount={SearchReturnedCount}, DistinctLinkedInJobIds={DistinctLinkedInJobIds}, ExistingMatchCount={ExistingMatchCount}")]
    public static partial void JobImportDiagnosticsStarted(
        ILogger logger,
        int searchReturnedCount,
        int distinctLinkedInJobIds,
        int existingMatchCount);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Information,
        Message = "Job import diagnostics reconciled existing job. Sequence={Sequence}, LinkedInJobId={LinkedInJobId}, Title={Title}, Action=Refreshed")]
    public static partial void JobImportDiagnosticsReconciledExistingJob(
        ILogger logger,
        int sequence,
        string linkedInJobId,
        string title);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "Job import diagnostics reconciled new job. Sequence={Sequence}, LinkedInJobId={LinkedInJobId}, Title={Title}, Action=Inserted")]
    public static partial void JobImportDiagnosticsReconciledNewJob(
        ILogger logger,
        int sequence,
        string linkedInJobId,
        string title);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Information,
        Message = "Job import diagnostics persisting reconciliation results. ProcessedCount={ProcessedCount}, ImportedCount={ImportedCount}, RefreshedCount={RefreshedCount}, InsertBatchCount={InsertBatchCount}")]
    public static partial void JobImportDiagnosticsPersistingReconciliationResults(
        ILogger logger,
        int processedCount,
        int importedCount,
        int refreshedCount,
        int insertBatchCount);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Information,
        Message = "Job import diagnostics completed persistence. ProcessedCount={ProcessedCount}, ImportedCount={ImportedCount}, RefreshedCount={RefreshedCount}, SkippedCount={SkippedCount}, SaveElapsedMilliseconds={SaveElapsedMilliseconds}")]
    public static partial void JobImportDiagnosticsCompletedPersistence(
        ILogger logger,
        int processedCount,
        int importedCount,
        int refreshedCount,
        int skippedCount,
        long saveElapsedMilliseconds);
}
