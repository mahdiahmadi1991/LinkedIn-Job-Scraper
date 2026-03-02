using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchAndScore(
        [FromForm] JobsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _jobsDashboardService.RunFetchAndScoreAsync(cancellationToken);
        TempData["JobsAlertMessage"] = result.Message;
        TempData["JobsAlertSeverity"] = result.Severity;

        return RedirectToAction(nameof(Index), BuildRouteValues(query));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        Guid jobId,
        JobWorkflowStatus status,
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
}
