using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class JobBatchScoringService : IJobBatchScoringService
{
    private readonly IAiBehaviorSettingsService _behaviorSettingsService;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IJobScoringGateway _jobScoringGateway;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;

    public JobBatchScoringService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobScoringGateway jobScoringGateway,
        IAiBehaviorSettingsService behaviorSettingsService,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions)
    {
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

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var behaviorProfile = await _behaviorSettingsService.GetActiveAsync(cancellationToken);

        var jobsToScore = await dbContext.Jobs
            .Where(
                static job =>
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
                        new JobScoringGatewayRequest(
                            job.Title,
                            job.Description!,
                            behaviorProfile.BehavioralInstructions,
                            behaviorProfile.PrioritySignals,
                            behaviorProfile.ExclusionSignals,
                            behaviorProfile.OutputLanguageCode,
                            job.CompanyName,
                            job.LocationName,
                            job.EmploymentStatus)))
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

                completedScoringJob.WorkItem.Job.AiScore = completedScoringJob.Result.Score.Value;
                completedScoringJob.WorkItem.Job.AiLabel = completedScoringJob.Result.Label;
                completedScoringJob.WorkItem.Job.AiSummary = completedScoringJob.Result.Summary;
                completedScoringJob.WorkItem.Job.AiWhyMatched = completedScoringJob.Result.WhyMatched;
                completedScoringJob.WorkItem.Job.AiConcerns = completedScoringJob.Result.Concerns;
                completedScoringJob.WorkItem.Job.LastScoredAtUtc = DateTimeOffset.UtcNow;
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

    private sealed record PendingScoringJob(
        JobRecord Job,
        string DisplayTitle,
        JobScoringGatewayRequest Request);

    private sealed record CompletedScoringJob(
        PendingScoringJob WorkItem,
        JobScoringGatewayResult Result);
}
