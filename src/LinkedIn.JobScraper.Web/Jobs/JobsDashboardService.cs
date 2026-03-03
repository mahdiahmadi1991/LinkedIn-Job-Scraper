using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobsDashboardService : IJobsDashboardService
{
    private const int JobsPageSize = 40;
    private static readonly Action<ILogger, string, string?, Exception?> LogWorkflowStarted =
        LoggerMessage.Define<string, string?>(
            LogLevel.Information,
            new EventId(2001, nameof(LogWorkflowStarted)),
            "Fetch and score workflow started. WorkflowId={WorkflowId}, ProgressConnectionId={ProgressConnectionId}");
    private static readonly Action<ILogger, string, int, int, int, int, Exception?> LogImportCompleted =
        LoggerMessage.Define<string, int, int, int, int>(
            LogLevel.Information,
            new EventId(2002, nameof(LogImportCompleted)),
            "Fetch and score import completed. WorkflowId={WorkflowId}, PagesFetched={PagesFetched}, FetchedCount={FetchedCount}, ImportedCount={ImportedCount}, RefreshedCount={RefreshedCount}");
    private static readonly Action<ILogger, string, int, int, int, int, Exception?> LogEnrichmentCompleted =
        LoggerMessage.Define<string, int, int, int, int>(
            LogLevel.Information,
            new EventId(2003, nameof(LogEnrichmentCompleted)),
            "Fetch and score enrichment completed. WorkflowId={WorkflowId}, RequestedCount={RequestedCount}, ProcessedCount={ProcessedCount}, EnrichedCount={EnrichedCount}, FailedCount={FailedCount}");
    private static readonly Action<ILogger, string, int, int, int, int, Exception?> LogScoringCompleted =
        LoggerMessage.Define<string, int, int, int, int>(
            LogLevel.Information,
            new EventId(2004, nameof(LogScoringCompleted)),
            "Fetch and score scoring completed. WorkflowId={WorkflowId}, RequestedCount={RequestedCount}, ProcessedCount={ProcessedCount}, ScoredCount={ScoredCount}, FailedCount={FailedCount}");
    private static readonly Action<ILogger, string, bool, string, Exception?> LogWorkflowCompleted =
        LoggerMessage.Define<string, bool, string>(
            LogLevel.Information,
            new EventId(2005, nameof(LogWorkflowCompleted)),
            "Fetch and score workflow completed. WorkflowId={WorkflowId}, Success={Success}, Severity={Severity}");
    private static readonly Action<ILogger, string, string, Exception?> LogWorkflowFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2006, nameof(LogWorkflowFailed)),
            "Fetch and score workflow failed early. WorkflowId={WorkflowId}, Reason={Reason}");

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IJobEnrichmentService _jobEnrichmentService;
    private readonly IJobImportService _jobImportService;
    private readonly IJobsWorkflowProgressNotifier _jobsWorkflowProgressNotifier;
    private readonly IJobsWorkflowStateStore _jobsWorkflowStateStore;
    private readonly IJobBatchScoringService _jobBatchScoringService;
    private readonly ILogger<JobsDashboardService> _logger;

    public JobsDashboardService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobImportService jobImportService,
        IJobEnrichmentService jobEnrichmentService,
        IJobBatchScoringService jobBatchScoringService,
        IJobsWorkflowStateStore jobsWorkflowStateStore,
        IJobsWorkflowProgressNotifier jobsWorkflowProgressNotifier,
        ILogger<JobsDashboardService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _jobImportService = jobImportService;
        _jobEnrichmentService = jobEnrichmentService;
        _jobBatchScoringService = jobBatchScoringService;
        _jobsWorkflowStateStore = jobsWorkflowStateStore;
        _jobsWorkflowProgressNotifier = jobsWorkflowProgressNotifier;
        _logger = logger;
    }

    public async Task<JobsDashboardSnapshot> GetSnapshotAsync(
        JobsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        query ??= new JobsDashboardQuery();

        var filteredQuery = ApplyFilters(dbContext.Jobs.AsNoTracking(), query);

        var filteredJobs = await filteredQuery.CountAsync(cancellationToken);
        var aiOutputLanguageCode = await GetAiOutputLanguageCodeAsync(dbContext, cancellationToken);
        var rowsChunk = await GetRowsChunkAsync(
            ApplySorting(filteredQuery, query),
            query,
            aiOutputLanguageCode,
            0,
            cancellationToken);

        var dashboardCounts = await dbContext.Jobs
            .GroupBy(static _ => 1)
            .Select(
                static jobs => new
                {
                    TotalJobs = jobs.Count(),
                    ScoredJobs = jobs.Count(job => job.AiScore != null),
                    StrongMatches = jobs.Count(job => job.AiLabel == "StrongMatch")
                })
            .SingleOrDefaultAsync(cancellationToken);

        var totalJobs = dashboardCounts?.TotalJobs ?? 0;
        var scoredJobs = dashboardCounts?.ScoredJobs ?? 0;
        var strongMatches = dashboardCounts?.StrongMatches ?? 0;

        return new JobsDashboardSnapshot(
            totalJobs,
            filteredJobs,
            scoredJobs,
            strongMatches,
            totalJobs - scoredJobs,
            query,
            rowsChunk);
    }

    public async Task<JobDetailsSnapshot?> GetJobDetailsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var aiOutputLanguageCode = await GetAiOutputLanguageCodeAsync(dbContext, cancellationToken);

        return new JobDetailsSnapshot(
            job.Id,
            job.Title,
            job.CompanyName,
            job.LocationName,
            job.EmploymentStatus,
            job.ListedAtUtc,
            job.FirstDiscoveredAtUtc,
            job.LastSeenAtUtc,
            ToViewStatus(job.CurrentStatus),
            job.Description,
            job.CompanyApplyUrl,
            job.AiScore,
            job.AiLabel,
            job.AiSummary,
            job.AiWhyMatched,
            job.AiConcerns,
            aiOutputLanguageCode,
            AiOutputLanguage.GetDirection(aiOutputLanguageCode));
    }

    public async Task<JobsRowsChunk> GetRowsAsync(
        JobsDashboardQuery query,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        query ??= new JobsDashboardQuery();
        offset = Math.Max(0, offset);

        var filteredQuery = ApplyFilters(dbContext.Jobs.AsNoTracking(), query);
        var sortedQuery = ApplySorting(filteredQuery, query);
        var aiOutputLanguageCode = await GetAiOutputLanguageCodeAsync(dbContext, cancellationToken);

        return await GetRowsChunkAsync(
            sortedQuery,
            query,
            aiOutputLanguageCode,
            offset,
            cancellationToken);
    }

    public async Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
        string? progressConnectionId,
        string workflowId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var workflowCancellationToken = _jobsWorkflowStateStore.RegisterWorkflow(workflowId, cancellationToken);
        var effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
        LogWorkflowStarted(_logger, effectiveCorrelationId, progressConnectionId, null);

        JobImportResult? importResult = null;
        JobEnrichmentResult? enrichmentResult = null;
        JobBatchScoringResult? scoringResult = null;

        await PublishProgressAsync(
            progressConnectionId,
            new JobsWorkflowProgressUpdate(
                workflowId,
                effectiveCorrelationId,
                "running",
                "fetch",
                10,
                "Workflow accepted. Starting LinkedIn fetch using the current stored session and search settings."),
            CancellationToken.None);

        try
        {
            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "running",
                    "fetch",
                    16,
                    "Calling LinkedIn search endpoints and reconciling results with the local database..."),
                CancellationToken.None);

            importResult = await _jobImportService.ImportCurrentSearchAsync(workflowCancellationToken);

            if (!importResult.Success)
            {
                LogWorkflowFailed(_logger, effectiveCorrelationId, importResult.Message, null);

                await PublishProgressAsync(
                    progressConnectionId,
                    new JobsWorkflowProgressUpdate(
                        workflowId,
                        effectiveCorrelationId,
                        "failed",
                        "fetch",
                        100,
                        importResult.Message,
                        importResult.FetchedCount,
                        importResult.FetchedCount,
                        importResult.ImportedCount + importResult.UpdatedExistingCount,
                        1),
                    CancellationToken.None);

                return new FetchAndScoreWorkflowResult(
                    false,
                    $"Fetch failed. {importResult.Message}",
                    "danger",
                    importResult,
                    null,
                    null);
            }

            LogImportCompleted(
                _logger,
                effectiveCorrelationId,
                importResult.PagesFetched,
                importResult.FetchedCount,
                importResult.ImportedCount,
                importResult.UpdatedExistingCount,
                null);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "running",
                    "fetch",
                    32,
                    $"Fetch completed. {importResult.FetchedCount} jobs collected across {importResult.PagesFetched} page(s): {importResult.ImportedCount} new, {importResult.UpdatedExistingCount} refreshed, {importResult.SkippedCount} skipped.",
                    importResult.TotalAvailableCount,
                    importResult.FetchedCount,
                    importResult.ImportedCount + importResult.UpdatedExistingCount,
                    0),
                CancellationToken.None);

            var enrichmentBatchSize = Math.Clamp(
                importResult.ImportedCount > 0 ? importResult.ImportedCount : Math.Min(importResult.FetchedCount, 5),
                1,
                25);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "running",
                    "enrichment",
                    46,
                    $"Preparing enrichment batch. Up to {enrichmentBatchSize} jobs will request LinkedIn detail payloads.",
                    enrichmentBatchSize,
                    0,
                    0,
                    0),
                CancellationToken.None);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "running",
                    "enrichment",
                    54,
                    "Calling LinkedIn job detail endpoints for incomplete records..."),
                CancellationToken.None);

            enrichmentResult = await _jobEnrichmentService.EnrichIncompleteJobsAsync(
                enrichmentBatchSize,
                workflowCancellationToken);

            LogEnrichmentCompleted(
                _logger,
                effectiveCorrelationId,
                enrichmentResult.RequestedCount,
                enrichmentResult.ProcessedCount,
                enrichmentResult.EnrichedCount,
                enrichmentResult.FailedCount,
                null);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    enrichmentResult.Success ? "running" : "warning",
                    "enrichment",
                    68,
                    enrichmentResult.Success
                        ? $"Enrichment completed. {enrichmentResult.EnrichedCount} of {enrichmentResult.ProcessedCount} processed jobs were updated. Warnings: {enrichmentResult.WarningCount}. Failed: {enrichmentResult.FailedCount}."
                        : $"Enrichment issue: {enrichmentResult.Message}",
                    enrichmentResult.RequestedCount,
                    enrichmentResult.ProcessedCount,
                    enrichmentResult.EnrichedCount,
                    enrichmentResult.FailedCount),
                CancellationToken.None);

            var scoringBatchSize = Math.Clamp(
                enrichmentResult.Success
                    ? Math.Max(enrichmentResult.EnrichedCount, Math.Min(importResult.ImportedCount, 5))
                    : Math.Max(Math.Min(importResult.ImportedCount, 5), 1),
                1,
                10);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "running",
                    "scoring",
                    78,
                    $"Preparing AI scoring batch. Up to {scoringBatchSize} jobs will be sent to OpenAI for evaluation.",
                    scoringBatchSize,
                    0,
                    0,
                    0),
                CancellationToken.None);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "running",
                    "scoring",
                    86,
                    "Submitting the scoring batch to OpenAI and waiting for parsed results..."),
                CancellationToken.None);

            scoringResult = await _jobBatchScoringService.ScoreReadyJobsAsync(
                scoringBatchSize,
                workflowCancellationToken);

            LogScoringCompleted(
                _logger,
                effectiveCorrelationId,
                scoringResult.RequestedCount,
                scoringResult.ProcessedCount,
                scoringResult.ScoredCount,
                scoringResult.FailedCount,
                null);

            var severity = "success";
            var messageParts = new List<string>
            {
                $"Import: +{importResult.ImportedCount} new, {importResult.UpdatedExistingCount} refreshed."
            };

            if (enrichmentResult.Success)
            {
                messageParts.Add($"Enrichment: {enrichmentResult.EnrichedCount} updated.");
            }
            else
            {
                severity = "warning";
                messageParts.Add($"Enrichment issue: {enrichmentResult.Message}");
            }

            if (scoringResult.Success)
            {
                messageParts.Add($"AI scoring: {scoringResult.ScoredCount} scored.");
            }
            else
            {
                severity = severity == "danger" ? "danger" : "warning";
                messageParts.Add($"AI scoring issue: {scoringResult.Message}");
            }

            var workflowResult = new FetchAndScoreWorkflowResult(
                true,
                string.Join(' ', messageParts),
                severity,
                importResult,
                enrichmentResult,
                scoringResult);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    severity == "danger" ? "failed" : severity == "warning" ? "warning" : "completed",
                    "completed",
                    100,
                    workflowResult.Message,
                    scoringResult.RequestedCount,
                    scoringResult.ProcessedCount,
                    scoringResult.ScoredCount,
                    scoringResult.FailedCount),
                CancellationToken.None);

            LogWorkflowCompleted(_logger, effectiveCorrelationId, workflowResult.Success, workflowResult.Severity, null);

            return workflowResult;
        }
        catch (OperationCanceledException) when (workflowCancellationToken.IsCancellationRequested)
        {
            var cancelledImport = importResult ?? JobImportResult.Failed("Workflow was cancelled before fetch completed.", StatusCodes.Status409Conflict);
            var cancelledResult = new FetchAndScoreWorkflowResult(
                false,
                "Fetch & Score was cancelled.",
                "warning",
                cancelledImport,
                enrichmentResult,
                scoringResult);

            await PublishProgressAsync(
                progressConnectionId,
                new JobsWorkflowProgressUpdate(
                    workflowId,
                    effectiveCorrelationId,
                    "cancelled",
                    "completed",
                    100,
                    "Cancellation requested. The workflow stopped before completing all remaining background work.",
                    scoringResult?.RequestedCount ?? enrichmentResult?.RequestedCount ?? importResult?.TotalAvailableCount,
                    scoringResult?.ProcessedCount ?? enrichmentResult?.ProcessedCount ?? importResult?.FetchedCount,
                    scoringResult?.ScoredCount ?? enrichmentResult?.EnrichedCount ?? (importResult?.ImportedCount ?? 0) + (importResult?.UpdatedExistingCount ?? 0),
                    (scoringResult?.FailedCount ?? 0) + (enrichmentResult?.FailedCount ?? 0)),
                CancellationToken.None);

            LogWorkflowCompleted(_logger, effectiveCorrelationId, cancelledResult.Success, cancelledResult.Severity, null);
            return cancelledResult;
        }
    }

    public async Task<JobStatusChangeResult> UpdateStatusAsync(
        Guid jobId,
        JobWorkflowState status,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.Jobs.SingleOrDefaultAsync(
            job => job.Id == jobId,
            cancellationToken);

        if (job is null)
        {
            return new JobStatusChangeResult(false, "Job was not found.", "danger");
        }

        var entityStatus = ToEntityStatus(status);

        job.CurrentStatus = entityStatus;

        dbContext.JobStatusHistory.Add(
            new JobStatusHistoryRecord
            {
                JobRecordId = job.Id,
                Status = entityStatus,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new JobStatusChangeResult(
                false,
                "Job status was updated by another operation. Refresh the dashboard and try again.",
                "danger");
        }

        return new JobStatusChangeResult(
            true,
            $"Status for '{job.Title}' was updated to {status}.",
            "success");
    }

    private Task PublishProgressAsync(
        string? progressConnectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken)
    {
        return _jobsWorkflowProgressNotifier.PublishAsync(progressConnectionId, update, cancellationToken);
    }

    private static IQueryable<JobRecord> ApplyFilters(
        IQueryable<JobRecord> queryable,
        JobsDashboardQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            queryable = queryable.Where(
                job =>
                    job.Title.Contains(search) ||
                    (job.CompanyName != null && job.CompanyName.Contains(search)) ||
                    (job.LocationName != null && job.LocationName.Contains(search)) ||
                    (job.AiSummary != null && job.AiSummary.Contains(search)));
        }

        if (query.FilterStatus.HasValue)
        {
            var status = ToEntityStatus(query.FilterStatus.Value);
            queryable = queryable.Where(job => job.CurrentStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.AiLabel))
        {
            var aiLabel = query.AiLabel.Trim();
            queryable = queryable.Where(job => job.AiLabel == aiLabel);
        }

        if (query.OnlyUnscored)
        {
            queryable = queryable.Where(static job => job.AiScore == null);
        }

        if (query.MinScore.HasValue)
        {
            var minScore = query.MinScore.Value;
            queryable = queryable.Where(job => job.AiScore != null && job.AiScore >= minScore);
        }

        return queryable;
    }

    private static IQueryable<JobRecord> ApplySorting(
        IQueryable<JobRecord> queryable,
        JobsDashboardQuery query)
    {
        return query.GetNormalizedSortBy() switch
        {
            "listed" => queryable
                .OrderByDescending(job => job.ListedAtUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "score" => queryable
                .OrderByDescending(job => job.AiScore ?? int.MinValue)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "title" => queryable
                .OrderBy(job => job.Title)
                .ThenByDescending(job => job.LastSeenAtUtc),
            "company" => queryable
                .OrderBy(job => job.CompanyName ?? string.Empty)
                .ThenBy(job => job.Title),
            _ => queryable
                .OrderByDescending(job => job.LastSeenAtUtc)
                .ThenByDescending(job => job.AiScore ?? int.MinValue)
        };
    }

    private static async Task<JobsRowsChunk> GetRowsChunkAsync(
        IQueryable<JobRecord> sortedQuery,
        JobsDashboardQuery query,
        string aiOutputLanguageCode,
        int offset,
        CancellationToken cancellationToken)
    {
        var page = await sortedQuery
            .Skip(offset)
            .Take(JobsPageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMoreJobs = page.Count > JobsPageSize;
        var materializedRows = page
            .Take(JobsPageSize)
            .Select(
                static job => new JobDashboardRow(
                    job.Id,
                    job.Title,
                    job.CompanyName,
                    job.LocationName,
                    job.EmploymentStatus,
                    job.ListedAtUtc,
                    job.LastSeenAtUtc,
                    ToViewStatus(job.CurrentStatus),
                    job.AiScore,
                    job.AiLabel,
                    job.AiSummary,
                    job.AiWhyMatched,
                    job.AiConcerns,
                    job.CompanyApplyUrl))
            .ToArray();

        return new JobsRowsChunk(
            query,
            materializedRows,
            offset + materializedRows.Length,
            hasMoreJobs,
            aiOutputLanguageCode,
            AiOutputLanguage.GetDirection(aiOutputLanguageCode));
    }

    private static async Task<string> GetAiOutputLanguageCodeAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var outputLanguageCode = await dbContext.AiBehaviorSettings
            .AsNoTracking()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .ThenByDescending(settings => settings.Id)
            .Select(settings => settings.OutputLanguageCode)
            .FirstOrDefaultAsync(cancellationToken);

        return AiOutputLanguage.Normalize(outputLanguageCode);
    }

    private static JobWorkflowState ToViewStatus(Persistence.Entities.JobWorkflowStatus status)
    {
        return status switch
        {
            Persistence.Entities.JobWorkflowStatus.New => JobWorkflowState.New,
            Persistence.Entities.JobWorkflowStatus.Shortlisted => JobWorkflowState.Shortlisted,
            Persistence.Entities.JobWorkflowStatus.Applied => JobWorkflowState.Applied,
            Persistence.Entities.JobWorkflowStatus.Ignored => JobWorkflowState.Ignored,
            Persistence.Entities.JobWorkflowStatus.Archived => JobWorkflowState.Archived,
            _ => JobWorkflowState.New
        };
    }

    private static Persistence.Entities.JobWorkflowStatus ToEntityStatus(JobWorkflowState status)
    {
        return status switch
        {
            JobWorkflowState.New => Persistence.Entities.JobWorkflowStatus.New,
            JobWorkflowState.Shortlisted => Persistence.Entities.JobWorkflowStatus.Shortlisted,
            JobWorkflowState.Applied => Persistence.Entities.JobWorkflowStatus.Applied,
            JobWorkflowState.Ignored => Persistence.Entities.JobWorkflowStatus.Ignored,
            JobWorkflowState.Archived => Persistence.Entities.JobWorkflowStatus.Archived,
            _ => Persistence.Entities.JobWorkflowStatus.New
        };
    }
}
