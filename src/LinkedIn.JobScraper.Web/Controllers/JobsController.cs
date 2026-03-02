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
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshot = await _jobsDashboardService.GetSnapshotAsync(cancellationToken);
        return View(snapshot);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchAndScore(CancellationToken cancellationToken)
    {
        var result = await _jobsDashboardService.RunFetchAndScoreAsync(cancellationToken);
        TempData["JobsAlertMessage"] = result.Message;
        TempData["JobsAlertSeverity"] = result.Severity;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        Guid jobId,
        JobWorkflowStatus status,
        CancellationToken cancellationToken)
    {
        var result = await _jobsDashboardService.UpdateStatusAsync(jobId, status, cancellationToken);
        TempData["JobsAlertMessage"] = result.Message;
        TempData["JobsAlertSeverity"] = result.Severity;

        return RedirectToAction(nameof(Index));
    }
}
