using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class JobBatchScoringService : IJobBatchScoringService
{
    private readonly IAiBehaviorSettingsService _behaviorSettingsService;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IJobScoringGateway _jobScoringGateway;

    public JobBatchScoringService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobScoringGateway jobScoringGateway,
        IAiBehaviorSettingsService behaviorSettingsService)
    {
        _dbContextFactory = dbContextFactory;
        _jobScoringGateway = jobScoringGateway;
        _behaviorSettingsService = behaviorSettingsService;
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

        if (progressCallback is not null)
        {
            await progressCallback(
                new JobStageProgress(
                    $"Queued {jobsToScore.Count} jobs for AI scoring.",
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

        try
        {
            foreach (var job in jobsToScore)
            {
                processedCount++;
                var displayTitle = string.IsNullOrWhiteSpace(job.Title) ? job.Id.ToString("N") : job.Title;

                var result = await _jobScoringGateway.ScoreAsync(
                    new JobScoringGatewayRequest(
                        job.Title,
                        job.Description!,
                        behaviorProfile.BehavioralInstructions,
                        behaviorProfile.PrioritySignals,
                        behaviorProfile.ExclusionSignals,
                        behaviorProfile.OutputLanguageCode,
                        job.CompanyName,
                        job.LocationName,
                        job.EmploymentStatus),
                    cancellationToken);

                if (!result.CanScore ||
                    !result.Score.HasValue ||
                    string.IsNullOrWhiteSpace(result.Label))
                {
                    failedCount++;
                    firstFailureMessage ??= result.Message;
                    firstFailureStatusCode ??= result.StatusCode ?? StatusCodes.Status502BadGateway;

                    if (progressCallback is not null)
                    {
                        await progressCallback(
                            new JobStageProgress(
                                $"Scoring {processedCount}/{jobsToScore.Count} failed for '{displayTitle}': {result.Message}",
                                jobsToScore.Count,
                                processedCount,
                                scoredCount,
                                failedCount),
                            cancellationToken);
                    }

                    continue;
                }

                job.AiScore = result.Score.Value;
                job.AiLabel = result.Label;
                job.AiSummary = result.Summary;
                job.AiWhyMatched = result.WhyMatched;
                job.AiConcerns = result.Concerns;
                job.LastScoredAtUtc = DateTimeOffset.UtcNow;
                scoredCount++;

                if (progressCallback is not null)
                {
                    await progressCallback(
                        new JobStageProgress(
                            $"Scoring {processedCount}/{jobsToScore.Count} saved a score for '{displayTitle}'.",
                            jobsToScore.Count,
                            processedCount,
                            scoredCount,
                            failedCount),
                        cancellationToken);
                }
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
}
