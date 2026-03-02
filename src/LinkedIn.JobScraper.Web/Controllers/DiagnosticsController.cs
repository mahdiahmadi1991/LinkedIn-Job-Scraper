using LinkedIn.JobScraper.Web.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Route("diagnostics")]
public class DiagnosticsController : Controller
{
    private readonly LinkedInFeasibilityProbe _linkedInFeasibilityProbe;

    public DiagnosticsController(LinkedInFeasibilityProbe linkedInFeasibilityProbe)
    {
        _linkedInFeasibilityProbe = linkedInFeasibilityProbe;
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
}
