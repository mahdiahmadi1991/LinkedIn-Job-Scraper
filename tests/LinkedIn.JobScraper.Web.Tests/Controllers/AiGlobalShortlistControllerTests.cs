using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AiGlobalShortlistControllerTests
{
    [Fact]
    public async Task StartReturnsTypedPayloadAndStatusCode()
    {
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                new AiGlobalShortlistRunResult(
                    true,
                    "completed",
                    StatusCodes.Status200OK,
                    Guid.Parse("aaaa1111-2222-3333-4444-555566667777"),
                    120,
                    5,
                    25,
                    2,
                    0)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.Start();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var payload = Assert.IsType<AiGlobalShortlistStartResponse>(objectResult.Value);
        Assert.True(payload.Success);
        Assert.Equal(120, payload.CandidateCount);
        Assert.Equal(25, payload.ShortlistedCount);
    }

    [Fact]
    public async Task StartReturnsFailurePayloadWithServiceStatusCode()
    {
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                new AiGlobalShortlistRunResult(
                    false,
                    "generation failed",
                    StatusCodes.Status502BadGateway,
                    null,
                    0,
                    1,
                    0,
                    0,
                    1)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.Start();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);

        var payload = Assert.IsType<AiGlobalShortlistStartResponse>(objectResult.Value);
        Assert.False(payload.Success);
        Assert.Equal("generation failed", payload.Message);
    }

    [Fact]
    public async Task ResumeReturnsTypedPayload()
    {
        var runId = Guid.NewGuid();
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(runId, 30, 10, 6, 2, 1)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.Resume(runId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var payload = Assert.IsType<AiGlobalShortlistStartResponse>(objectResult.Value);
        Assert.True(payload.Success);
        Assert.Equal(runId, payload.RunId);
        Assert.Equal(10, payload.ProcessedCount);
    }

    [Fact]
    public async Task CancelReturnsTypedPayload()
    {
        var runId = Guid.NewGuid();
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(runId, 30, 12, 7, 3, 1)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.Cancel(runId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var payload = Assert.IsType<AiGlobalShortlistStartResponse>(objectResult.Value);
        Assert.True(payload.Success);
        Assert.Equal(runId, payload.RunId);
        Assert.Equal(12, payload.ProcessedCount);
    }

    [Fact]
    public async Task LatestReturnsEmptySuccessWhenNoRunExists()
    {
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(Guid.NewGuid(), 0, 0, 0, 0, 0),
                queueOverview: new AiGlobalShortlistQueueOverviewSnapshot(10, 3, 7)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.Latest(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiGlobalShortlistRunResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Null(payload.Run);
        Assert.Equal(10, payload.Overview.EligibleTotal);
        Assert.Equal(3, payload.Overview.AlreadyReviewed);
        Assert.Equal(7, payload.Overview.QueueRemaining);
    }

    [Fact]
    public async Task GetByIdReturnsNotFoundPayloadWhenRunDoesNotExist()
    {
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(Guid.NewGuid(), 0, 0, 0, 0, 0),
                queueOverview: new AiGlobalShortlistQueueOverviewSnapshot(20, 5, 15)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var payload = Assert.IsType<AiGlobalShortlistRunResponse>(objectResult.Value);
        Assert.False(payload.Success);
        Assert.Null(payload.Run);
        Assert.Equal(20, payload.Overview.EligibleTotal);
        Assert.Equal(5, payload.Overview.AlreadyReviewed);
        Assert.Equal(15, payload.Overview.QueueRemaining);
    }

    [Fact]
    public async Task GetByIdReturnsRunPayloadWhenRunExists()
    {
        var runId = Guid.Parse("bbbb1111-2222-3333-4444-555566667777");
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(runId, 30, 2, 10, 1, 0),
                new AiGlobalShortlistRunSnapshot(
                    runId,
                    "Completed",
                    DateTimeOffset.UtcNow.AddMinutes(-2),
                    DateTimeOffset.UtcNow,
                    null,
                    30,
                    2,
                    10,
                    1,
                    0,
                    3,
                    "gpt-5-mini",
                    "ok",
                    [
                        new AiGlobalShortlistItemSnapshot(
                            Guid.Parse("cccc1111-2222-3333-4444-555566667777"),
                            "4379963196",
                            "Senior .NET Engineer",
                            "Acme",
                            "Limassol, Cyprus",
                            DateTimeOffset.UtcNow.AddDays(-1),
                            1,
                            "Accepted",
                            DateTimeOffset.UtcNow,
                            "v1",
                            "gpt-5-mini",
                            420,
                            null,
                            null,
                            null,
                            null,
                            94,
                            88,
                            "Strong technical match",
                            "Time zone overlap required")
                    ]),
                new AiGlobalShortlistQueueOverviewSnapshot(44, 14, 30)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.GetById(runId, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiGlobalShortlistRunResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.NotNull(payload.Run);
        Assert.Equal(runId, payload.Run!.RunId);
        Assert.Single(payload.Run.Items);
        Assert.Equal(1, payload.Run.Items[0].Rank);
        Assert.Equal(44, payload.Overview.EligibleTotal);
        Assert.Equal(14, payload.Overview.AlreadyReviewed);
        Assert.Equal(30, payload.Overview.QueueRemaining);
    }

    [Fact]
    public void ProgressReturnsOrderedBatch()
    {
        var runId = Guid.NewGuid();
        var progressStore = new InMemoryAiGlobalShortlistProgressStateStore();
        progressStore.Append(
            new AiGlobalShortlistProgressUpdate(
                runId,
                "running",
                "candidate-processed",
                "ok",
                CandidateCount: 10,
                ProcessedCount: 1,
                AcceptedCount: 1));

        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(runId, 10, 1, 1, 0, 0)),
            progressStore);

        var result = controller.Progress(runId, 0);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiGlobalShortlistProgressBatch>(json.Value);
        Assert.True(payload.RunFound);
        Assert.Single(payload.Events);
        Assert.Equal(1, payload.Events[0].Sequence);
        Assert.Equal("candidate-processed", payload.Events[0].Update.Stage);
    }

    [Fact]
    public async Task OverviewReturnsTypedPayload()
    {
        var controller = new AiGlobalShortlistController(
            new FakeAiGlobalShortlistService(
                AiGlobalShortlistRunResult.Succeeded(Guid.NewGuid(), 0, 0, 0, 0, 0),
                queueOverview: new AiGlobalShortlistQueueOverviewSnapshot(125, 40, 85)),
            new InMemoryAiGlobalShortlistProgressStateStore());

        var result = await controller.Overview(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiGlobalShortlistOverviewResponse>(json.Value);
        Assert.True(payload.Success);
        Assert.Equal(125, payload.Overview.EligibleTotal);
        Assert.Equal(40, payload.Overview.AlreadyReviewed);
        Assert.Equal(85, payload.Overview.QueueRemaining);
    }

    private sealed class FakeAiGlobalShortlistService : IAiGlobalShortlistService
    {
        private readonly AiGlobalShortlistRunResult _startResult;
        private readonly AiGlobalShortlistRunSnapshot? _run;
        private readonly AiGlobalShortlistQueueOverviewSnapshot _queueOverview;

        public FakeAiGlobalShortlistService(
            AiGlobalShortlistRunResult startResult,
            AiGlobalShortlistRunSnapshot? run = null,
            AiGlobalShortlistQueueOverviewSnapshot? queueOverview = null)
        {
            _startResult = startResult;
            _run = run;
            _queueOverview = queueOverview ?? new AiGlobalShortlistQueueOverviewSnapshot(0, 0, 0);
        }

        public Task<AiGlobalShortlistRunResult> GenerateAsync(
            CancellationToken cancellationToken,
            string? progressConnectionId = null,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(_startResult);
        }

        public Task<AiGlobalShortlistRunSnapshot?> GetLatestRunAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_run);
        }

        public Task<AiGlobalShortlistRunResult> ResumeAsync(
            Guid runId,
            CancellationToken cancellationToken,
            string? progressConnectionId = null,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(_startResult);
        }

        public Task<AiGlobalShortlistRunResult> RequestCancelAsync(Guid runId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_startResult);
        }

        public Task<AiGlobalShortlistRunSnapshot?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
        {
            if (_run is null || _run.RunId != runId)
            {
                return Task.FromResult<AiGlobalShortlistRunSnapshot?>(null);
            }

            return Task.FromResult<AiGlobalShortlistRunSnapshot?>(_run);
        }

        public Task<AiGlobalShortlistQueueOverviewSnapshot> GetQueueOverviewAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_queueOverview);
        }
    }
}
