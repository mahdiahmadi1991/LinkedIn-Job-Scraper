using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobsDashboardServiceTests
{
    [Fact]
    public async Task RunFetchAndScorePublishesProgressInExpectedStageOrder()
    {
        var notifier = new FakeJobsWorkflowProgressNotifier();
        var service = CreateService(
            dbContextFactory: CreateDbContextFactory(incompleteJobCount: 6),
            jobImportService: new SuccessfulJobImportService(),
            jobEnrichmentService: new SuccessfulJobEnrichmentService(),
            jobsWorkflowProgressNotifier: notifier);

        var result = await service.RunFetchAndScoreAsync("connection-1", "workflow-1", "corr-1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("success", result.Severity);
        Assert.Null(result.ScoringResult);
        Assert.Equal(
            ["fetch", "fetch", "fetch", "fetch", "fetch", "enrichment", "enrichment", "enrichment", "enrichment", "enrichment", "completed"],
            notifier.Updates.Select(static update => update.Stage).ToArray());
        Assert.Equal(
            ["running", "running", "running", "running", "running", "running", "running", "running", "running", "running", "completed"],
            notifier.Updates.Select(static update => update.State).ToArray());
        Assert.Contains(notifier.Updates, static update => update.Percent % 1 != 0);
        Assert.All(notifier.ConnectionIds, static connectionId => Assert.Equal("connection-1", connectionId));
        Assert.All(notifier.Updates, static update => Assert.Equal("corr-1", update.CorrelationId));
        Assert.All(notifier.Updates, static update => Assert.Equal("workflow-1", update.WorkflowId));
    }

    [Fact]
    public async Task RunFetchAndScoreStopsAfterImportFailureAndPublishesFailure()
    {
        var notifier = new FakeJobsWorkflowProgressNotifier();
        var service = CreateService(
            dbContextFactory: CreateDbContextFactory(incompleteJobCount: 0),
            jobImportService: new FailedJobImportService(),
            jobEnrichmentService: new GuardJobEnrichmentService(),
            jobBatchScoringService: new GuardJobBatchScoringService(),
            jobsWorkflowProgressNotifier: notifier);

        var result = await service.RunFetchAndScoreAsync("connection-2", "workflow-2", "corr-2", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("danger", result.Severity);
        Assert.Null(result.EnrichmentResult);
        Assert.Null(result.ScoringResult);
        Assert.Equal(3, notifier.Updates.Count);
        Assert.Equal("fetch", notifier.Updates[0].Stage);
        Assert.Equal("fetch", notifier.Updates[1].Stage);
        Assert.Equal("fetch", notifier.Updates[2].Stage);
        Assert.Equal("failed", notifier.Updates[2].State);
        Assert.All(notifier.Updates, static update => Assert.Equal("corr-2", update.CorrelationId));
        Assert.All(notifier.Updates, static update => Assert.Equal("workflow-2", update.WorkflowId));
        Assert.Contains("Stored session expired.", notifier.Updates[2].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunFetchAndScoreProcessesMultipleEnrichmentBatchesUntilIncompleteJobsAreExhausted()
    {
        var enrichmentService = new RecordingJobEnrichmentService();
        var notifier = new FakeJobsWorkflowProgressNotifier();
        var service = CreateService(
            dbContextFactory: CreateDbContextFactory(incompleteJobCount: 5),
            jobImportService: new SuccessfulJobImportService(),
            jobEnrichmentService: enrichmentService,
            jobsWorkflowProgressNotifier: notifier,
            jobsWorkflowOptions: new JobsWorkflowOptions
            {
                EnrichmentBatchSize = 2
            });

        var result = await service.RunFetchAndScoreAsync("connection-3", "workflow-3", "corr-3", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.EnrichmentResult);
        Assert.Equal([2, 2, 1], enrichmentService.RequestedCounts);
        Assert.Equal(5, result.EnrichmentResult!.RequestedCount);
        Assert.Equal(5, result.EnrichmentResult.ProcessedCount);
        Assert.Equal(5, result.EnrichmentResult.EnrichedCount);
        Assert.Equal(3, notifier.Updates.Count(static update => update.Stage == "enrichment" && update.Message.Contains("Preparing enrichment batch", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RunFetchAndScoreRejectsWhenAnotherWorkflowIsActive()
    {
        var notifier = new FakeJobsWorkflowProgressNotifier();
        var workflowStateStore = new InMemoryJobsWorkflowStateStore();
        var registration = workflowStateStore.RegisterWorkflow("workflow-active", CancellationToken.None);
        Assert.True(registration.Accepted);

        var service = CreateService(
            dbContextFactory: CreateDbContextFactory(incompleteJobCount: 0),
            jobsWorkflowStateStore: workflowStateStore,
            jobsWorkflowProgressNotifier: notifier);

        var result = await service.RunFetchAndScoreAsync("connection-4", "workflow-next", "corr-4", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("warning", result.Severity);
        Assert.Equal(StatusCodes.Status409Conflict, result.ImportResult.StatusCode);
        Assert.Contains("workflow-active", result.Message, StringComparison.Ordinal);
        Assert.Equal("workflow-active", result.ActiveWorkflowId);
        Assert.Empty(notifier.Updates);
    }

    private static JobsDashboardService CreateService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IJobImportService? jobImportService = null,
        IJobEnrichmentService? jobEnrichmentService = null,
        IJobBatchScoringService? jobBatchScoringService = null,
        IJobsWorkflowStateStore? jobsWorkflowStateStore = null,
        IJobsWorkflowProgressNotifier? jobsWorkflowProgressNotifier = null,
        JobsWorkflowOptions? jobsWorkflowOptions = null)
    {
        return new JobsDashboardService(
            dbContextFactory,
            jobImportService ?? new SuccessfulJobImportService(),
            jobEnrichmentService ?? new SuccessfulJobEnrichmentService(),
            jobBatchScoringService ?? new SuccessfulJobBatchScoringService(),
            jobsWorkflowStateStore ?? new InMemoryJobsWorkflowStateStore(),
            jobsWorkflowProgressNotifier ?? new FakeJobsWorkflowProgressNotifier(),
            Options.Create(jobsWorkflowOptions ?? new JobsWorkflowOptions()),
            NullLogger<JobsDashboardService>.Instance);
    }

    private static TestDbContextFactory CreateDbContextFactory(int incompleteJobCount)
    {
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        using (var dbContext = new LinkedInJobScraperDbContext(options))
        {
            for (var index = 0; index < incompleteJobCount; index++)
            {
                dbContext.Jobs.Add(
                    new JobRecord
                    {
                        LinkedInJobId = $"job-{index}",
                        LinkedInJobPostingUrn = $"urn:li:jobPosting:{index}",
                        Title = $"Job {index}",
                        FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
                        LastSeenAtUtc = DateTimeOffset.UtcNow,
                        Description = null,
                        CompanyApplyUrl = null,
                        EmploymentStatus = null
                    });
            }

            dbContext.SaveChanges();
        }

        return new TestDbContextFactory(options);
    }

    private sealed class FakeJobsWorkflowProgressNotifier : IJobsWorkflowProgressNotifier
    {
        public List<string?> ConnectionIds { get; } = [];

        public List<JobsWorkflowProgressUpdate> Updates { get; } = [];

        public Task PublishAsync(
            string? connectionId,
            JobsWorkflowProgressUpdate update,
            CancellationToken cancellationToken)
        {
            ConnectionIds.Add(connectionId);
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class SuccessfulJobImportService : IJobImportService
    {
        public async Task<JobImportResult> ImportCurrentSearchAsync(
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            if (progressCallback is not null)
            {
                await progressCallback(new JobStageProgress("Fetch queued.", 6, 0, 0, 0), cancellationToken);
                await progressCallback(new JobStageProgress("Fetch 1/6.", 6, 1, 1, 0), cancellationToken);
            }

            return JobImportResult.Succeeded(
                pagesFetched: 2,
                fetchedCount: 50,
                totalAvailableCount: 80,
                importedCount: 6,
                updatedExistingCount: 44,
                skippedCount: 44,
                message: "Import completed.");
        }
    }

    private sealed class FailedJobImportService : IJobImportService
    {
        public Task<JobImportResult> ImportCurrentSearchAsync(
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(
                JobImportResult.Failed(
                    "Stored session expired.",
                    StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class SuccessfulJobEnrichmentService : IJobEnrichmentService
    {
        public async Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
            int maxCount,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null,
            IReadOnlySet<Guid>? excludedJobIds = null)
        {
            var attemptedJobIds = Enumerable.Range(0, maxCount)
                .Select(_ => Guid.NewGuid())
                .ToArray();

            if (progressCallback is not null)
            {
                await progressCallback(new JobStageProgress("Enrichment queued.", maxCount, 0, 0, 0), cancellationToken);
                await progressCallback(new JobStageProgress($"Enrichment {maxCount}/{maxCount}.", maxCount, maxCount, maxCount, 0), cancellationToken);
            }

            return JobEnrichmentResult.Succeeded(
                requestedCount: maxCount,
                processedCount: maxCount,
                enrichedCount: maxCount,
                failedCount: 0,
                warningCount: 0,
                attemptedJobIds: attemptedJobIds);
        }
    }

    private sealed class RecordingJobEnrichmentService : IJobEnrichmentService
    {
        public List<int> RequestedCounts { get; } = [];

        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
            int maxCount,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null,
            IReadOnlySet<Guid>? excludedJobIds = null)
        {
            RequestedCounts.Add(maxCount);

            var attemptedJobIds = Enumerable.Range(0, maxCount)
                .Select(_ => Guid.NewGuid())
                .ToArray();

            return Task.FromResult(
                JobEnrichmentResult.Succeeded(
                    requestedCount: maxCount,
                    processedCount: maxCount,
                    enrichedCount: maxCount,
                    failedCount: 0,
                    warningCount: 0,
                    attemptedJobIds: attemptedJobIds));
        }
    }

    private sealed class GuardJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
            int maxCount,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null,
            IReadOnlySet<Guid>? excludedJobIds = null)
        {
            throw new InvalidOperationException("Enrichment should not run after import failure.");
        }
    }

    private sealed class SuccessfulJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(
            int maxCount,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(
                JobBatchScoringResult.Succeeded(
                    requestedCount: 0,
                    processedCount: 0,
                    scoredCount: 0,
                    failedCount: 0));
        }

        public Task<SingleJobScoringResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class GuardJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(
            int maxCount,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            throw new InvalidOperationException("Scoring should not run after import failure.");
        }

        public Task<SingleJobScoringResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Manual scoring should not run in this test.");
        }
    }
}
