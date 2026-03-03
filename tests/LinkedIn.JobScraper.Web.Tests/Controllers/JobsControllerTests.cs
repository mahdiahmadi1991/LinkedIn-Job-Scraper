using LinkedIn.JobScraper.Web.AI;
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
        Assert.Equal("Workflow complete.", controller.TempData["JobsAlertMessage"]);
        Assert.Equal("success", controller.TempData["JobsAlertSeverity"]);
        Assert.Equal(25, controller.TempData["JobsWorkflowImportFetchedCount"]);
        Assert.Equal(100, controller.TempData["JobsWorkflowImportTotalCount"]);
        Assert.Equal(3, controller.TempData["JobsWorkflowEnrichmentEnrichedCount"]);
        Assert.Equal(3, controller.TempData["JobsWorkflowScoringScoredCount"]);
        Assert.Equal("connection-1", service.LastConnectionId);
        Assert.Equal("workflow-1", service.LastWorkflowId);
        Assert.False(string.IsNullOrWhiteSpace(service.LastCorrelationId));
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
        Assert.Equal("Fetch & Score failed", details.Title);
        Assert.Equal("Workflow failed.", details.Detail);
        Assert.Equal("Workflow failed.", controller.TempData["JobsAlertMessage"]);
        Assert.Equal("danger", controller.TempData["JobsAlertSeverity"]);
    }

    private sealed class FakeJobsDashboardService : IJobsDashboardService
    {
        public string? LastConnectionId { get; private set; }

        public string? LastWorkflowId { get; private set; }

        public string? LastCorrelationId { get; private set; }

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

            var scoring = JobBatchScoringResult.Succeeded(
                requestedCount: 3,
                processedCount: 3,
                scoredCount: 3,
                failedCount: 0);

            return Task.FromResult(
                new FetchAndScoreWorkflowResult(
                    true,
                    "Workflow complete.",
                    "success",
                    import,
                    enrichment,
                    scoring));
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

        public CancellationToken RegisterWorkflow(string workflowId, CancellationToken outerCancellationToken)
        {
            return outerCancellationToken;
        }

        public bool RequestCancellation(string workflowId)
        {
            return false;
        }
    }
}
