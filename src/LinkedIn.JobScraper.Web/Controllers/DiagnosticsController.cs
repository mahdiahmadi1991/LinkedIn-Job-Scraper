using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Controllers;

[Route("diagnostics")]
public class DiagnosticsController : Controller
{
    private readonly IJobBatchScoringService _jobBatchScoringService;
    private readonly LinkedInFeasibilityProbe _linkedInFeasibilityProbe;
    private readonly IJobEnrichmentService _jobEnrichmentService;
    private readonly IJobImportService _jobImportService;
    private readonly IOptions<SqlServerOptions> _sqlServerOptions;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;
    private readonly ILinkedInSessionStore _linkedInSessionStore;

    public DiagnosticsController(
        LinkedInFeasibilityProbe linkedInFeasibilityProbe,
        IJobImportService jobImportService,
        IJobEnrichmentService jobEnrichmentService,
        IJobBatchScoringService jobBatchScoringService,
        IOptions<SqlServerOptions> sqlServerOptions,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions,
        ILinkedInSessionStore linkedInSessionStore)
    {
        _linkedInFeasibilityProbe = linkedInFeasibilityProbe;
        _jobImportService = jobImportService;
        _jobEnrichmentService = jobEnrichmentService;
        _jobBatchScoringService = jobBatchScoringService;
        _sqlServerOptions = sqlServerOptions;
        _openAiSecurityOptions = openAiSecurityOptions;
        _linkedInSessionStore = linkedInSessionStore;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        LinkedInSessionSnapshot? sessionSnapshot = null;
        string? sessionReadError = null;

        try
        {
            sessionSnapshot = await _linkedInSessionStore.GetCurrentAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            sessionReadError = exception.Message;
        }

        var sqlServerOptions = _sqlServerOptions.Value;
        var openAiSecurityOptions = _openAiSecurityOptions.Value;

        return Json(new
        {
            config = new
            {
                sqlServerConfigured = !string.IsNullOrWhiteSpace(sqlServerOptions.ConnectionString),
                openAiApiKeyConfigured = !string.IsNullOrWhiteSpace(openAiSecurityOptions.ApiKey),
                openAiModelConfigured = !string.IsNullOrWhiteSpace(openAiSecurityOptions.Model)
            },
            session = new
            {
                storedSessionAvailable = sessionSnapshot is not null,
                capturedAtUtc = sessionSnapshot?.CapturedAtUtc,
                source = sessionSnapshot?.Source,
                readError = sessionReadError
            }
        });
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
