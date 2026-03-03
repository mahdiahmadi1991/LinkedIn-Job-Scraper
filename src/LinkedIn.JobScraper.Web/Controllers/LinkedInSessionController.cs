using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

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
    public async Task<IActionResult> Capture(CancellationToken cancellationToken)
    {
        var result = await _linkedInBrowserLoginService.CaptureAndSaveAsync(cancellationToken);

        if (!result.Success)
        {
            return await BuildActionResponseAsync(false, result.Message, cancellationToken);
        }

        return await VerifyAndBuildActionResponseAsync(
            "LinkedIn session was captured, but automatic verification failed",
            result.Message,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Launch(CancellationToken cancellationToken)
    {
        var result = await _linkedInBrowserLoginService.LaunchLoginAsync(cancellationToken);
        return await BuildActionResponseAsync(result.Success, result.Message, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        return await VerifyAndBuildActionResponseAsync(
            "Stored session verification failed",
            null,
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        try
        {
            await _linkedInSessionStore.InvalidateCurrentAsync(cancellationToken);

            return await BuildActionResponseAsync(
                true,
                "The stored LinkedIn session was revoked. Use Connect Session to capture a fresh one.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            return await BuildActionResponseAsync(
                false,
                $"The stored LinkedIn session could not be revoked: {exception.Message}",
                cancellationToken);
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
                viewModel.StatusMessage = $"Stored session state is currently unavailable: {exception.Message}";
            }

            viewModel.StatusSucceeded = false;
        }

        return viewModel;
    }

    private async Task<IActionResult> BuildActionResponseAsync(
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        WriteStatusMessage(success, message);

        if (!IsAjaxRequest())
        {
            return RedirectToAction("Index", "Jobs");
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

            return await BuildActionResponseAsync(result.Success, message, cancellationToken);
        }
        catch (Exception exception)
        {
            return await BuildActionResponseAsync(
                false,
                $"{failurePrefix}: {exception.Message}",
                cancellationToken);
        }
    }

    private static object CreatePayload(LinkedInSessionPageViewModel viewModel)
    {
        return new
        {
            success = viewModel.StatusSucceeded,
            message = viewModel.StatusMessage,
            state = new
            {
                browserOpen = viewModel.BrowserOpen,
                currentPageUrl = viewModel.CurrentPageUrl,
                storedSessionAvailable = viewModel.StoredSessionAvailable,
                storedSessionCapturedAtUtc = viewModel.StoredSessionCapturedAtUtc,
                storedSessionSource = viewModel.StoredSessionSource,
                autoCaptureActive = viewModel.AutoCaptureActive,
                autoCaptureStatusMessage = viewModel.AutoCaptureStatusMessage,
                autoCaptureCompletedSuccessfully = viewModel.AutoCaptureCompletedSuccessfully,
                showManualCaptureAction = viewModel.ShowManualCaptureAction,
                primaryActionLabel = viewModel.PrimaryActionLabel,
                sessionIndicatorLabel = viewModel.SessionIndicatorLabel,
                sessionIndicatorClass = viewModel.SessionIndicatorClass
            }
        };
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
