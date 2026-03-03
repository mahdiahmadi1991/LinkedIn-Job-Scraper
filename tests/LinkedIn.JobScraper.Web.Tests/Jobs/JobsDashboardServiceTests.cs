using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobsDashboardServiceTests
{
    [Fact]
    public async Task RunFetchAndScorePublishesProgressInExpectedStageOrder()
    {
        var notifier = new FakeJobsWorkflowProgressNotifier();
        var service = new JobsDashboardService(
            new UnusedDbContextFactory(),
            new SuccessfulJobImportService(),
            new SuccessfulJobEnrichmentService(),
            new SuccessfulJobBatchScoringService(),
            new InMemoryJobsWorkflowStateStore(),
            notifier,
            NullLogger<JobsDashboardService>.Instance);

        var result = await service.RunFetchAndScoreAsync("connection-1", "workflow-1", "corr-1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("success", result.Severity);
        Assert.Equal(
            ["fetch", "fetch", "fetch", "enrichment", "enrichment", "enrichment", "scoring", "scoring", "completed"],
            notifier.Updates.Select(static update => update.Stage).ToArray());
        Assert.Equal(
            ["running", "running", "running", "running", "running", "running", "running", "running", "completed"],
            notifier.Updates.Select(static update => update.State).ToArray());
        Assert.All(notifier.ConnectionIds, static connectionId => Assert.Equal("connection-1", connectionId));
        Assert.All(notifier.Updates, static update => Assert.Equal("corr-1", update.CorrelationId));
        Assert.All(notifier.Updates, static update => Assert.Equal("workflow-1", update.WorkflowId));
    }

    [Fact]
    public async Task RunFetchAndScoreStopsAfterImportFailureAndPublishesFailure()
    {
        var notifier = new FakeJobsWorkflowProgressNotifier();
        var service = new JobsDashboardService(
            new UnusedDbContextFactory(),
            new FailedJobImportService(),
            new GuardJobEnrichmentService(),
            new GuardJobBatchScoringService(),
            new InMemoryJobsWorkflowStateStore(),
            notifier,
            NullLogger<JobsDashboardService>.Instance);

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

    private sealed class UnusedDbContextFactory : IDbContextFactory<LinkedInJobScraperDbContext>
    {
        public LinkedInJobScraperDbContext CreateDbContext()
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInJobScraperDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
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
        public Task<JobImportResult> ImportCurrentSearchAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                JobImportResult.Succeeded(
                    pagesFetched: 2,
                    fetchedCount: 50,
                    totalAvailableCount: 80,
                    importedCount: 6,
                    updatedExistingCount: 44,
                    skippedCount: 44,
                    message: "Import completed."));
        }
    }

    private sealed class FailedJobImportService : IJobImportService
    {
        public Task<JobImportResult> ImportCurrentSearchAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                JobImportResult.Failed(
                    "Stored session expired.",
                    StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class SuccessfulJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(int maxCount, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                JobEnrichmentResult.Succeeded(
                    requestedCount: 6,
                    processedCount: 6,
                    enrichedCount: 6,
                    failedCount: 0,
                    warningCount: 0));
        }
    }

    private sealed class GuardJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(int maxCount, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Enrichment should not run after import failure.");
        }
    }

    private sealed class SuccessfulJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(int maxCount, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                JobBatchScoringResult.Succeeded(
                    requestedCount: 6,
                    processedCount: 6,
                    scoredCount: 6,
                    failedCount: 0));
        }
    }

    private sealed class GuardJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(int maxCount, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Scoring should not run after import failure.");
        }
    }
}
