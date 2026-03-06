using System.Diagnostics;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobImportService : IJobImportService
{
    private const int ImportCheckpointSize = 100;

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly LinkedInIncrementalFetchOptions _incrementalFetchOptions;
    private readonly ILinkedInJobSearchService _linkedInJobSearchService;
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly ILogger<JobImportService> _logger;

    public JobImportService(
        ICurrentAppUserContext currentAppUserContext,
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ILinkedInJobSearchService linkedInJobSearchService,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        IOptions<LinkedInIncrementalFetchOptions> incrementalFetchOptions,
        ILogger<JobImportService> logger)
    {
        _currentAppUserContext = currentAppUserContext;
        _dbContextFactory = dbContextFactory;
        _linkedInJobSearchService = linkedInJobSearchService;
        _fetchDiagnosticsOptions = fetchDiagnosticsOptions.Value;
        _incrementalFetchOptions = incrementalFetchOptions.Value;
        _logger = logger;
    }

    public async Task<JobImportResult> ImportCurrentSearchAsync(
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null)
    {
        var diagnosticsEnabled = _fetchDiagnosticsOptions.Enabled;
        var canLogDiagnostics = diagnosticsEnabled && _logger.IsEnabled(LogLevel.Information);
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var incrementalState = await BuildIncrementalStopStateAsync(dbContext, userId, cancellationToken);
        if (canLogDiagnostics)
        {
            if (incrementalState is null)
            {
                Log.JobImportDiagnosticsIncrementalPolicyDisabled(_logger);
            }
            else
            {
                Log.JobImportDiagnosticsIncrementalPolicyConfigured(
                    _logger,
                    incrementalState.KnownJobCount,
                    _incrementalFetchOptions.MinimumPagesBeforeStop,
                    _incrementalFetchOptions.OverlapPageCount,
                    _incrementalFetchOptions.KnownStreakThreshold);
            }
        }

        var searchFetchRequest = incrementalState is null
            ? null
            : new LinkedInJobSearchFetchRequest(
                stopContext =>
                {
                    var decision = incrementalState.EvaluatePage(stopContext);

                    if (canLogDiagnostics)
                    {
                        Log.JobImportDiagnosticsIncrementalPolicyEvaluatedPage(
                            _logger,
                            stopContext.PageIndex,
                            stopContext.PagesFetched,
                            decision.KnownOnPage,
                            decision.NewOnPage,
                            decision.ConsecutiveKnownJobs,
                            decision.ShouldStop,
                            decision.Reason);
                    }

                    return decision.ShouldStop;
                },
                "LinkedIn job search stopped early after reaching the incremental known-job streak threshold.");
        var searchResult = await _linkedInJobSearchService.FetchCurrentSearchAsync(cancellationToken, searchFetchRequest);

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

        var seenAtUtc = DateTimeOffset.UtcNow;
        var jobIds = searchResult.Jobs
            .Select(static job => job.LinkedInJobId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingJobs = await dbContext.Jobs
            .Where(job => job.AppUserId == userId && jobIds.Contains(job.LinkedInJobId))
            .ToListAsync(cancellationToken);

        var existingById = existingJobs.ToDictionary(
            static job => job.LinkedInJobId,
            StringComparer.Ordinal);

        var importedCount = 0;
        var updatedExistingCount = 0;
        var skippedCount = 0;
        var processedCount = 0;
        var pendingPersistenceCount = 0;
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
                        AppUserId = userId,
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
                pendingPersistenceCount++;

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

                if (pendingPersistenceCount < ImportCheckpointSize)
                {
                    continue;
                }

                await PersistCheckpointAsync(
                    dbContext,
                    canLogDiagnostics,
                    progressCallback,
                    searchResult.Jobs.Count,
                    processedCount,
                    importedCount,
                    updatedExistingCount,
                    skippedCount,
                    pendingPersistenceCount,
                    cancellationToken);

                pendingPersistenceCount = 0;
            }

            if (pendingPersistenceCount > 0)
            {
                await PersistCheckpointAsync(
                    dbContext,
                    canLogDiagnostics,
                    progressCallback,
                    searchResult.Jobs.Count,
                    processedCount,
                    importedCount,
                    updatedExistingCount,
                    skippedCount,
                    pendingPersistenceCount,
                    cancellationToken);
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

    private async Task<IncrementalStopState?> BuildIncrementalStopStateAsync(
        LinkedInJobScraperDbContext dbContext,
        int userId,
        CancellationToken cancellationToken)
    {
        if (!_incrementalFetchOptions.Enabled)
        {
            return null;
        }

        var knownIds = await dbContext.Jobs
            .AsNoTracking()
            .Where(job => job.AppUserId == userId)
            .Select(static job => job.LinkedInJobId)
            .ToListAsync(cancellationToken);

        return new IncrementalStopState(_incrementalFetchOptions, knownIds);
    }

    private async Task PersistCheckpointAsync(
        LinkedInJobScraperDbContext dbContext,
        bool canLogDiagnostics,
        JobStageProgressCallback? progressCallback,
        int totalJobs,
        int processedCount,
        int importedCount,
        int updatedExistingCount,
        int skippedCount,
        int checkpointCount,
        CancellationToken cancellationToken)
    {
        if (canLogDiagnostics)
        {
            Log.JobImportDiagnosticsPersistingCheckpoint(
                _logger,
                processedCount,
                importedCount,
                updatedExistingCount,
                skippedCount,
                checkpointCount);
        }

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Saving a local checkpoint after {processedCount} reconciled job(s)...",
                    totalJobs,
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
            Log.JobImportDiagnosticsCompletedCheckpoint(
                _logger,
                processedCount,
                importedCount,
                updatedExistingCount,
                skippedCount,
                checkpointCount,
                saveStopwatch.ElapsedMilliseconds);
        }
    }

    private sealed class IncrementalStopState
    {
        private readonly HashSet<string> _knownJobIds;
        private readonly LinkedInIncrementalFetchOptions _options;

        private int _consecutiveKnownJobs;

        public IncrementalStopState(
            LinkedInIncrementalFetchOptions options,
            IEnumerable<string> knownJobIds)
        {
            _options = options;
            _knownJobIds = new HashSet<string>(knownJobIds, StringComparer.Ordinal);
        }

        public int KnownJobCount => _knownJobIds.Count;

        public IncrementalStopDecision EvaluatePage(LinkedInJobSearchPageContext context)
        {
            var knownOnPage = 0;
            var newOnPage = 0;

            foreach (var job in context.UniqueJobsAdded)
            {
                if (_knownJobIds.Contains(job.LinkedInJobId))
                {
                    knownOnPage++;
                    _consecutiveKnownJobs++;
                }
                else
                {
                    newOnPage++;
                    _knownJobIds.Add(job.LinkedInJobId);
                    _consecutiveKnownJobs = 0;
                }
            }

            if (context.PagesFetched <= _options.OverlapPageCount)
            {
                return new IncrementalStopDecision(
                    false,
                    _consecutiveKnownJobs,
                    knownOnPage,
                    newOnPage,
                    "WithinOverlapWindow");
            }

            if (context.PagesFetched < _options.MinimumPagesBeforeStop)
            {
                return new IncrementalStopDecision(
                    false,
                    _consecutiveKnownJobs,
                    knownOnPage,
                    newOnPage,
                    "BelowMinimumPages");
            }

            var shouldStop = _consecutiveKnownJobs >= _options.KnownStreakThreshold;

            return new IncrementalStopDecision(
                shouldStop,
                _consecutiveKnownJobs,
                knownOnPage,
                newOnPage,
                shouldStop ? "KnownStreakThresholdReached" : "KnownStreakBelowThreshold");
        }
    }

    private readonly record struct IncrementalStopDecision(
        bool ShouldStop,
        int ConsecutiveKnownJobs,
        int KnownOnPage,
        int NewOnPage,
        string Reason);
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
        Message = "Job import diagnostics persisting reconciliation checkpoint. ProcessedCount={ProcessedCount}, ImportedCount={ImportedCount}, RefreshedCount={RefreshedCount}, SkippedCount={SkippedCount}, CheckpointCount={CheckpointCount}")]
    public static partial void JobImportDiagnosticsPersistingCheckpoint(
        ILogger logger,
        int processedCount,
        int importedCount,
        int refreshedCount,
        int skippedCount,
        int checkpointCount);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Information,
        Message = "Job import diagnostics completed reconciliation checkpoint. ProcessedCount={ProcessedCount}, ImportedCount={ImportedCount}, RefreshedCount={RefreshedCount}, SkippedCount={SkippedCount}, CheckpointCount={CheckpointCount}, SaveElapsedMilliseconds={SaveElapsedMilliseconds}")]
    public static partial void JobImportDiagnosticsCompletedCheckpoint(
        ILogger logger,
        int processedCount,
        int importedCount,
        int refreshedCount,
        int skippedCount,
        int checkpointCount,
        long saveElapsedMilliseconds);

    [LoggerMessage(
        EventId = 2106,
        Level = LogLevel.Information,
        Message = "Job import diagnostics incremental policy is disabled. Reason=LinkedIn:IncrementalFetch:Enabled is false.")]
    public static partial void JobImportDiagnosticsIncrementalPolicyDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 2107,
        Level = LogLevel.Information,
        Message = "Job import diagnostics incremental policy configured. KnownJobCount={KnownJobCount}, MinimumPagesBeforeStop={MinimumPagesBeforeStop}, OverlapPageCount={OverlapPageCount}, KnownStreakThreshold={KnownStreakThreshold}")]
    public static partial void JobImportDiagnosticsIncrementalPolicyConfigured(
        ILogger logger,
        int knownJobCount,
        int minimumPagesBeforeStop,
        int overlapPageCount,
        int knownStreakThreshold);

    [LoggerMessage(
        EventId = 2108,
        Level = LogLevel.Information,
        Message = "Job import diagnostics incremental policy evaluated page. PageIndex={PageIndex}, PagesFetched={PagesFetched}, KnownOnPage={KnownOnPage}, NewOnPage={NewOnPage}, ConsecutiveKnownJobs={ConsecutiveKnownJobs}, ShouldStop={ShouldStop}, Reason={Reason}")]
    public static partial void JobImportDiagnosticsIncrementalPolicyEvaluatedPage(
        ILogger logger,
        int pageIndex,
        int pagesFetched,
        int knownOnPage,
        int newOnPage,
        int consecutiveKnownJobs,
        bool shouldStop,
        string reason);
}
