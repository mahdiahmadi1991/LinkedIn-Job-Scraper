using System.Diagnostics;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class AiGlobalShortlistService : IAiGlobalShortlistService
{
    private const string RunStatusPending = "Pending";
    private const string RunStatusRunning = "Running";
    private const string RunStatusCompleted = "Completed";
    private const string RunStatusFailed = "Failed";
    private const string RunStatusCancelled = "Cancelled";

    private const string DecisionAccepted = "Accepted";
    private const string DecisionRejected = "Rejected";
    private const string DecisionNeedsReview = "NeedsReview";
    private const string CandidateStatusPending = "Pending";

    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IAiBehaviorSettingsService _behaviorSettingsService;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IAiGlobalShortlistGateway _globalShortlistGateway;
    private readonly IAiGlobalShortlistProgressNotifier _progressNotifier;
    private readonly IAiGlobalShortlistProgressStateStore _progressStateStore;
    private readonly IJobScoringGateway _jobScoringGateway;
    private readonly ILogger<AiGlobalShortlistService> _logger;
    private readonly IOptions<AiGlobalShortlistOptions> _shortlistOptions;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;

    public AiGlobalShortlistService(
        ICurrentAppUserContext currentAppUserContext,
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IAiGlobalShortlistGateway globalShortlistGateway,
        IAiGlobalShortlistProgressNotifier progressNotifier,
        IAiGlobalShortlistProgressStateStore progressStateStore,
        IJobScoringGateway jobScoringGateway,
        IAiBehaviorSettingsService behaviorSettingsService,
        IOptions<AiGlobalShortlistOptions> shortlistOptions,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions,
        ILogger<AiGlobalShortlistService> logger)
    {
        _currentAppUserContext = currentAppUserContext;
        _dbContextFactory = dbContextFactory;
        _globalShortlistGateway = globalShortlistGateway;
        _progressNotifier = progressNotifier;
        _progressStateStore = progressStateStore;
        _jobScoringGateway = jobScoringGateway;
        _behaviorSettingsService = behaviorSettingsService;
        _shortlistOptions = shortlistOptions;
        _openAiSecurityOptions = openAiSecurityOptions;
        _logger = logger;
    }

    public async Task<AiGlobalShortlistRunResult> GenerateAsync(
        CancellationToken cancellationToken,
        string? progressConnectionId = null,
        JobStageProgressCallback? progressCallback = null)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await RecoverOrphanedActiveRunsAsync(dbContext, userId, cancellationToken);

        var activeRun = await dbContext.AiGlobalShortlistRuns
            .AsNoTracking()
            .Where(run => run.AppUserId == userId)
            .Where(run => run.Status == RunStatusPending || run.Status == RunStatusRunning)
            .OrderByDescending(run => run.CreatedAtUtc)
            .Select(
                run => new
                {
                    run.Id,
                    run.CandidateCount,
                    run.ProcessedCount,
                    run.ShortlistedCount,
                    run.NeedsReviewCount,
                    run.FailedCount,
                    run.Status
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRun is not null)
        {
            return AiGlobalShortlistRunResult.Failed(
                $"Another AI live review run is already {activeRun.Status.ToLowerInvariant()} (run id: {activeRun.Id}). Stop or finish that run before starting a new one.",
                StatusCodes.Status409Conflict,
                activeRun.Id,
                activeRun.CandidateCount,
                activeRun.ProcessedCount,
                activeRun.ShortlistedCount,
                activeRun.NeedsReviewCount,
                activeRun.FailedCount);
        }

        var options = _shortlistOptions.Value;
        var promptVersion = options.GetPromptVersion();
        var modelName = _openAiSecurityOptions.Value.Model;

        var candidates = await SelectCandidatesAsync(
            dbContext,
            userId,
            options.GetMaxCandidateCount(),
            cancellationToken);
        var run = new AiGlobalShortlistRunRecord
        {
            AppUserId = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = RunStatusPending,
            CandidateCount = candidates.Count,
            ProcessedCount = 0,
            NextSequenceNumber = 1,
            ShortlistedCount = 0,
            NeedsReviewCount = 0,
            FailedCount = 0,
            PromptVersion = promptVersion,
            ModelName = modelName
        };

        dbContext.AiGlobalShortlistRuns.Add(run);

        if (candidates.Count > 0)
        {
            var snapshotRows = candidates
                .Select(
                    (candidate, index) =>
                        new AiGlobalShortlistRunCandidateRecord
                        {
                            RunId = run.Id,
                            JobRecordId = candidate.JobId,
                            SequenceNumber = index + 1,
                            Status = CandidateStatusPending
                        })
                .ToArray();

            dbContext.AiGlobalShortlistRunCandidates.AddRange(snapshotRows);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishProgressSafeAsync(
            userId,
            progressConnectionId,
            new AiGlobalShortlistProgressUpdate(
                run.Id,
                "pending",
                "snapshot-created",
                $"Snapshot created with {candidates.Count} candidate(s).",
                CandidateCount: candidates.Count,
                ProcessedCount: 0,
                AcceptedCount: 0,
                NeedsReviewCount: 0,
                FailedCount: 0),
            cancellationToken);

        if (candidates.Count == 0)
        {
            run.Status = RunStatusCompleted;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.Summary = LimitText("No enriched jobs were available for shortlist generation.", 2000);
            await dbContext.SaveChangesAsync(cancellationToken);

            await PublishProgressSafeAsync(
                userId,
                progressConnectionId,
                new AiGlobalShortlistProgressUpdate(
                    run.Id,
                    "completed",
                    "run-finished",
                    "No candidates were available for processing.",
                    CandidateCount: 0,
                    ProcessedCount: 0,
                    AcceptedCount: 0,
                    NeedsReviewCount: 0,
                    FailedCount: 0),
                cancellationToken);

            return AiGlobalShortlistRunResult.Succeeded(run.Id, 0, 0, 0, 0, 0);
        }

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Created shortlist snapshot with {candidates.Count} candidate job(s).",
                    candidates.Count,
                    0,
                    0,
                    0),
                cancellationToken);
        }

        return await ResumeAsync(run.Id, cancellationToken, progressConnectionId, progressCallback);
    }

    public async Task<AiGlobalShortlistRunResult> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken,
        string? progressConnectionId = null,
        JobStageProgressCallback? progressCallback = null)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var run = await dbContext.AiGlobalShortlistRuns
            .SingleOrDefaultAsync(candidate => candidate.Id == runId && candidate.AppUserId == userId, cancellationToken);

        if (run is null)
        {
            return AiGlobalShortlistRunResult.Failed(
                "AI global shortlist run was not found.",
                StatusCodes.Status404NotFound,
                runId);
        }

        if (run.Status == RunStatusCompleted)
        {
            return AiGlobalShortlistRunResult.Succeeded(
                run.Id,
                run.CandidateCount,
                run.ProcessedCount,
                run.ShortlistedCount,
                run.NeedsReviewCount,
                run.FailedCount);
        }

        var options = _shortlistOptions.Value;
        var behaviorProfile = await _behaviorSettingsService.GetActiveAsync(cancellationToken);
        var interCandidateDelay = options.GetInterCandidateDelay();
        var acceptedThreshold = options.GetAcceptedScoreThreshold();
        var rejectedThreshold = options.GetRejectedScoreThreshold();
        var promptVersion = options.GetPromptVersion();
        var fallbackRemaining = options.GetFallbackPerItemCap();

        run.Status = RunStatusRunning;
        run.CompletedAtUtc = null;
        run.CancellationRequestedAtUtc = null;
        run.PromptVersion = promptVersion;
        run.ModelName ??= _openAiSecurityOptions.Value.Model;
        run.NextSequenceNumber = Math.Max(1, run.NextSequenceNumber);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishProgressSafeAsync(
            userId,
            progressConnectionId,
            new AiGlobalShortlistProgressUpdate(
                run.Id,
                "running",
                "run-started",
                $"Run is processing from checkpoint {run.NextSequenceNumber}/{run.CandidateCount}.",
                CandidateCount: run.CandidateCount,
                ProcessedCount: run.ProcessedCount,
                AcceptedCount: run.ShortlistedCount,
                NeedsReviewCount: run.NeedsReviewCount,
                FailedCount: run.FailedCount),
            cancellationToken);

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Resuming shortlist run from checkpoint {run.NextSequenceNumber}/{run.CandidateCount}.",
                    run.CandidateCount,
                    run.ProcessedCount,
                    run.ShortlistedCount,
                    run.FailedCount),
                cancellationToken);
        }

        try
        {
            while (run.ProcessedCount < run.CandidateCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await dbContext.Entry(run).ReloadAsync(cancellationToken);

                if (run.CancellationRequestedAtUtc.HasValue)
                {
                    await MarkRunCancelledAsync(dbContext, run, cancellationToken);
                    await PublishProgressSafeAsync(
                        userId,
                        progressConnectionId,
                        new AiGlobalShortlistProgressUpdate(
                            run.Id,
                            "cancelled",
                            "run-cancelled",
                            $"Run cancelled at checkpoint {run.ProcessedCount}/{run.CandidateCount}.",
                            CandidateCount: run.CandidateCount,
                            ProcessedCount: run.ProcessedCount,
                            AcceptedCount: run.ShortlistedCount,
                            NeedsReviewCount: run.NeedsReviewCount,
                            FailedCount: run.FailedCount),
                        cancellationToken);

                    return AiGlobalShortlistRunResult.Cancelled(
                        run.Id,
                        run.CandidateCount,
                        run.ProcessedCount,
                        run.ShortlistedCount,
                        run.NeedsReviewCount,
                        run.FailedCount);
                }

                var runCandidate = await dbContext.AiGlobalShortlistRunCandidates
                    .Include(candidate => candidate.JobRecord)
                    .Where(
                        candidate =>
                            candidate.RunId == run.Id &&
                            candidate.SequenceNumber >= run.NextSequenceNumber &&
                            candidate.Status == CandidateStatusPending)
                    .OrderBy(candidate => candidate.SequenceNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (runCandidate is null)
                {
                    break;
                }

                await PublishProgressSafeAsync(
                    userId,
                    progressConnectionId,
                    new AiGlobalShortlistProgressUpdate(
                        run.Id,
                        "running",
                        "candidate-started",
                        $"Evaluating candidate {runCandidate.SequenceNumber}/{run.CandidateCount}: {runCandidate.JobRecord.Title}.",
                        CandidateCount: run.CandidateCount,
                        ProcessedCount: run.ProcessedCount,
                        AcceptedCount: run.ShortlistedCount,
                        NeedsReviewCount: run.NeedsReviewCount,
                        FailedCount: run.FailedCount,
                        SequenceNumber: runCandidate.SequenceNumber,
                        JobId: runCandidate.JobRecordId,
                        LinkedInJobId: runCandidate.JobRecord.LinkedInJobId,
                        JobTitle: runCandidate.JobRecord.Title,
                        CompanyName: runCandidate.JobRecord.CompanyName,
                        LocationName: runCandidate.JobRecord.LocationName),
                    cancellationToken);

                var evaluation = await EvaluateCandidateAsync(
                    userId,
                    run.Id,
                    runCandidate.SequenceNumber,
                    runCandidate.JobRecord,
                    behaviorProfile,
                    acceptedThreshold,
                    rejectedThreshold,
                    fallbackRemaining > 0,
                    options.GetTransientRetryAttempts(),
                    options.GetTransientRetryBaseDelay(),
                    progressConnectionId,
                    cancellationToken);

                if (evaluation.UsedFallback && fallbackRemaining > 0)
                {
                    fallbackRemaining--;
                }

                var now = DateTimeOffset.UtcNow;
                var rank = run.ProcessedCount + 1;
                var item = await dbContext.AiGlobalShortlistItems
                    .SingleOrDefaultAsync(
                        candidate =>
                            candidate.RunId == run.Id &&
                            candidate.JobRecordId == runCandidate.JobRecordId,
                        cancellationToken);

                if (item is null)
                {
                    item = new AiGlobalShortlistItemRecord
                    {
                        RunId = run.Id,
                        JobRecordId = runCandidate.JobRecordId
                    };

                    dbContext.AiGlobalShortlistItems.Add(item);
                }

                item.Rank = rank;
                item.Decision = evaluation.Decision;
                item.CreatedAtUtc = now;
                item.PromptVersion = promptVersion;
                item.ModelName = evaluation.ModelName ?? run.ModelName;
                item.LatencyMilliseconds = evaluation.LatencyMilliseconds;
                item.InputTokenCount = evaluation.InputTokenCount;
                item.OutputTokenCount = evaluation.OutputTokenCount;
                item.TotalTokenCount = evaluation.TotalTokenCount;
                item.ErrorCode = evaluation.ErrorCode;
                item.Score = evaluation.Score;
                item.Confidence = evaluation.Confidence;
                item.RecommendationReason = LimitText(evaluation.RecommendationReason, 2000);
                item.Concerns = LimitText(evaluation.Concerns, 2000);

                runCandidate.Status = evaluation.Decision;
                runCandidate.ProcessedAtUtc = now;

                run.ProcessedCount = rank;
                run.NextSequenceNumber = runCandidate.SequenceNumber + 1;

                if (evaluation.Decision == DecisionAccepted)
                {
                    run.ShortlistedCount++;
                }

                if (evaluation.Decision == DecisionNeedsReview)
                {
                    run.NeedsReviewCount++;
                }

                if (!string.IsNullOrWhiteSpace(evaluation.ErrorCode))
                {
                    run.FailedCount++;
                }

                run.Summary = LimitText(
                    $"Checkpoint {run.ProcessedCount}/{run.CandidateCount}. " +
                    $"Accepted: {run.ShortlistedCount}, NeedsReview: {run.NeedsReviewCount}, Failed: {run.FailedCount}.",
                    2000);

                await dbContext.SaveChangesAsync(cancellationToken);

                await PublishProgressSafeAsync(
                    userId,
                    progressConnectionId,
                    new AiGlobalShortlistProgressUpdate(
                        run.Id,
                        "running",
                        "candidate-processed",
                        $"Candidate {run.ProcessedCount}/{run.CandidateCount} processed as {evaluation.Decision}.",
                        CandidateCount: run.CandidateCount,
                        ProcessedCount: run.ProcessedCount,
                        AcceptedCount: run.ShortlistedCount,
                        NeedsReviewCount: run.NeedsReviewCount,
                        FailedCount: run.FailedCount,
                        SequenceNumber: runCandidate.SequenceNumber,
                        Decision: evaluation.Decision,
                        JobId: runCandidate.JobRecordId,
                        LinkedInJobId: runCandidate.JobRecord.LinkedInJobId,
                        JobTitle: runCandidate.JobRecord.Title,
                        CompanyName: runCandidate.JobRecord.CompanyName,
                        LocationName: runCandidate.JobRecord.LocationName,
                        Score: evaluation.Score,
                        Confidence: evaluation.Confidence,
                        RecommendationReason: evaluation.RecommendationReason,
                        Concerns: evaluation.Concerns,
                        ErrorCode: evaluation.ErrorCode),
                    cancellationToken);

                if (progressCallback is not null)
                {
                    await progressCallback(
                        new JobStageProgress(
                            $"Shortlist candidate {run.ProcessedCount}/{run.CandidateCount} processed as {evaluation.Decision}.",
                            run.CandidateCount,
                            run.ProcessedCount,
                            run.ShortlistedCount,
                            run.FailedCount),
                        cancellationToken);
                }

                if (interCandidateDelay <= TimeSpan.Zero ||
                    run.ProcessedCount >= run.CandidateCount)
                {
                    continue;
                }

                await PublishProgressSafeAsync(
                    userId,
                    progressConnectionId,
                    new AiGlobalShortlistProgressUpdate(
                        run.Id,
                        "running",
                        "candidate-delay",
                        $"Throttling enabled. Waiting {(int)Math.Ceiling(interCandidateDelay.TotalMilliseconds)}ms before the next candidate.",
                        CandidateCount: run.CandidateCount,
                        ProcessedCount: run.ProcessedCount,
                        AcceptedCount: run.ShortlistedCount,
                        NeedsReviewCount: run.NeedsReviewCount,
                        FailedCount: run.FailedCount),
                    cancellationToken);

                await Task.Delay(interCandidateDelay, cancellationToken);
            }

            run.Status = RunStatusCompleted;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.Summary = LimitText(
                $"Processed {run.ProcessedCount}/{run.CandidateCount}. " +
                $"Accepted: {run.ShortlistedCount}, NeedsReview: {run.NeedsReviewCount}, Failed: {run.FailedCount}.",
                2000);

            await dbContext.SaveChangesAsync(cancellationToken);

            await PublishProgressSafeAsync(
                userId,
                progressConnectionId,
                new AiGlobalShortlistProgressUpdate(
                    run.Id,
                    "completed",
                    "run-finished",
                    $"Run completed: {run.ProcessedCount}/{run.CandidateCount} processed.",
                    CandidateCount: run.CandidateCount,
                    ProcessedCount: run.ProcessedCount,
                    AcceptedCount: run.ShortlistedCount,
                    NeedsReviewCount: run.NeedsReviewCount,
                    FailedCount: run.FailedCount),
                cancellationToken);

            return AiGlobalShortlistRunResult.Succeeded(
                run.Id,
                run.CandidateCount,
                run.ProcessedCount,
                run.ShortlistedCount,
                run.NeedsReviewCount,
                run.FailedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryMarkRunCancelledAsync(runId, userId);
            throw;
        }
        catch (Exception exception)
        {
            Log.AiGlobalShortlistRunFailedUnexpectedly(_logger, exception);

            run.Status = RunStatusFailed;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.Summary = LimitText(
                $"Run failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}",
                2000);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await PublishProgressSafeAsync(
                    userId,
                    progressConnectionId,
                    new AiGlobalShortlistProgressUpdate(
                        run.Id,
                        "failed",
                        "run-failed",
                        $"Run failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}",
                        CandidateCount: run.CandidateCount,
                        ProcessedCount: run.ProcessedCount,
                        AcceptedCount: run.ShortlistedCount,
                        NeedsReviewCount: run.NeedsReviewCount,
                        FailedCount: run.FailedCount,
                        ErrorCode: "UNEXPECTED_ERROR"),
                    cancellationToken);
            }
            catch (Exception saveException)
            {
                Log.AiGlobalShortlistRunFailedToPersistFailedStatus(_logger, saveException);
            }

            return AiGlobalShortlistRunResult.Failed(
                $"AI global shortlist run failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}",
                StatusCodes.Status500InternalServerError,
                run.Id,
                run.CandidateCount,
                run.ProcessedCount,
                run.ShortlistedCount,
                run.NeedsReviewCount,
                run.FailedCount);
        }
    }

    public async Task<AiGlobalShortlistRunResult> RequestCancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var run = await dbContext.AiGlobalShortlistRuns
            .SingleOrDefaultAsync(candidate => candidate.Id == runId && candidate.AppUserId == userId, cancellationToken);

        if (run is null)
        {
            return AiGlobalShortlistRunResult.Failed(
                "AI global shortlist run was not found.",
                StatusCodes.Status404NotFound,
                runId);
        }

        if (run.Status is RunStatusCompleted or RunStatusFailed or RunStatusCancelled)
        {
            var terminalMessage = run.Status switch
            {
                RunStatusCancelled => "AI global shortlist run was already cancelled.",
                RunStatusFailed => "AI global shortlist run has already failed.",
                _ => "AI global shortlist run is already completed."
            };

            return AiGlobalShortlistRunResult.Succeeded(
                run.Id,
                run.CandidateCount,
                run.ProcessedCount,
                run.ShortlistedCount,
                run.NeedsReviewCount,
                run.FailedCount,
                terminalMessage);
        }

        run.CancellationRequestedAtUtc = DateTimeOffset.UtcNow;
        run.Summary = LimitText("Cancellation requested. Run will stop at the next checkpoint.", 2000);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishProgressSafeAsync(
            userId,
            connectionId: null,
            new AiGlobalShortlistProgressUpdate(
                run.Id,
                "running",
                "cancel-requested",
                "Cancellation requested. Run will stop at the next checkpoint.",
                CandidateCount: run.CandidateCount,
                ProcessedCount: run.ProcessedCount,
                AcceptedCount: run.ShortlistedCount,
                NeedsReviewCount: run.NeedsReviewCount,
                FailedCount: run.FailedCount),
            cancellationToken);

        return AiGlobalShortlistRunResult.Succeeded(
            run.Id,
            run.CandidateCount,
            run.ProcessedCount,
            run.ShortlistedCount,
            run.NeedsReviewCount,
            run.FailedCount,
            "Cancellation requested. Run will stop at the next checkpoint.");
    }

    public async Task<AiGlobalShortlistRunSnapshot?> GetLatestRunAsync(CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await RecoverOrphanedActiveRunsAsync(dbContext, userId, cancellationToken);

        var latestRunId = await dbContext.AiGlobalShortlistRuns
            .AsNoTracking()
            .Where(run => run.AppUserId == userId)
            .OrderByDescending(static run => run.CreatedAtUtc)
            .Select(static run => (Guid?)run.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!latestRunId.HasValue)
        {
            return null;
        }

        return await GetRunAsync(latestRunId.Value, cancellationToken);
    }

    public async Task<AiGlobalShortlistRunSnapshot?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await RecoverOrphanedActiveRunsAsync(dbContext, userId, cancellationToken);

        var run = await dbContext.AiGlobalShortlistRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == runId && candidate.AppUserId == userId, cancellationToken);

        if (run is null)
        {
            return null;
        }

        var items = await dbContext.AiGlobalShortlistItems
            .AsNoTracking()
            .Where(item => item.RunId == runId && item.Run.AppUserId == userId)
            .OrderBy(static item => item.Rank)
            .Select(
                item => new AiGlobalShortlistItemSnapshot(
                    item.JobRecordId,
                    item.JobRecord.LinkedInJobId,
                    item.JobRecord.Title,
                    item.JobRecord.CompanyName,
                    item.JobRecord.LocationName,
                    item.JobRecord.ListedAtUtc,
                    item.Rank,
                    item.Decision,
                    item.CreatedAtUtc,
                    item.PromptVersion,
                    item.ModelName,
                    item.LatencyMilliseconds,
                    item.InputTokenCount,
                    item.OutputTokenCount,
                    item.TotalTokenCount,
                    item.ErrorCode,
                    item.Score,
                    item.Confidence,
                    item.RecommendationReason,
                    item.Concerns))
            .ToListAsync(cancellationToken);

        return new AiGlobalShortlistRunSnapshot(
            run.Id,
            run.Status,
            run.CreatedAtUtc,
            run.CompletedAtUtc,
            run.CancellationRequestedAtUtc,
            run.CandidateCount,
            run.ProcessedCount,
            run.ShortlistedCount,
            run.NeedsReviewCount,
            run.FailedCount,
            run.NextSequenceNumber,
            run.ModelName,
            run.Summary,
            items);
    }

    public async Task<AiGlobalShortlistQueueOverviewSnapshot> GetQueueOverviewAsync(CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var eligibleJobs = dbContext.Jobs
            .AsNoTracking()
            .Where(
                job =>
                    job.AppUserId == userId &&
                    !string.IsNullOrWhiteSpace(job.Description) &&
                    job.LastDetailSyncedAtUtc.HasValue);

        var eligibleTotal = await eligibleJobs.CountAsync(cancellationToken);

        var alreadyReviewed = await dbContext.AiGlobalShortlistItems
            .AsNoTracking()
            .Where(item => item.Run.AppUserId == userId)
            .Select(static item => item.JobRecordId)
            .Distinct()
            .Join(
                eligibleJobs.Select(static job => job.Id),
                static reviewedJobId => reviewedJobId,
                static eligibleJobId => eligibleJobId,
                static (reviewedJobId, _) => reviewedJobId)
            .CountAsync(cancellationToken);

        return new AiGlobalShortlistQueueOverviewSnapshot(
            eligibleTotal,
            alreadyReviewed,
            Math.Max(eligibleTotal - alreadyReviewed, 0));
    }

    private async Task RecoverOrphanedActiveRunsAsync(
        LinkedInJobScraperDbContext dbContext,
        int userId,
        CancellationToken cancellationToken)
    {
        var activeRuns = await dbContext.AiGlobalShortlistRuns
            .Where(run => run.AppUserId == userId)
            .Where(run => run.Status == RunStatusPending || run.Status == RunStatusRunning)
            .ToListAsync(cancellationToken);

        if (activeRuns.Count == 0)
        {
            return;
        }

        var recoveredCount = 0;
        var recoveredAtUtc = DateTimeOffset.UtcNow;

        foreach (var run in activeRuns)
        {
            var batch = _progressStateStore.GetBatch(userId, run.Id, afterSequence: 0);
            if (batch.RunFound)
            {
                continue;
            }

            run.Status = RunStatusCancelled;
            run.CompletedAtUtc = recoveredAtUtc;
            run.CancellationRequestedAtUtc ??= recoveredAtUtc;
            run.Summary = LimitText(
                "Recovered after application restart: previous active run was marked as cancelled.",
                2000);
            recoveredCount++;
        }

        if (recoveredCount == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Log.AiGlobalShortlistRecoveredOrphanedRuns(_logger, recoveredCount);
    }

    private async Task<CandidateEvaluation> EvaluateCandidateAsync(
        int userId,
        Guid runId,
        int sequenceNumber,
        JobRecord job,
        AiBehaviorProfile behaviorProfile,
        int acceptedThreshold,
        int rejectedThreshold,
        bool allowFallback,
        int transientRetryAttempts,
        TimeSpan transientRetryBaseDelay,
        string? progressConnectionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.Description) ||
            !job.LastDetailSyncedAtUtc.HasValue)
        {
            return new CandidateEvaluation(
                DecisionNeedsReview,
                null,
                null,
                "Candidate skipped because enriched job details are missing.",
                "Refresh job details and retry this run.",
                "MISSING_DETAILS",
                null,
                null,
                null,
                null,
                _openAiSecurityOptions.Value.Model,
                false);
        }

        var gatewayRequest = new AiGlobalShortlistBatchGatewayRequest(
            [
                new AiGlobalShortlistBatchCandidate(
                    job.Id.ToString("N"),
                    job.LinkedInJobId,
                    job.Title,
                    job.Description,
                    job.CompanyName,
                    job.LocationName,
                    job.EmploymentStatus,
                    job.ListedAtUtc,
                    job.LinkedInUpdatedAtUtc,
                    job.AiScore,
                    job.AiLabel)
            ],
            behaviorProfile.BehavioralInstructions,
            behaviorProfile.PrioritySignals,
            behaviorProfile.ExclusionSignals,
            behaviorProfile.OutputLanguageCode,
            1);

        var attempt = 0;
        AiGlobalShortlistBatchGatewayResult gatewayResult;
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            gatewayResult = await _globalShortlistGateway.RankBatchAsync(gatewayRequest, cancellationToken);
            if (!IsTransientGatewayFailure(gatewayResult) ||
                attempt >= transientRetryAttempts)
            {
                break;
            }

            var nextAttempt = attempt + 1;
            var delay = GetRetryDelay(transientRetryBaseDelay, nextAttempt);

            await PublishProgressSafeAsync(
                userId,
                progressConnectionId,
                new AiGlobalShortlistProgressUpdate(
                    runId,
                    "running",
                    "candidate-retry",
                    $"Transient failure for candidate {sequenceNumber}. Scheduling retry {nextAttempt}/{transientRetryAttempts}.",
                    SequenceNumber: sequenceNumber,
                    JobId: job.Id,
                    LinkedInJobId: job.LinkedInJobId,
                    ErrorCode: gatewayResult.ErrorCode),
                cancellationToken);

            attempt = nextAttempt;
            await Task.Delay(delay, cancellationToken);
        }

        stopwatch.Stop();

        if (gatewayResult.CanRank &&
            gatewayResult.Recommendations is { Count: > 0 })
        {
            var recommendation = gatewayResult.Recommendations
                .FirstOrDefault(
                    recommendation =>
                        recommendation.CandidateId == gatewayRequest.Candidates[0].CandidateId);

            if (recommendation is not null)
            {
                var score = Math.Clamp(recommendation.Score, 0, 100);
                var confidence = Math.Clamp(recommendation.Confidence, 0, 100);

                return new CandidateEvaluation(
                    ClassifyDecision(score, acceptedThreshold, rejectedThreshold),
                    score,
                    confidence,
                    recommendation.RecommendationReason,
                    recommendation.Concerns,
                    null,
                    ToMilliseconds(stopwatch.Elapsed),
                    gatewayResult.InputTokenCount,
                    gatewayResult.OutputTokenCount,
                    gatewayResult.TotalTokenCount,
                    gatewayResult.ModelName,
                    false);
            }
        }

        if (allowFallback)
        {
            var fallbackResult = await _jobScoringGateway.ScoreAsync(
                BuildFallbackRequest(
                    new CandidateJob(
                        job.Id,
                        job.Id.ToString("N"),
                        job.LinkedInJobId,
                        job.Title,
                        job.Description,
                        job.CompanyName,
                        job.LocationName,
                        job.EmploymentStatus,
                        job.ListedAtUtc,
                        job.LinkedInUpdatedAtUtc,
                        job.AiScore,
                        job.AiLabel),
                    behaviorProfile),
                cancellationToken);

            if (fallbackResult.CanScore && fallbackResult.Score.HasValue)
            {
                var score = Math.Clamp(fallbackResult.Score.Value, 0, 100);
                return new CandidateEvaluation(
                    ClassifyDecision(score, acceptedThreshold, rejectedThreshold),
                    score,
                    score,
                    fallbackResult.WhyMatched ?? fallbackResult.Summary ?? "Fallback recommendation.",
                    fallbackResult.Concerns ?? string.Empty,
                    "FALLBACK_SCORING",
                    null,
                    null,
                    null,
                    null,
                    _openAiSecurityOptions.Value.Model,
                    true);
            }
        }

        return new CandidateEvaluation(
            DecisionNeedsReview,
            null,
            null,
            "AI did not produce a usable recommendation for this candidate.",
            gatewayResult.Message,
            gatewayResult.ErrorCode ?? "NO_RECOMMENDATION",
            ToMilliseconds(stopwatch.Elapsed),
            gatewayResult.InputTokenCount,
            gatewayResult.OutputTokenCount,
            gatewayResult.TotalTokenCount,
            gatewayResult.ModelName,
            false);
    }

    private static async Task MarkRunCancelledAsync(
        LinkedInJobScraperDbContext dbContext,
        AiGlobalShortlistRunRecord run,
        CancellationToken cancellationToken)
    {
        run.Status = RunStatusCancelled;
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.Summary = LimitText(
            $"Run cancelled at checkpoint {run.ProcessedCount}/{run.CandidateCount}.",
            2000);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task TryMarkRunCancelledAsync(Guid runId, int userId)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);

            var run = await dbContext.AiGlobalShortlistRuns
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == runId && candidate.AppUserId == userId,
                    CancellationToken.None);

            if (run is null ||
                run.Status is RunStatusCompleted or RunStatusCancelled)
            {
                return;
            }

            run.Status = RunStatusCancelled;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.Summary = LimitText(
                $"Run cancelled at checkpoint {run.ProcessedCount}/{run.CandidateCount}.",
                2000);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            Log.AiGlobalShortlistRunFailedToPersistFailedStatus(_logger, exception);
        }
    }

    private static string ClassifyDecision(
        int score,
        int acceptedThreshold,
        int rejectedThreshold)
    {
        if (score >= acceptedThreshold)
        {
            return DecisionAccepted;
        }

        if (score <= rejectedThreshold)
        {
            return DecisionRejected;
        }

        return DecisionNeedsReview;
    }

    private static int? ToMilliseconds(TimeSpan elapsed)
    {
        var milliseconds = elapsed.TotalMilliseconds;
        if (milliseconds < 0)
        {
            return null;
        }

        return milliseconds > int.MaxValue
            ? int.MaxValue
            : (int)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
    }

    private static bool IsTransientGatewayFailure(AiGlobalShortlistBatchGatewayResult gatewayResult)
    {
        if (gatewayResult.CanRank)
        {
            return false;
        }

        if (gatewayResult.StatusCode is StatusCodes.Status408RequestTimeout or
            StatusCodes.Status429TooManyRequests)
        {
            return true;
        }

        if (gatewayResult.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return true;
        }

        return string.Equals(gatewayResult.ErrorCode, "TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
               (gatewayResult.ErrorCode?.StartsWith("HTTP_5", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static TimeSpan GetRetryDelay(TimeSpan baseDelay, int retryAttempt)
    {
        if (baseDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var scale = Math.Pow(2, Math.Max(0, retryAttempt - 1));
        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * scale);
        return delay > TimeSpan.FromSeconds(15)
            ? TimeSpan.FromSeconds(15)
            : delay;
    }

    private async Task PublishProgressSafeAsync(
        int userId,
        string? connectionId,
        AiGlobalShortlistProgressUpdate update,
        CancellationToken cancellationToken)
    {
        try
        {
            await _progressNotifier.PublishAsync(userId, connectionId, update, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.AiGlobalShortlistProgressPublishFailed(_logger, update.RunId, exception);
        }
    }

    private static JobScoringGatewayRequest BuildFallbackRequest(
        CandidateJob candidate,
        AiBehaviorProfile behaviorProfile)
    {
        return new JobScoringGatewayRequest(
            candidate.Title,
            candidate.Description,
            behaviorProfile.BehavioralInstructions,
            behaviorProfile.PrioritySignals,
            behaviorProfile.ExclusionSignals,
            behaviorProfile.OutputLanguageCode,
            candidate.CompanyName,
            candidate.LocationName,
            candidate.EmploymentStatus);
    }

    private static Task<List<CandidateJob>> SelectCandidatesAsync(
        LinkedInJobScraperDbContext dbContext,
        int userId,
        int? maxCandidateCount,
        CancellationToken cancellationToken)
    {
        var previouslyReviewedJobIds = dbContext.AiGlobalShortlistItems
            .Where(item => item.Run.AppUserId == userId)
            .Select(static item => item.JobRecordId);

        IQueryable<JobRecord> query = dbContext.Jobs
            .AsNoTracking()
            .Where(
                job =>
                    job.AppUserId == userId &&
                    !string.IsNullOrWhiteSpace(job.Description) &&
                    job.LastDetailSyncedAtUtc.HasValue &&
                    !previouslyReviewedJobIds.Contains(job.Id))
            .OrderByDescending(static job => job.LinkedInUpdatedAtUtc ?? job.ListedAtUtc ?? job.LastSeenAtUtc)
            .ThenByDescending(static job => job.LastSeenAtUtc)
            .ThenBy(static job => job.LinkedInJobId);

        if (maxCandidateCount is > 0)
        {
            query = query.Take(maxCandidateCount.Value);
        }

        return query
            .Select(
                static job =>
                    new CandidateJob(
                        job.Id,
                        job.Id.ToString("N"),
                        job.LinkedInJobId,
                        job.Title,
                        job.Description ?? string.Empty,
                        job.CompanyName,
                        job.LocationName,
                        job.EmploymentStatus,
                        job.ListedAtUtc,
                        job.LinkedInUpdatedAtUtc,
                        job.AiScore,
                        job.AiLabel))
            .ToListAsync(cancellationToken);
    }

    private sealed record CandidateJob(
        Guid JobId,
        string CandidateId,
        string LinkedInJobId,
        string Title,
        string Description,
        string? CompanyName,
        string? LocationName,
        string? EmploymentStatus,
        DateTimeOffset? ListedAtUtc,
        DateTimeOffset? LinkedInUpdatedAtUtc,
        int? ExistingAiScore,
        string? ExistingAiLabel);

    private sealed record CandidateEvaluation(
        string Decision,
        int? Score,
        int? Confidence,
        string RecommendationReason,
        string Concerns,
        string? ErrorCode,
        int? LatencyMilliseconds,
        int? InputTokenCount,
        int? OutputTokenCount,
        int? TotalTokenCount,
        string? ModelName,
        bool UsedFallback);

    private static string? LimitText(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 4012,
        Level = LogLevel.Error,
        Message = "AI global shortlist run failed unexpectedly.")]
    public static partial void AiGlobalShortlistRunFailedUnexpectedly(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 4013,
        Level = LogLevel.Error,
        Message = "Failed to persist failed AI global shortlist run status.")]
    public static partial void AiGlobalShortlistRunFailedToPersistFailedStatus(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 4014,
        Level = LogLevel.Warning,
        Message = "Failed to publish AI global shortlist progress event for run {RunId}.")]
    public static partial void AiGlobalShortlistProgressPublishFailed(
        ILogger logger,
        Guid runId,
        Exception exception);

    [LoggerMessage(
        EventId = 4015,
        Level = LogLevel.Information,
        Message = "Recovered {RecoveredCount} orphaned AI live review run(s) after restart.")]
    public static partial void AiGlobalShortlistRecoveredOrphanedRuns(
        ILogger logger,
        int recoveredCount);
}
