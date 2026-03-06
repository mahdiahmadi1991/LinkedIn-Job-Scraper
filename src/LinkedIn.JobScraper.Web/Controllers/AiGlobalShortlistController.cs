using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme)]
[Route("ai-global-shortlist")]
public sealed class AiGlobalShortlistController : Controller
{
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IAiGlobalShortlistService _globalShortlistService;
    private readonly IAiGlobalShortlistProgressStateStore _globalShortlistProgressStateStore;

    public AiGlobalShortlistController(
        ICurrentAppUserContext currentAppUserContext,
        IAiGlobalShortlistService globalShortlistService,
        IAiGlobalShortlistProgressStateStore globalShortlistProgressStateStore)
    {
        _currentAppUserContext = currentAppUserContext;
        _globalShortlistService = globalShortlistService;
        _globalShortlistProgressStateStore = globalShortlistProgressStateStore;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost("runs")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> Start()
    {
        var result = await _globalShortlistService.GenerateAsync(
            CancellationToken.None,
            GetProgressConnectionIdFromHeaders());

        return StatusCode(
            result.StatusCode,
            new AiGlobalShortlistStartResponse(
                result.Success,
                result.Message,
                result.RunId,
                result.CandidateCount,
                result.ProcessedCount,
                result.ShortlistedCount,
                result.NeedsReviewCount,
                result.FailedCount));
    }

    [HttpPost("runs/{runId:guid}/resume")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> Resume(Guid runId)
    {
        var result = await _globalShortlistService.ResumeAsync(
            runId,
            CancellationToken.None,
            GetProgressConnectionIdFromHeaders());

        return StatusCode(
            result.StatusCode,
            new AiGlobalShortlistStartResponse(
                result.Success,
                result.Message,
                result.RunId,
                result.CandidateCount,
                result.ProcessedCount,
                result.ShortlistedCount,
                result.NeedsReviewCount,
                result.FailedCount));
    }

    [HttpPost("runs/{runId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> Cancel(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _globalShortlistService.RequestCancelAsync(runId, cancellationToken);

        return StatusCode(
            result.StatusCode,
            new AiGlobalShortlistStartResponse(
                result.Success,
                result.Message,
                result.RunId,
                result.CandidateCount,
                result.ProcessedCount,
                result.ShortlistedCount,
                result.NeedsReviewCount,
                result.FailedCount));
    }

    [HttpGet("runs/{runId:guid}/progress")]
    public async Task<IActionResult> Progress(
        Guid runId,
        [FromQuery] long afterSequence = 0,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        var run = await _globalShortlistService.GetRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var batch = _globalShortlistProgressStateStore.GetBatch(userId, runId, afterSequence);
        return Json(batch.RunFound ? batch : batch with { RunFound = true });
    }

    [HttpGet("runs/overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var overview = await _globalShortlistService.GetQueueOverviewAsync(cancellationToken);
        return Json(
            new AiGlobalShortlistOverviewResponse(
                true,
                "AI live review overview loaded.",
                ToOverviewPayload(overview)));
    }

    [HttpGet("runs/latest")]
    public async Task<IActionResult> Latest(CancellationToken cancellationToken)
    {
        var overview = await _globalShortlistService.GetQueueOverviewAsync(cancellationToken);
        var snapshot = await _globalShortlistService.GetLatestRunAsync(cancellationToken);
        if (snapshot is null)
        {
            return Json(
                new AiGlobalShortlistRunResponse(
                    true,
                    "No AI global shortlist run is available yet.",
                    null,
                    ToOverviewPayload(overview)));
        }

        return Json(
            new AiGlobalShortlistRunResponse(
                true,
                "AI global shortlist run loaded.",
                ToPayload(snapshot),
                ToOverviewPayload(overview)));
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<IActionResult> GetById(Guid runId, CancellationToken cancellationToken)
    {
        var overview = await _globalShortlistService.GetQueueOverviewAsync(cancellationToken);
        var snapshot = await _globalShortlistService.GetRunAsync(runId, cancellationToken);
        if (snapshot is null)
        {
            return StatusCode(
                StatusCodes.Status404NotFound,
                new AiGlobalShortlistRunResponse(
                    false,
                    "AI global shortlist run was not found.",
                    null,
                    ToOverviewPayload(overview)));
        }

        return Json(
            new AiGlobalShortlistRunResponse(
                true,
                "AI global shortlist run loaded.",
                ToPayload(snapshot),
                ToOverviewPayload(overview)));
    }

    private static AiGlobalShortlistRunPayload ToPayload(AiGlobalShortlistRunSnapshot snapshot)
    {
        return new AiGlobalShortlistRunPayload(
            snapshot.RunId,
            snapshot.Status,
            snapshot.CreatedAtUtc,
            snapshot.CompletedAtUtc,
            snapshot.CancellationRequestedAtUtc,
            snapshot.CandidateCount,
            snapshot.ProcessedCount,
            snapshot.ShortlistedCount,
            snapshot.NeedsReviewCount,
            snapshot.FailedCount,
            snapshot.NextSequenceNumber,
            snapshot.ModelName,
            snapshot.Summary,
            snapshot.Items
                .Select(
                    static item =>
                        new AiGlobalShortlistItemPayload(
                            item.JobId,
                            item.LinkedInJobId,
                            item.JobTitle,
                            item.CompanyName,
                            item.LocationName,
                            item.ListedAtUtc,
                            item.Rank,
                            item.Decision,
                            item.CreatedAtUtc,
                            item.PromptVersion,
                            item.ModelName,
                            item.LatencyMilliseconds,
                            item.InputTokenCount,
                            item.OutputTokenCount,
                            item.TotalTokenCount,
                            item.ErrorCode,
                            item.Score,
                            item.Confidence,
                            item.RecommendationReason,
                            item.Concerns))
                .ToArray());
    }

    private static AiGlobalShortlistQueueOverviewPayload ToOverviewPayload(AiGlobalShortlistQueueOverviewSnapshot overview)
    {
        return new AiGlobalShortlistQueueOverviewPayload(
            overview.EligibleTotal,
            overview.AlreadyReviewed,
            overview.QueueRemaining);
    }

    private string? GetProgressConnectionIdFromHeaders()
    {
        var headers = HttpContext?.Request?.Headers;
        if (headers is null ||
            !headers.TryGetValue("X-Progress-ConnectionId", out var values))
        {
            return null;
        }

        var connectionId = values.ToString().Trim();
        return string.IsNullOrWhiteSpace(connectionId)
            ? null
            : connectionId;
    }
}
