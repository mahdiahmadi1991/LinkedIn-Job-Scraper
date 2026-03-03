using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme)]
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
            sessionReadError = SensitiveDataRedaction.SanitizeForMessage(exception.Message);
        }

        var sqlServerOptions = _sqlServerOptions.Value;
        var openAiSecurityOptions = _openAiSecurityOptions.Value;

        return Json(
            new DiagnosticsSummaryResponse(
                new DiagnosticsConfigSummaryResponse(
                    !string.IsNullOrWhiteSpace(sqlServerOptions.ConnectionString),
                    !string.IsNullOrWhiteSpace(openAiSecurityOptions.ApiKey),
                    !string.IsNullOrWhiteSpace(openAiSecurityOptions.Model)),
                new DiagnosticsSessionSummaryResponse(
                    sessionSnapshot is not null,
                    sessionSnapshot?.CapturedAtUtc,
                    sessionSnapshot?.Source,
                    sessionReadError)));
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
                $"Feasibility probe failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (!result.Success)
        {
            return Problem(
                title: "LinkedIn diagnostics probe failed",
                detail: result.Message,
                statusCode: result.StatusCode ?? StatusCodes.Status502BadGateway);
        }

        return Json(
            new LinkedInFeasibilityResponse(
                result.Success,
                result.Message,
                result.StatusCode,
                result.ReturnedCount,
                result.TotalCount,
                result.SampledJobCardUrns,
                result.ResponsePreview));
    }

    [HttpPost("import-current-search")]
    public async Task<IActionResult> ImportCurrentSearch(CancellationToken cancellationToken)
    {
        var result = await _jobImportService.ImportCurrentSearchAsync(cancellationToken);

        if (!result.Success)
        {
            return Problem(
                title: "LinkedIn import diagnostics failed",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        return Json(
            new DiagnosticsImportResponse(
                result.Success,
                result.Message,
                result.StatusCode,
                result.PagesFetched,
                result.FetchedCount,
                result.TotalAvailableCount,
                result.ImportedCount,
                result.UpdatedExistingCount,
                result.SkippedCount));
    }

    [HttpPost("enrich-incomplete-jobs")]
    public async Task<IActionResult> EnrichIncompleteJobs(
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobEnrichmentService.EnrichIncompleteJobsAsync(count, cancellationToken);

        if (!result.Success)
        {
            return Problem(
                title: "LinkedIn enrichment diagnostics failed",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        return Json(
            new DiagnosticsEnrichmentResponse(
                result.Success,
                result.Message,
                result.StatusCode,
                result.RequestedCount,
                result.ProcessedCount,
                result.EnrichedCount,
                result.FailedCount,
                result.WarningCount));
    }

    [HttpPost("score-ready-jobs")]
    public async Task<IActionResult> ScoreReadyJobs(
        [FromQuery] int count = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobBatchScoringService.ScoreReadyJobsAsync(count, cancellationToken);

        if (!result.Success)
        {
            return Problem(
                title: "AI scoring diagnostics failed",
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        return Json(
            new DiagnosticsScoringResponse(
                result.Success,
                result.Message,
                result.StatusCode,
                result.RequestedCount,
                result.ProcessedCount,
                result.ScoredCount,
                result.FailedCount));
    }
}
