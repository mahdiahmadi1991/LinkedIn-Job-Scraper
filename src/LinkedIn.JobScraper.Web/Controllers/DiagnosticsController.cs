using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Route("diagnostics")]
public class DiagnosticsController : Controller
{
    private readonly IJobBatchScoringService _jobBatchScoringService;
    private readonly LinkedInFeasibilityProbe _linkedInFeasibilityProbe;
    private readonly IJobEnrichmentService _jobEnrichmentService;
    private readonly IJobImportService _jobImportService;

    public DiagnosticsController(
        LinkedInFeasibilityProbe linkedInFeasibilityProbe,
        IJobImportService jobImportService,
        IJobEnrichmentService jobEnrichmentService,
        IJobBatchScoringService jobBatchScoringService)
    {
        _linkedInFeasibilityProbe = linkedInFeasibilityProbe;
        _jobImportService = jobImportService;
        _jobEnrichmentService = jobEnrichmentService;
        _jobBatchScoringService = jobBatchScoringService;
    }

    [HttpGet("linkedin-feasibility")]
    public async Task<IActionResult> LinkedInFeasibility(
        [FromQuery] bool useStoredSession,
        CancellationToken cancellationToken)
    {
        LinkedInFeasibilityResult result;

        try
        {
            result = useStoredSession
                ? await _linkedInFeasibilityProbe.RunUsingStoredSessionAsync(cancellationToken)
                : await _linkedInFeasibilityProbe.RunAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            result = LinkedInFeasibilityResult.Failed(
                $"Feasibility probe failed: {exception.Message}",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway, result);
        }

        return Json(result);
    }

    [HttpPost("import-current-search")]
    public async Task<IActionResult> ImportCurrentSearch(CancellationToken cancellationToken)
    {
        var result = await _jobImportService.ImportCurrentSearchAsync(cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, result);
        }

        return Json(result);
    }

    [HttpPost("enrich-incomplete-jobs")]
    public async Task<IActionResult> EnrichIncompleteJobs(
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobEnrichmentService.EnrichIncompleteJobsAsync(count, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, result);
        }

        return Json(result);
    }

    [HttpPost("score-ready-jobs")]
    public async Task<IActionResult> ScoreReadyJobs(
        [FromQuery] int count = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobBatchScoringService.ScoreReadyJobsAsync(count, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, result);
        }

        return Json(result);
    }
}
