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
    private readonly ILinkedInBrowserLoginService _linkedInBrowserLoginService;
    private readonly ILinkedInSessionStore _linkedInSessionStore;
    private readonly ILinkedInSessionVerificationService _linkedInSessionVerificationService;

    public LinkedInSessionController(
        ILinkedInBrowserLoginService linkedInBrowserLoginService,
        ILinkedInSessionStore linkedInSessionStore,
        ILinkedInSessionVerificationService linkedInSessionVerificationService)
    {
        _linkedInBrowserLoginService = linkedInBrowserLoginService;
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
    public async Task<IActionResult> Capture(CancellationToken cancellationToken)
    {
        var result = await _linkedInBrowserLoginService.CaptureAndSaveAsync(cancellationToken);

        if (!result.Success)
        {
            return await BuildActionResponseAsync(result, cancellationToken, StatusCodes.Status409Conflict);
        }

        return await VerifyAndBuildActionResponseAsync(
            "LinkedIn session was captured, but automatic verification failed",
            result.Message,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.SensitiveLocalActions)]
    public async Task<IActionResult> Launch(CancellationToken cancellationToken)
    {
        var result = await _linkedInBrowserLoginService.LaunchLoginAsync(cancellationToken);
        return await BuildActionResponseAsync(result, cancellationToken, result.Success ? null : StatusCodes.Status409Conflict);
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

            return await BuildActionResponseAsync(
                new OperationResult(
                    true,
                    "The stored LinkedIn session was revoked. Use Connect Session to capture a fresh one."),
                cancellationToken);
        }
        catch (Exception exception)
        {
            return await BuildActionResponseAsync(
                new OperationResult(
                    false,
                    $"The stored LinkedIn session could not be revoked: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}"),
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
            var state = await _linkedInBrowserLoginService.GetStateAsync(cancellationToken);

            viewModel.BrowserOpen = state.BrowserOpen;
            viewModel.CurrentPageUrl = state.CurrentPageUrl;
            viewModel.StoredSessionAvailable = state.StoredSessionAvailable;
            viewModel.StoredSessionCapturedAtUtc = state.StoredSessionCapturedAtUtc;
            viewModel.StoredSessionSource = state.StoredSessionSource;
            viewModel.AutoCaptureActive = state.AutoCaptureActive;
            viewModel.AutoCaptureStatusMessage = state.AutoCaptureStatusMessage;
            viewModel.AutoCaptureCompletedSuccessfully = state.AutoCaptureCompletedSuccessfully;
        }
        catch (Exception exception)
        {
            if (string.IsNullOrWhiteSpace(viewModel.StatusMessage))
            {
                viewModel.StatusMessage = $"Stored session state is currently unavailable: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}";
            }

            viewModel.StatusSucceeded = false;
        }

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
                statusCode: failureStatusCode ?? StatusCodes.Status409Conflict);
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

    private static LinkedInSessionActionResponse CreatePayload(LinkedInSessionPageViewModel viewModel)
    {
        return new LinkedInSessionActionResponse(
            viewModel.StatusSucceeded,
            viewModel.StatusMessage,
            new LinkedInSessionStateResponse(
                viewModel.BrowserOpen,
                viewModel.CurrentPageUrl,
                viewModel.StoredSessionAvailable,
                viewModel.StoredSessionCapturedAtUtc,
                viewModel.StoredSessionSource,
                viewModel.AutoCaptureActive,
                viewModel.AutoCaptureStatusMessage,
                viewModel.AutoCaptureCompletedSuccessfully,
                viewModel.ShowManualCaptureAction,
                viewModel.PrimaryActionLabel,
                viewModel.SessionIndicatorLabel,
                viewModel.SessionIndicatorClass));
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
