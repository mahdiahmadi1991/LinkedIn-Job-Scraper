using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class JobBatchScoringService : IJobBatchScoringService
{
    private readonly IAiBehaviorSettingsService _behaviorSettingsService;
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IJobScoringGateway _jobScoringGateway;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;

    public JobBatchScoringService(
        ICurrentAppUserContext currentAppUserContext,
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobScoringGateway jobScoringGateway,
        IAiBehaviorSettingsService behaviorSettingsService,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions)
    {
        _currentAppUserContext = currentAppUserContext;
        _dbContextFactory = dbContextFactory;
        _jobScoringGateway = jobScoringGateway;
        _behaviorSettingsService = behaviorSettingsService;
        _openAiSecurityOptions = openAiSecurityOptions;
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

        var scoringOptions = _openAiSecurityOptions.Value;
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

        var behaviorProfile = await _behaviorSettingsService.GetActiveAsync(cancellationToken);
        var result = await _jobScoringGateway.ScoreAsync(BuildGatewayRequest(job, behaviorProfile), cancellationToken);

        if (!result.CanScore ||
            !result.Score.HasValue ||
            string.IsNullOrWhiteSpace(result.Label))
        {
            return SingleJobScoringResult.Failed(
                result.Message,
                result.StatusCode ?? StatusCodes.Status502BadGateway);
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

    private sealed record PendingScoringJob(
        JobRecord Job,
        string DisplayTitle,
        JobScoringGatewayRequest Request);

    private sealed record CompletedScoringJob(
        PendingScoringJob WorkItem,
        JobScoringGatewayResult Result);
}
