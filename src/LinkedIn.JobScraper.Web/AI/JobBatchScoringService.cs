using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class JobBatchScoringService : IJobBatchScoringService
{
    private const string OpenAiUnavailableSupportMessage =
        "AI scoring is currently unavailable (OpenAI configuration/connection is not ready). Please contact support.";
    private const string OpenAiAuthenticationSupportMessage =
        "AI scoring is currently unavailable (OpenAI authentication failed). Please contact support.";
    private const string OpenAiRateLimitedSupportMessage =
        "AI scoring is temporarily unavailable (OpenAI rate limit). Please retry shortly or contact support.";
    private const string OpenAiServiceUnavailableSupportMessage =
        "AI scoring is currently unavailable (OpenAI service issue). Please contact support.";
    private const string OpenAiTimeoutSupportMessage =
        "AI scoring timed out while contacting OpenAI. Please retry in a few moments or contact support.";
    private const string OpenAiUpstreamResponseSupportMessage =
        "AI scoring could not be completed due to an unexpected OpenAI response. Please retry or contact support.";

    private readonly IAiBehaviorSettingsService _behaviorSettingsService;
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IJobScoringGateway _jobScoringGateway;
    private readonly ILogger<JobBatchScoringService> _logger;
    private readonly IOpenAiEffectiveSecurityOptionsResolver _openAiSecurityOptionsResolver;

    public JobBatchScoringService(
        ICurrentAppUserContext currentAppUserContext,
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobScoringGateway jobScoringGateway,
        IAiBehaviorSettingsService behaviorSettingsService,
        IOpenAiEffectiveSecurityOptionsResolver openAiSecurityOptionsResolver,
        ILogger<JobBatchScoringService> logger)
    {
        _currentAppUserContext = currentAppUserContext;
        _dbContextFactory = dbContextFactory;
        _jobScoringGateway = jobScoringGateway;
        _behaviorSettingsService = behaviorSettingsService;
        _openAiSecurityOptionsResolver = openAiSecurityOptionsResolver;
        _logger = logger;
    }

    public async Task<JobBatchScoringResult> ScoreReadyJobsAsync(
        int maxCount,
        CancellationToken cancellationToken,
        JobStageProgressCallback? progressCallback = null)
    {
        if (maxCount <= 0)
        {
            maxCount = 1;
        }

        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var behaviorProfile = await _behaviorSettingsService.GetActiveAsync(cancellationToken);

        var jobsToScore = await dbContext.Jobs
            .Where(
                job =>
                    job.AppUserId == userId &&
                    !string.IsNullOrWhiteSpace(job.Description) &&
                    job.AiScore == null)
            .OrderByDescending(static job => job.LastSeenAtUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        if (jobsToScore.Count == 0)
        {
            return JobBatchScoringResult.Succeeded(maxCount, 0, 0, 0);
        }

        var scoringOptions = await _openAiSecurityOptionsResolver.ResolveAsync(cancellationToken);
        var concurrencyValidationError = scoringOptions.ValidateScoringConcurrency();

        if (concurrencyValidationError is not null)
        {
            return JobBatchScoringResult.Failed(
                concurrencyValidationError,
                StatusCodes.Status500InternalServerError,
                jobsToScore.Count,
                0,
                0,
                0);
        }

        var maxConcurrency = Math.Min(scoringOptions.MaxConcurrentScoringRequests, jobsToScore.Count);
        var scoringWorkItems = jobsToScore
            .Select(
                job =>
                    new PendingScoringJob(
                        job,
                        string.IsNullOrWhiteSpace(job.Title) ? job.Id.ToString("N") : job.Title,
                        BuildGatewayRequest(job, behaviorProfile)))
            .ToArray();

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Queued {jobsToScore.Count} jobs for AI scoring with up to {maxConcurrency} concurrent request(s).",
                    jobsToScore.Count,
                    0,
                    0,
                    0),
                cancellationToken);
        }

        var processedCount = 0;
        var scoredCount = 0;
        var failedCount = 0;
        string? firstFailureMessage = null;
        int? firstFailureStatusCode = null;
        var originalAutoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        using var concurrencyGate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        try
        {
            var allScoringTasks = scoringWorkItems
                .Select(workItem => ScoreJobAsync(workItem, concurrencyGate, cancellationToken))
                .ToArray();
            var pendingScoringTasks = allScoringTasks.ToList();
            var completedScoringResults = new List<CompletedScoringJob>(scoringWorkItems.Length);

            try
            {
                while (pendingScoringTasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(pendingScoringTasks);
                    pendingScoringTasks.Remove(completedTask);

                    var completedScoringJob = await completedTask;
                    completedScoringResults.Add(completedScoringJob);
                    processedCount++;

                    if (!completedScoringJob.Result.CanScore ||
                        !completedScoringJob.Result.Score.HasValue ||
                        string.IsNullOrWhiteSpace(completedScoringJob.Result.Label))
                    {
                        failedCount++;
                        firstFailureMessage ??= completedScoringJob.Result.Message;
                        firstFailureStatusCode ??= completedScoringJob.Result.StatusCode ?? StatusCodes.Status502BadGateway;

                        if (progressCallback is not null)
                        {
                            await progressCallback(
                                new JobStageProgress(
                                    $"Scoring {processedCount}/{jobsToScore.Count} failed for '{completedScoringJob.WorkItem.DisplayTitle}': {completedScoringJob.Result.Message}",
                                    jobsToScore.Count,
                                    processedCount,
                                    scoredCount,
                                    failedCount),
                                cancellationToken);
                        }

                        continue;
                    }

                    scoredCount++;

                    if (progressCallback is not null)
                    {
                        await progressCallback(
                            new JobStageProgress(
                                $"Scoring {processedCount}/{jobsToScore.Count} completed for '{completedScoringJob.WorkItem.DisplayTitle}'.",
                                jobsToScore.Count,
                                processedCount,
                                scoredCount,
                                failedCount),
                            cancellationToken);
                    }
                }
            }
            catch
            {
                await ObserveRemainingTasksAsync(allScoringTasks);
                throw;
            }

            if (scoredCount == 0 && failedCount > 0)
            {
                return JobBatchScoringResult.Failed(
                    firstFailureMessage ?? "No jobs were scored.",
                    firstFailureStatusCode ?? StatusCodes.Status502BadGateway,
                    maxCount,
                    processedCount,
                    0,
                    failedCount);
            }

            foreach (var completedScoringJob in completedScoringResults)
            {
                if (!completedScoringJob.Result.CanScore ||
                    !completedScoringJob.Result.Score.HasValue ||
                    string.IsNullOrWhiteSpace(completedScoringJob.Result.Label))
                {
                    continue;
                }

                ApplyScoringResult(
                    completedScoringJob.WorkItem.Job,
                    completedScoringJob.Result,
                    DateTimeOffset.UtcNow);
            }

            if (progressCallback is not null)
            {
                await progressCallback(
                    new JobStageProgress(
                        $"Saving AI scoring results for {scoredCount} job(s).",
                        jobsToScore.Count,
                        processedCount,
                        scoredCount,
                        failedCount),
                    cancellationToken);
            }

            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
        }

        return JobBatchScoringResult.Succeeded(maxCount, processedCount, scoredCount, failedCount);
    }

    public async Task<SingleJobScoringResult> ScoreJobAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.Jobs.SingleOrDefaultAsync(
            candidate => candidate.Id == jobId && candidate.AppUserId == userId,
            cancellationToken);

        if (job is null)
        {
            return SingleJobScoringResult.Failed(
                "Job was not found.",
                StatusCodes.Status404NotFound);
        }

        if (job.AiScore.HasValue || job.LastScoredAtUtc.HasValue)
        {
            return SingleJobScoringResult.Failed(
                "This job was already scored and cannot be scored again.",
                StatusCodes.Status409Conflict,
                TryCreateSnapshot(job, outputLanguageCode: null));
        }

        if (string.IsNullOrWhiteSpace(job.Description))
        {
            return SingleJobScoringResult.Failed(
                "This job is not ready for AI scoring yet because the LinkedIn detail enrichment is incomplete.",
                StatusCodes.Status409Conflict);
        }

        Log.SingleJobScoringStarted(_logger, userId, job.Id);
        var stopwatch = Stopwatch.StartNew();
        var behaviorProfile = await _behaviorSettingsService.GetActiveAsync(cancellationToken);
        var result = await _jobScoringGateway.ScoreAsync(BuildGatewayRequest(job, behaviorProfile), cancellationToken);
        stopwatch.Stop();

        if (!result.CanScore ||
            !result.Score.HasValue ||
            string.IsNullOrWhiteSpace(result.Label))
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status502BadGateway;
            var failure = NormalizeSingleJobScoreFailureMessage(result.Message, statusCode);
            Log.SingleJobScoringFailed(
                _logger,
                userId,
                job.Id,
                failure.Category,
                statusCode,
                (int)stopwatch.ElapsedMilliseconds,
                LimitForLog(result.Message));

            return SingleJobScoringResult.Failed(
                failure.UserMessage,
                statusCode);
        }

        var scoredAtUtc = DateTimeOffset.UtcNow;
        ApplyScoringResult(job, result, scoredAtUtc);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbContext.Entry(job).ReloadAsync(cancellationToken);

            if (job.AiScore.HasValue || job.LastScoredAtUtc.HasValue)
            {
                return SingleJobScoringResult.Failed(
                    "This job was already scored by another request.",
                    StatusCodes.Status409Conflict,
                    TryCreateSnapshot(job, behaviorProfile.OutputLanguageCode));
            }

            return SingleJobScoringResult.Failed(
                "The job changed while AI scoring was being saved. Refresh and try again.",
                StatusCodes.Status409Conflict);
        }

        Log.SingleJobScoringCompleted(
            _logger,
            userId,
            job.Id,
            result.Score.Value,
            result.Label,
            (int)stopwatch.ElapsedMilliseconds);

        return SingleJobScoringResult.Succeeded(
            new SingleJobScoringSnapshot(
                job.Id,
                result.Score.Value,
                result.Label,
                result.Summary,
                result.WhyMatched,
                result.Concerns,
                scoredAtUtc,
                AiOutputLanguage.Normalize(behaviorProfile.OutputLanguageCode)));
    }

    private async Task<CompletedScoringJob> ScoreJobAsync(
        PendingScoringJob workItem,
        SemaphoreSlim concurrencyGate,
        CancellationToken cancellationToken)
    {
        await concurrencyGate.WaitAsync(cancellationToken);

        try
        {
            var result = await _jobScoringGateway.ScoreAsync(workItem.Request, cancellationToken);
            return new CompletedScoringJob(workItem, result);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    private static async Task ObserveRemainingTasksAsync(IEnumerable<Task<CompletedScoringJob>> tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
        }
    }

    private static JobScoringGatewayRequest BuildGatewayRequest(
        JobRecord job,
        AiBehaviorProfile behaviorProfile)
    {
        return new JobScoringGatewayRequest(
            job.Title,
            job.Description!,
            behaviorProfile.BehavioralInstructions,
            behaviorProfile.PrioritySignals,
            behaviorProfile.ExclusionSignals,
            behaviorProfile.OutputLanguageCode,
            job.CompanyName,
            job.LocationName,
            job.EmploymentStatus);
    }

    private static void ApplyScoringResult(
        JobRecord job,
        JobScoringGatewayResult result,
        DateTimeOffset scoredAtUtc)
    {
        job.AiScore = result.Score!.Value;
        job.AiLabel = result.Label;
        job.AiSummary = result.Summary;
        job.AiWhyMatched = result.WhyMatched;
        job.AiConcerns = result.Concerns;
        job.LastScoredAtUtc = scoredAtUtc;
    }

    private static SingleJobScoringSnapshot? TryCreateSnapshot(
        JobRecord job,
        string? outputLanguageCode)
    {
        if (!job.AiScore.HasValue ||
            string.IsNullOrWhiteSpace(job.AiLabel) ||
            !job.LastScoredAtUtc.HasValue)
        {
            return null;
        }

        return new SingleJobScoringSnapshot(
            job.Id,
            job.AiScore.Value,
            job.AiLabel,
            job.AiSummary,
            job.AiWhyMatched,
            job.AiConcerns,
            job.LastScoredAtUtc.Value,
            AiOutputLanguage.Normalize(outputLanguageCode));
    }

    private static ScoringFailureDescriptor NormalizeSingleJobScoreFailureMessage(string message, int statusCode)
    {
        var normalized = message.Trim();
        var lowerInvariant = normalized.ToLowerInvariant();

        if (statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden ||
            lowerInvariant.Contains("invalid_request_error", StringComparison.Ordinal) ||
            lowerInvariant.Contains("unauthorized", StringComparison.Ordinal) ||
            lowerInvariant.Contains("forbidden", StringComparison.Ordinal))
        {
            return new ScoringFailureDescriptor("authentication", OpenAiAuthenticationSupportMessage);
        }

        if (statusCode == StatusCodes.Status429TooManyRequests ||
            lowerInvariant.Contains("rate-limited", StringComparison.Ordinal) ||
            lowerInvariant.Contains("rate limit", StringComparison.Ordinal))
        {
            return new ScoringFailureDescriptor("rate-limit", OpenAiRateLimitedSupportMessage);
        }

        if (lowerInvariant.Contains("timed out", StringComparison.Ordinal) ||
            lowerInvariant.Contains("timeout", StringComparison.Ordinal))
        {
            return new ScoringFailureDescriptor("timeout", OpenAiTimeoutSupportMessage);
        }

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return new ScoringFailureDescriptor("service-unavailable", OpenAiServiceUnavailableSupportMessage);
        }

        if (lowerInvariant.Contains("api key is not configured", StringComparison.Ordinal) ||
            lowerInvariant.Contains("model is not configured", StringComparison.Ordinal) ||
            lowerInvariant.Contains("configuration", StringComparison.Ordinal))
        {
            return new ScoringFailureDescriptor("configuration", OpenAiUnavailableSupportMessage);
        }

        if (normalized.Length > 220 || normalized.Contains("********", StringComparison.Ordinal))
        {
            return new ScoringFailureDescriptor("sanitized-upstream", OpenAiUnavailableSupportMessage);
        }

        return new ScoringFailureDescriptor("upstream-message", OpenAiUpstreamResponseSupportMessage);
    }

    private static string LimitForLog(string message)
    {
        var normalized = message.Trim();
        if (normalized.Length <= 1000)
        {
            return normalized;
        }

        return normalized[..1000];
    }

    private sealed record ScoringFailureDescriptor(
        string Category,
        string UserMessage);

    private sealed record PendingScoringJob(
        JobRecord Job,
        string DisplayTitle,
        JobScoringGatewayRequest Request);

    private sealed record CompletedScoringJob(
        PendingScoringJob WorkItem,
        JobScoringGatewayResult Result);
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 4020,
        Level = LogLevel.Information,
        Message = "Single-job AI scoring started. UserId={UserId}, JobId={JobId}")]
    public static partial void SingleJobScoringStarted(
        ILogger logger,
        int userId,
        Guid jobId);

    [LoggerMessage(
        EventId = 4021,
        Level = LogLevel.Warning,
        Message = "Single-job AI scoring failed. UserId={UserId}, JobId={JobId}, Category={Category}, StatusCode={StatusCode}, DurationMs={DurationMs}, RawMessage={RawMessage}")]
    public static partial void SingleJobScoringFailed(
        ILogger logger,
        int userId,
        Guid jobId,
        string category,
        int statusCode,
        int durationMs,
        string rawMessage);

    [LoggerMessage(
        EventId = 4022,
        Level = LogLevel.Information,
        Message = "Single-job AI scoring completed. UserId={UserId}, JobId={JobId}, Score={Score}, Label={Label}, DurationMs={DurationMs}")]
    public static partial void SingleJobScoringCompleted(
        ILogger logger,
        int userId,
        Guid jobId,
        int score,
        string label,
        int durationMs);
}
