using LinkedIn.JobScraper.Web.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using Microsoft.AspNetCore.RateLimiting;

namespace LinkedIn.JobScraper.Web.Controllers;

public class JobsController : Controller
{
    private readonly IJobsDashboardService _jobsDashboardService;

    public JobsController(IJobsDashboardService jobsDashboardService)
    {
        _jobsDashboardService = jobsDashboardService;
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
        CancellationToken cancellationToken)
    {
        var result = await _jobsDashboardService.RunFetchAndScoreAsync(
            progressConnectionId,
            HttpContext.TraceIdentifier,
            cancellationToken);
        TempData["JobsAlertMessage"] = result.Message;
        TempData["JobsAlertSeverity"] = result.Severity;
        WriteWorkflowSummary(result);

        var redirectUrl = Url.Action(nameof(Index), BuildRouteValues(query)) ?? Url.Action(nameof(Index)) ?? "/Jobs";

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            if (!result.Success)
            {
                return Problem(
                    title: "Fetch & Score failed",
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
                    redirectUrl));
        }

        return Redirect(redirectUrl);
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
