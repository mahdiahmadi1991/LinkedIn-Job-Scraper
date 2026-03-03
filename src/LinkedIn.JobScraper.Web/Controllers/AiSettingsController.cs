using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

public sealed class AiSettingsController : Controller
{
    private readonly IAiBehaviorSettingsService _aiBehaviorSettingsService;

    public AiSettingsController(IAiBehaviorSettingsService aiBehaviorSettingsService)
    {
        _aiBehaviorSettingsService = aiBehaviorSettingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await _aiBehaviorSettingsService.GetActiveAsync(cancellationToken);

        return View(new AiSettingsPageViewModel
        {
            ProfileName = profile.ProfileName,
            BehavioralInstructions = profile.BehavioralInstructions,
            PrioritySignals = profile.PrioritySignals,
            ExclusionSignals = profile.ExclusionSignals,
            OutputLanguageCode = profile.OutputLanguageCode,
            StatusMessage = TempData["AiSettingsStatusMessage"] as string,
            StatusSucceeded = string.Equals(
                TempData["AiSettingsStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AiSettingsPageViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            viewModel.StatusMessage = "All AI behavior fields are required.";
            viewModel.StatusSucceeded = false;
            return View("Index", viewModel);
        }

        var savedProfile = await _aiBehaviorSettingsService.SaveAsync(
            new AiBehaviorProfile(
                viewModel.ProfileName,
                viewModel.BehavioralInstructions,
                viewModel.PrioritySignals,
                viewModel.ExclusionSignals,
                viewModel.OutputLanguageCode),
            cancellationToken);

        TempData["AiSettingsStatusMessage"] =
            $"Saved AI behavior profile '{savedProfile.ProfileName}' with {AiOutputLanguage.GetDisplayName(savedProfile.OutputLanguageCode)} output.";
        TempData["AiSettingsStatusSucceeded"] = bool.TrueString;

        return RedirectToAction(nameof(Index));
    }
}
