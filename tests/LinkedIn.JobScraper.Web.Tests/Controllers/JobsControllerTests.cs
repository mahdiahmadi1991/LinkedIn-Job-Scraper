using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class JobsControllerTests
{
    [Fact]
    public async Task FetchAndScoreReturnsJsonPayloadForAjaxRequests()
    {
        var service = new FakeJobsDashboardService();
        var workflowStateStore = new FakeJobsWorkflowStateStore();
        var controller = new JobsController(service, workflowStateStore)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()),
            Url = new TestUrlHelper("/Jobs?sortBy=last-seen")
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.FetchAndScore(new JobsDashboardQuery(), "connection-1", "workflow-1", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<FetchAndScoreAjaxResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal("success", payload.Severity);
        Assert.Equal("Workflow complete.", payload.Message);
        Assert.Equal("/Jobs?sortBy=last-seen", payload.RedirectUrl);
        Assert.Equal("workflow-1", payload.WorkflowId);
        Assert.Equal("Workflow complete.", controller.TempData["JobsAlertMessage"]);
        Assert.Equal("success", controller.TempData["JobsAlertSeverity"]);
        Assert.Equal(25, controller.TempData["JobsWorkflowImportFetchedCount"]);
        Assert.Equal(100, controller.TempData["JobsWorkflowImportTotalCount"]);
        Assert.Equal(3, controller.TempData["JobsWorkflowEnrichmentEnrichedCount"]);
        Assert.Null(controller.TempData["JobsWorkflowScoringScoredCount"]);
        Assert.Equal("connection-1", service.LastConnectionId);
        Assert.Equal("workflow-1", service.LastWorkflowId);
        Assert.False(string.IsNullOrWhiteSpace(service.LastCorrelationId));
        Assert.Equal(CancellationToken.None, service.LastCancellationToken);
    }

    [Fact]
    public async Task FetchAndScoreReturnsProblemDetailsForAjaxFailures()
    {
        var controller = new JobsController(new FailedJobsDashboardService(), new FakeJobsWorkflowStateStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()),
            Url = new TestUrlHelper("/Jobs")
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.FetchAndScore(new JobsDashboardQuery(), "connection-1", "workflow-1", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("Fetch Jobs failed", details.Title);
        Assert.Equal("Workflow failed.", details.Detail);
        Assert.Equal("Workflow failed.", controller.TempData["JobsAlertMessage"]);
        Assert.Equal("danger", controller.TempData["JobsAlertSeverity"]);
    }

    [Fact]
    public async Task FetchAndScoreReturnsWarningJsonPayloadForAjaxWarnings()
    {
        var controller = new JobsController(new WarningJobsDashboardService(), new FakeJobsWorkflowStateStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()),
            Url = new TestUrlHelper("/Jobs")
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.FetchAndScore(new JobsDashboardQuery(), "connection-1", "workflow-1", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<FetchAndScoreAjaxResponse>(objectResult.Value);

        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        Assert.False(payload.Success);
        Assert.Equal("warning", payload.Severity);
        Assert.Equal("Workflow already running.", payload.Message);
        Assert.Equal("/Jobs", payload.RedirectUrl);
        Assert.Equal("workflow-active", payload.WorkflowId);
    }

    [Fact]
    public async Task ScoreJobReturnsTypedJsonPayload()
    {
        var service = new FakeJobsDashboardService();
        var controller = new JobsController(service, new FakeJobsWorkflowStateStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.ScoreJob(Guid.Parse("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<ScoreJobAjaxResponse>(objectResult.Value);

        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
        Assert.True(payload.Success);
        Assert.Equal("success", payload.Severity);
        Assert.Equal(91, payload.Job?.AiScore);
        Assert.Equal("StrongMatch", payload.Job?.AiLabel);
    }

    private sealed class FakeJobsDashboardService : IJobsDashboardService
    {
        public string? LastConnectionId { get; private set; }

        public string? LastWorkflowId { get; private set; }

        public string? LastCorrelationId { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<JobsDashboardSnapshot> GetSnapshotAsync(JobsDashboardQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobDetailsSnapshot?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobsRowsChunk> GetRowsAsync(JobsDashboardQuery query, int offset, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
            string? progressConnectionId,
            string workflowId,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            LastConnectionId = progressConnectionId;
            LastWorkflowId = workflowId;
            LastCorrelationId = correlationId;
            LastCancellationToken = cancellationToken;

            var import = JobImportResult.Succeeded(
                pagesFetched: 1,
                fetchedCount: 25,
                totalAvailableCount: 100,
                importedCount: 3,
                updatedExistingCount: 22,
                skippedCount: 22,
                message: "Import ok");

            var enrichment = JobEnrichmentResult.Succeeded(
                requestedCount: 3,
                processedCount: 3,
                enrichedCount: 3,
                failedCount: 0,
                warningCount: 0);

            return Task.FromResult(
                new FetchAndScoreWorkflowResult(
                    true,
                    "Workflow complete.",
                    "success",
                    import,
                    enrichment,
                    null));
        }

        public Task<JobScoreActionResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new JobScoreActionResult(
                    true,
                    "AI scoring completed.",
                    "success",
                    StatusCodes.Status200OK,
                    new JobScoreSnapshot(
                        jobId,
                        new DateTimeOffset(2026, 3, 3, 21, 0, 0, TimeSpan.Zero),
                        91,
                        "StrongMatch",
                        "Summary",
                        "Why matched",
                        "Concerns",
                        "en",
                        "ltr")));
        }

        public Task<JobStatusChangeResult> UpdateStatusAsync(Guid jobId, JobWorkflowState status, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailedJobsDashboardService : IJobsDashboardService
    {
        public Task<JobsDashboardSnapshot> GetSnapshotAsync(JobsDashboardQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobDetailsSnapshot?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobsRowsChunk> GetRowsAsync(JobsDashboardQuery query, int offset, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
            string? progressConnectionId,
            string workflowId,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            var import = JobImportResult.Failed("Import failed.", StatusCodes.Status503ServiceUnavailable);

            return Task.FromResult(
                new FetchAndScoreWorkflowResult(
                    false,
                    "Workflow failed.",
                    "danger",
                    import,
                    null,
                    null));
        }

        public Task<JobScoreActionResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobStatusChangeResult> UpdateStatusAsync(Guid jobId, JobWorkflowState status, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class WarningJobsDashboardService : IJobsDashboardService
    {
        public Task<JobsDashboardSnapshot> GetSnapshotAsync(JobsDashboardQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobDetailsSnapshot?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobsRowsChunk> GetRowsAsync(JobsDashboardQuery query, int offset, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(string? progressConnectionId, string workflowId, string? correlationId, CancellationToken cancellationToken)
        {
            var import = JobImportResult.Failed("Workflow already running.", StatusCodes.Status409Conflict);

            return Task.FromResult(
                new FetchAndScoreWorkflowResult(
                    false,
                    "Workflow already running.",
                    "warning",
                    import,
                    null,
                    null,
                    "workflow-active"));
        }

        public Task<JobScoreActionResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobStatusChangeResult> UpdateStatusAsync(Guid jobId, JobWorkflowState status, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeJobsWorkflowStateStore : IJobsWorkflowStateStore
    {
        public void Append(JobsWorkflowProgressUpdate update)
        {
        }

        public JobsWorkflowProgressBatch GetBatch(string workflowId, long afterSequence)
        {
            return new JobsWorkflowProgressBatch([], 1, false, false);
        }

        public JobsWorkflowRegistrationResult RegisterWorkflow(string workflowId, CancellationToken outerCancellationToken)
        {
            return new JobsWorkflowRegistrationResult(true, null, outerCancellationToken);
        }

        public bool RequestCancellation(string workflowId)
        {
            return false;
        }

        public void ReleaseWorkflow(string workflowId)
        {
        }
    }
}
