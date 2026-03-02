using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

public class LinkedInSessionController : Controller
{
    private readonly ILinkedInBrowserLoginService _linkedInBrowserLoginService;
    private readonly LinkedInFeasibilityProbe _linkedInFeasibilityProbe;

    public LinkedInSessionController(
        ILinkedInBrowserLoginService linkedInBrowserLoginService,
        LinkedInFeasibilityProbe linkedInFeasibilityProbe)
    {
        _linkedInBrowserLoginService = linkedInBrowserLoginService;
        _linkedInFeasibilityProbe = linkedInFeasibilityProbe;
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
                StringComparison.OrdinalIgnoreCase),
            VerificationMessage = TempData["LinkedInSessionVerificationMessage"] as string
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
            var result = await _linkedInFeasibilityProbe.RunUsingStoredSessionAsync(cancellationToken);

            var message = result.Success
                ? $"Stored session verification succeeded. Returned {result.ReturnedCount} jobs from {result.TotalCount} total."
                : $"Stored session verification failed. {result.Message}";

            TempData["LinkedInSessionVerificationMessage"] = message;
            WriteStatusMessage(result.Success, message);
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
