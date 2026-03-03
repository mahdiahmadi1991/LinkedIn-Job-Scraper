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
        }
        catch (Exception exception)
        {
            if (string.IsNullOrWhiteSpace(viewModel.StatusMessage))
            {
                viewModel.StatusMessage = $"Stored session state is currently unavailable: {exception.Message}";
            }

            viewModel.StatusSucceeded = false;
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Capture(CancellationToken cancellationToken)
    {
        var result = await _linkedInBrowserLoginService.CaptureAndSaveAsync(cancellationToken);
        WriteStatusMessage(result.Success, result.Message);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Launch(CancellationToken cancellationToken)
    {
        var result = await _linkedInBrowserLoginService.LaunchLoginAsync(cancellationToken);
        WriteStatusMessage(result.Success, result.Message);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _linkedInSessionVerificationService.VerifyCurrentAsync(cancellationToken);
            WriteStatusMessage(result.Success, result.Message);
        }
        catch (Exception exception)
        {
            WriteStatusMessage(false, $"Stored session verification failed: {exception.Message}");
        }

        return RedirectToAction(nameof(Index));
    }

    private void WriteStatusMessage(bool success, string message)
    {
        TempData["LinkedInSessionStatusMessage"] = message;
        TempData["LinkedInSessionStatusSucceeded"] = success.ToString();
    }
}
