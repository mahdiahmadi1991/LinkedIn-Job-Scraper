using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

public class LinkedInSessionController : Controller
{
    private readonly ILinkedInBrowserLoginService _linkedInBrowserLoginService;
    private readonly ILinkedInSessionVerificationService _linkedInSessionVerificationService;

    public LinkedInSessionController(
        ILinkedInBrowserLoginService linkedInBrowserLoginService,
        ILinkedInSessionVerificationService linkedInSessionVerificationService)
    {
        _linkedInBrowserLoginService = linkedInBrowserLoginService;
        _linkedInSessionVerificationService = linkedInSessionVerificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await BuildViewModelAsync(cancellationToken);

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
        return await BuildActionResponseAsync(result.Success, result.Message, cancellationToken);
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
        try
        {
            var result = await _linkedInSessionVerificationService.VerifyCurrentAsync(cancellationToken);
            return await BuildActionResponseAsync(result.Success, result.Message, cancellationToken);
        }
        catch (Exception exception)
        {
            return await BuildActionResponseAsync(
                false,
                $"Stored session verification failed: {exception.Message}",
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
