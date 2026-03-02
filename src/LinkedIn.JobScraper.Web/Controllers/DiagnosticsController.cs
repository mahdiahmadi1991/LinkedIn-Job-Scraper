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
    public async Task<IActionResult> LinkedInFeasibility(CancellationToken cancellationToken)
    {
        var result = await _linkedInFeasibilityProbe.RunAsync(cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status502BadGateway, result);
        }

        return Json(result);
    }
}
