using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Tests.Authentication;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class JobsControllerTests
{
    [Fact]
    public async Task FetchAndScoreReturnsJsonPayloadForAjaxRequests()
    {
        var service = new FakeJobsDashboardService();
        var workflowExecutor = new FakeJobsWorkflowExecutor(service);
        var workflowStateStore = new FakeJobsWorkflowStateStore();
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            service,
            workflowExecutor,
            workflowStateStore,
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance)
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
        Assert.Equal("connection-1", workflowExecutor.LastConnectionId);
        Assert.Equal("workflow-1", workflowExecutor.LastWorkflowId);
        Assert.False(string.IsNullOrWhiteSpace(workflowExecutor.LastCorrelationId));
    }

    [Fact]
    public async Task FetchAndScoreReturnsProblemDetailsForAjaxFailures()
    {
        var service = new FailedJobsDashboardService();
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            service,
            new FakeJobsWorkflowExecutor(service),
            new FakeJobsWorkflowStateStore(),
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance)
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
        var service = new WarningJobsDashboardService();
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            service,
            new FakeJobsWorkflowExecutor(service),
            new FakeJobsWorkflowStateStore(),
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance)
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
    public async Task FetchAndScoreReturnsConflictWhenSessionResetIsRequired()
    {
        var service = new FakeJobsDashboardService();
        var resetTracker = new FakeLinkedInSessionResetRequirementTracker();
        resetTracker.MarkRequired(
            LinkedInSessionResetReasonCodes.SessionForbidden,
            "LinkedIn rejected this session with HTTP 403 (Forbidden). Reset Session, then reconnect to continue.",
            StatusCodes.Status403Forbidden);

        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            service,
            new FakeJobsWorkflowExecutor(service),
            new FakeJobsWorkflowStateStore(),
            resetTracker,
            NullLogger<JobsController>.Instance)
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
        Assert.Contains("HTTP 403", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScoreJobReturnsTypedJsonPayload()
    {
        var service = new FakeJobsDashboardService();
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            service,
            new FakeJobsWorkflowExecutor(service),
            new FakeJobsWorkflowStateStore(),
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance)
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

    [Fact]
    public void WorkflowProgressReturnsNotFoundWhenWorkflowIsMissing()
    {
        var stateStore = new FakeJobsWorkflowStateStore
        {
            Batch = new JobsWorkflowProgressBatch([], 1, false, false)
        };
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            new FakeJobsDashboardService(),
            new FakeJobsWorkflowExecutor(new FakeJobsDashboardService()),
            stateStore,
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance);

        var result = controller.WorkflowProgress("workflow-404");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(1, stateStore.LastGetBatchUserId);
        Assert.Equal("workflow-404", stateStore.LastGetBatchWorkflowId);
    }

    [Fact]
    public void WorkflowProgressReturnsJsonWhenWorkflowExists()
    {
        var expectedBatch = new JobsWorkflowProgressBatch([], 2, false, true);
        var stateStore = new FakeJobsWorkflowStateStore
        {
            Batch = expectedBatch
        };
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            new FakeJobsDashboardService(),
            new FakeJobsWorkflowExecutor(new FakeJobsDashboardService()),
            stateStore,
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance);

        var result = controller.WorkflowProgress("workflow-1");

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<JobsWorkflowProgressBatch>(json.Value);
        Assert.Equal(expectedBatch, payload);
    }

    [Fact]
    public async Task UpdateStatusReturnsNotFoundWhenJobDoesNotExistForCurrentUser()
    {
        var service = new NotFoundJobStatusDashboardService();
        var controller = new JobsController(
            new TestCurrentAppUserContext(),
            service,
            new FakeJobsWorkflowExecutor(service),
            new FakeJobsWorkflowStateStore(),
            new FakeLinkedInSessionResetRequirementTracker(),
            NullLogger<JobsController>.Instance);

        var result = await controller.UpdateStatus(
            Guid.NewGuid(),
            JobWorkflowState.Applied,
            new JobsDashboardQuery(),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
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

    private sealed class FakeJobsWorkflowExecutor : IJobsWorkflowExecutor
    {
        private readonly IJobsDashboardService _jobsDashboardService;

        public FakeJobsWorkflowExecutor(IJobsDashboardService jobsDashboardService)
        {
            _jobsDashboardService = jobsDashboardService;
        }

        public string? LastConnectionId { get; private set; }

        public string? LastWorkflowId { get; private set; }

        public string? LastCorrelationId { get; private set; }

        public Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
            string? progressConnectionId,
            string workflowId,
            string? correlationId)
        {
            LastConnectionId = progressConnectionId;
            LastWorkflowId = workflowId;
            LastCorrelationId = correlationId;

            return _jobsDashboardService.RunFetchAndScoreAsync(
                progressConnectionId,
                workflowId,
                correlationId,
                CancellationToken.None);
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

    private sealed class NotFoundJobStatusDashboardService : IJobsDashboardService
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
            throw new NotSupportedException();
        }

        public Task<JobScoreActionResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobStatusChangeResult> UpdateStatusAsync(
            Guid jobId,
            JobWorkflowState status,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new JobStatusChangeResult(
                    false,
                    "Job was not found.",
                    "danger",
                    StatusCodes.Status404NotFound));
        }
    }

    private sealed class FakeJobsWorkflowStateStore : IJobsWorkflowStateStore
    {
        public JobsWorkflowProgressBatch Batch { get; set; } = new([], 1, false, false);

        public int? LastGetBatchUserId { get; private set; }

        public string? LastGetBatchWorkflowId { get; private set; }

        public void Append(int userId, JobsWorkflowProgressUpdate update)
        {
        }

        public JobsWorkflowProgressBatch GetBatch(int userId, string workflowId, long afterSequence)
        {
            LastGetBatchUserId = userId;
            LastGetBatchWorkflowId = workflowId;
            return Batch;
        }

        public JobsWorkflowRegistrationResult RegisterWorkflow(int userId, string workflowId, CancellationToken outerCancellationToken)
        {
            return new JobsWorkflowRegistrationResult(true, null, outerCancellationToken);
        }

        public bool RequestCancellation(int userId, string workflowId)
        {
            return false;
        }

        public void ReleaseWorkflow(int userId, string workflowId)
        {
        }
    }

    private sealed class FakeLinkedInSessionResetRequirementTracker : ILinkedInSessionResetRequirementTracker
    {
        private LinkedInSessionResetRequirementState _current = LinkedInSessionResetRequirementState.NotRequired;

        public LinkedInSessionResetRequirementState GetCurrent()
        {
            return _current;
        }

        public void MarkRequired(string reasonCode, string message, int? statusCode)
        {
            _current = new LinkedInSessionResetRequirementState(
                true,
                reasonCode,
                message,
                statusCode,
                DateTimeOffset.UtcNow);
        }

        public void Clear()
        {
            _current = LinkedInSessionResetRequirementState.NotRequired;
        }
    }
}
