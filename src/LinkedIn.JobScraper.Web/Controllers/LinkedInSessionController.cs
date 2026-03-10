using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme)]
public class LinkedInSessionController : Controller
{
    private readonly ILinkedInSessionCurlImportService _linkedInSessionCurlImportService;
    private readonly ILinkedInSessionResetRequirementTracker _linkedInSessionResetRequirementTracker;
    private readonly ILinkedInSessionStore _linkedInSessionStore;
    private readonly ILinkedInSessionVerificationService _linkedInSessionVerificationService;

    public LinkedInSessionController(
        ILinkedInSessionCurlImportService linkedInSessionCurlImportService,
        ILinkedInSessionResetRequirementTracker linkedInSessionResetRequirementTracker,
        ILinkedInSessionStore linkedInSessionStore,
        ILinkedInSessionVerificationService linkedInSessionVerificationService)
    {
        _linkedInSessionCurlImportService = linkedInSessionCurlImportService;
        _linkedInSessionResetRequirementTracker = linkedInSessionResetRequirementTracker;
        _linkedInSessionStore = linkedInSessionStore;
        _linkedInSessionVerificationService = linkedInSessionVerificationService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Jobs");
    }

    [HttpGet]
    public async Task<IActionResult> State(CancellationToken cancellationToken)
    {
        var viewModel = await BuildViewModelAsync(cancellationToken);
        return Json(CreatePayload(viewModel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> ImportCurl(
        [FromForm] string? curlText,
        CancellationToken cancellationToken)
    {
        var result = await _linkedInSessionCurlImportService.ImportAsync(curlText, cancellationToken);
        if (result.Success)
        {
            _linkedInSessionResetRequirementTracker.Clear();
        }

        return await BuildActionResponseAsync(
            result,
            cancellationToken,
            result.Success ? null : result.StatusCode);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        return await VerifyAndBuildActionResponseAsync(
            "Stored session verification failed",
            null,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        try
        {
            await _linkedInSessionStore.InvalidateCurrentAsync(cancellationToken);
            _linkedInSessionResetRequirementTracker.Clear();

            return await BuildActionResponseAsync(
                new OperationResult(
                    true,
                    "The stored LinkedIn session was reset. Use cURL import to connect a fresh one."),
                cancellationToken);
        }
        catch (Exception exception)
        {
            return await BuildActionResponseAsync(
                new OperationResult(
                    false,
                    $"The stored LinkedIn session could not be reset: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}"),
                cancellationToken,
                StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<LinkedInSessionPageViewModel> BuildViewModelAsync(CancellationToken cancellationToken)
    {
        var viewModel = new LinkedInSessionPageViewModel
        {
            StatusMessage = TempData["LinkedInSessionStatusMessage"] as string,
            StatusSucceeded = string.Equals(
                TempData["LinkedInSessionStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase)
        };

        try
        {
            var sessionSnapshot = await _linkedInSessionStore.GetCurrentAsync(cancellationToken);
            viewModel.StoredSessionAvailable = sessionSnapshot is not null;
            viewModel.StoredSessionCapturedAtUtc = sessionSnapshot?.CapturedAtUtc;
            viewModel.StoredSessionSource = sessionSnapshot?.Source;
            viewModel.StoredSessionEstimatedExpiresAtUtc = sessionSnapshot?.EstimatedExpiresAtUtc;
            viewModel.StoredSessionExpirySource = sessionSnapshot?.ExpirySource;
        }
        catch (Exception exception)
        {
            if (string.IsNullOrWhiteSpace(viewModel.StatusMessage))
            {
                viewModel.StatusMessage = $"Stored session state is currently unavailable: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}";
            }

            viewModel.StatusSucceeded = false;
        }

        var resetRequirement = _linkedInSessionResetRequirementTracker.GetCurrent();
        viewModel.ResetRequired = resetRequirement.Required;

        return viewModel;
    }

    private async Task<IActionResult> BuildActionResponseAsync(
        OperationResult result,
        CancellationToken cancellationToken,
        int? failureStatusCode = null)
    {
        WriteStatusMessage(result.Success, result.Message);

        if (!IsAjaxRequest())
        {
            return RedirectToAction("Index", "Jobs");
        }

        if (!result.Success)
        {
            return Problem(
                title: "LinkedIn session action failed",
                detail: result.Message,
                statusCode: NormalizeFailureStatusCode(failureStatusCode));
        }

        var viewModel = await BuildViewModelAsync(cancellationToken);
        return Json(CreatePayload(viewModel));
    }

    private async Task<IActionResult> VerifyAndBuildActionResponseAsync(
        string failurePrefix,
        string? successPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _linkedInSessionVerificationService.VerifyCurrentAsync(cancellationToken);
            ApplyResetRequirementState(result);
            var message = result.Success && !string.IsNullOrWhiteSpace(successPrefix)
                ? $"{successPrefix} {result.Message}"
                : result.Message;

            return await BuildActionResponseAsync(
                new OperationResult(
                    result.Success,
                    message),
                cancellationToken,
                result.Success ? null : result.StatusCode);
        }
        catch (Exception exception)
        {
            return await BuildActionResponseAsync(
                new OperationResult(
                    false,
                    $"{failurePrefix}: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}"),
                cancellationToken,
                StatusCodes.Status500InternalServerError);
        }
    }

    private LinkedInSessionActionResponse CreatePayload(LinkedInSessionPageViewModel viewModel)
    {
        var resetRequirement = _linkedInSessionResetRequirementTracker.GetCurrent();

        return new LinkedInSessionActionResponse(
            viewModel.StatusSucceeded,
            viewModel.StatusMessage,
            new LinkedInSessionStateResponse(
                viewModel.StoredSessionAvailable,
                viewModel.StoredSessionCapturedAtUtc,
                viewModel.StoredSessionSource,
                viewModel.StoredSessionEstimatedExpiresAtUtc,
                viewModel.StoredSessionExpirySource,
                viewModel.SessionIndicatorLabel,
                viewModel.SessionIndicatorClass,
                CreateResetRequirementResponse(resetRequirement)));
    }

    private static LinkedInSessionResetRequirementResponse CreateResetRequirementResponse(
        LinkedInSessionResetRequirementState resetRequirement)
    {
        return new LinkedInSessionResetRequirementResponse(
            resetRequirement.Required,
            resetRequirement.ReasonCode,
            resetRequirement.Message,
            resetRequirement.StatusCode,
            resetRequirement.RequiredAtUtc);
    }

    private void ApplyResetRequirementState(LinkedInSessionVerificationResult verificationResult)
    {
        if (verificationResult.Success)
        {
            _linkedInSessionResetRequirementTracker.Clear();
            return;
        }

        if (verificationResult.StatusCode == StatusCodes.Status401Unauthorized)
        {
            _linkedInSessionResetRequirementTracker.MarkRequired(
                LinkedInSessionResetReasonCodes.SessionUnauthorized,
                "LinkedIn rejected this session with HTTP 401 (Unauthorized). Reset Session, then reconnect to continue.",
                verificationResult.StatusCode);
            return;
        }

        if (verificationResult.StatusCode == StatusCodes.Status403Forbidden)
        {
            _linkedInSessionResetRequirementTracker.MarkRequired(
                LinkedInSessionResetReasonCodes.SessionForbidden,
                "LinkedIn rejected this session with HTTP 403 (Forbidden). Reset Session, then reconnect to continue.",
                verificationResult.StatusCode);
        }
    }

    private static int NormalizeFailureStatusCode(int? statusCode)
    {
        return statusCode is >= 400 and <= 599
            ? statusCode.Value
            : StatusCodes.Status409Conflict;
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private void WriteStatusMessage(bool success, string message)
    {
        TempData["LinkedInSessionStatusMessage"] = message;
        TempData["LinkedInSessionStatusSucceeded"] = success.ToString();
    }
}
