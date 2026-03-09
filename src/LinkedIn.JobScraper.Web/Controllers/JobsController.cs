using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme)]
public class JobsController : Controller
{
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IJobsDashboardService _jobsDashboardService;
    private readonly IJobsWorkflowExecutor _jobsWorkflowExecutor;
    private readonly IJobsWorkflowStateStore _jobsWorkflowStateStore;

    public JobsController(
        ICurrentAppUserContext currentAppUserContext,
        IJobsDashboardService jobsDashboardService,
        IJobsWorkflowExecutor jobsWorkflowExecutor,
        IJobsWorkflowStateStore jobsWorkflowStateStore)
    {
        _currentAppUserContext = currentAppUserContext;
        _jobsDashboardService = jobsDashboardService;
        _jobsWorkflowExecutor = jobsWorkflowExecutor;
        _jobsWorkflowStateStore = jobsWorkflowStateStore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] JobsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var snapshot = await _jobsDashboardService.GetSnapshotAsync(query, cancellationToken);
        return View(snapshot);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobsDashboardService.GetJobDetailsAsync(jobId, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return View(job);
    }

    [HttpGet]
    public async Task<IActionResult> Rows(
        [FromQuery] JobsDashboardQuery query,
        [FromQuery] int offset,
        CancellationToken cancellationToken)
    {
        var rowsChunk = await _jobsDashboardService.GetRowsAsync(query, offset, cancellationToken);
        return PartialView("_JobRows", rowsChunk);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> FetchAndScore(
        [FromForm] JobsDashboardQuery query,
        [FromHeader(Name = "X-Progress-ConnectionId")] string? progressConnectionId,
        [FromHeader(Name = "X-Workflow-Id")] string? workflowId,
        CancellationToken cancellationToken)
    {
        var effectiveWorkflowId = string.IsNullOrWhiteSpace(workflowId)
            ? Guid.NewGuid().ToString("N")
            : workflowId.Trim();

        var result = await _jobsWorkflowExecutor.RunFetchAndScoreAsync(
            progressConnectionId,
            effectiveWorkflowId,
            HttpContext.TraceIdentifier);
        TempData["JobsAlertMessage"] = result.Message;
        TempData["JobsAlertSeverity"] = result.Severity;
        WriteWorkflowSummary(result);

        var redirectUrl = Url.Action(nameof(Index), BuildRouteValues(query)) ?? Url.Action(nameof(Index)) ?? "/Jobs";

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            if (!result.Success)
            {
                if (string.Equals(result.Severity, "warning", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(
                        StatusCodes.Status409Conflict,
                        new FetchAndScoreAjaxResponse(
                            false,
                            result.Severity,
                            result.Message,
                            redirectUrl,
                            result.ActiveWorkflowId));
                }

                return Problem(
                    title: "Fetch Jobs failed",
                    detail: result.Message,
                    statusCode: result.ImportResult.StatusCode > 0
                        ? result.ImportResult.StatusCode
                        : StatusCodes.Status409Conflict);
            }

            return Json(
                new FetchAndScoreAjaxResponse(
                    true,
                    result.Severity,
                    result.Message,
                    redirectUrl,
                    effectiveWorkflowId));
        }

        return Redirect(redirectUrl);
    }

    [HttpGet]
    public IActionResult WorkflowProgress(
        [FromQuery] string workflowId,
        [FromQuery] long afterSequence = 0)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();

        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return Problem(
                title: "Workflow id is required",
                detail: "Provide a workflow id before polling workflow progress.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var batch = _jobsWorkflowStateStore.GetBatch(userId, workflowId.Trim(), afterSequence);
        if (!batch.WorkflowFound)
        {
            return NotFound();
        }

        return Json(batch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public IActionResult CancelWorkflow([FromForm] string workflowId)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();

        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return Problem(
                title: "Workflow id is required",
                detail: "Provide a workflow id before requesting cancellation.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!_jobsWorkflowStateStore.RequestCancellation(userId, workflowId.Trim()))
        {
            return Problem(
                title: "Workflow not found",
                detail: "No running workflow was found for the provided id.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Json(
            new WorkflowCancellationResponse(
                true,
                "Cancellation requested. The current fetch run will stop as soon as the active background step yields."));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> ScoreJob(
        [FromForm] Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _jobsDashboardService.ScoreJobAsync(jobId, cancellationToken);
        var payload = new ScoreJobAjaxResponse(
            result.Success,
            result.Severity,
            result.Message,
            result.Job is null
                ? null
                : new JobScorePayload(
                    result.Job.Id,
                    result.Job.ScoredAtUtc.ToString("O"),
                    result.Job.AiScore,
                    result.Job.AiLabel,
                    result.Job.AiSummary,
                    result.Job.AiWhyMatched,
                    result.Job.AiConcerns,
                    result.Job.AiOutputLanguageCode,
                    result.Job.AiOutputDirection));

        return StatusCode(result.StatusCode, payload);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        Guid jobId,
        JobWorkflowState status,
        [FromForm] JobsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _jobsDashboardService.UpdateStatusAsync(jobId, status, cancellationToken);
        if (!result.Success && result.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        TempData["JobsAlertMessage"] = result.Message;
        TempData["JobsAlertSeverity"] = result.Severity;

        return RedirectToAction(nameof(Index), BuildRouteValues(query));
    }

    private static object BuildRouteValues(JobsDashboardQuery query)
    {
        return new
        {
            search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search,
            filterStatus = query.FilterStatus,
            aiLabel = string.IsNullOrWhiteSpace(query.AiLabel) ? null : query.AiLabel,
            onlyUnscored = query.OnlyUnscored ? (bool?)true : null,
            minScore = query.MinScore,
            sortBy = query.GetNormalizedSortBy()
        };
    }

    private void WriteWorkflowSummary(FetchAndScoreWorkflowResult result)
    {
        TempData["JobsWorkflowSummaryAvailable"] = bool.TrueString;
        TempData["JobsWorkflowImportFetchedCount"] = result.ImportResult.FetchedCount;
        TempData["JobsWorkflowImportTotalCount"] = result.ImportResult.TotalAvailableCount;
        TempData["JobsWorkflowImportImportedCount"] = result.ImportResult.ImportedCount;
        TempData["JobsWorkflowImportRefreshedCount"] = result.ImportResult.UpdatedExistingCount;
        TempData["JobsWorkflowImportSkippedCount"] = result.ImportResult.SkippedCount;

        if (result.EnrichmentResult is not null)
        {
            TempData["JobsWorkflowEnrichmentRequestedCount"] = result.EnrichmentResult.RequestedCount;
            TempData["JobsWorkflowEnrichmentProcessedCount"] = result.EnrichmentResult.ProcessedCount;
            TempData["JobsWorkflowEnrichmentEnrichedCount"] = result.EnrichmentResult.EnrichedCount;
            TempData["JobsWorkflowEnrichmentWarningCount"] = result.EnrichmentResult.WarningCount;
            TempData["JobsWorkflowEnrichmentFailedCount"] = result.EnrichmentResult.FailedCount;
        }

        if (result.ScoringResult is not null)
        {
            TempData["JobsWorkflowScoringRequestedCount"] = result.ScoringResult.RequestedCount;
            TempData["JobsWorkflowScoringProcessedCount"] = result.ScoringResult.ProcessedCount;
            TempData["JobsWorkflowScoringScoredCount"] = result.ScoringResult.ScoredCount;
            TempData["JobsWorkflowScoringFailedCount"] = result.ScoringResult.FailedCount;
        }
    }
}
