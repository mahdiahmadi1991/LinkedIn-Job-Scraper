using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Controllers;

public sealed class AiSettingsController : Controller
{
    private readonly IAiBehaviorSettingsService _aiBehaviorSettingsService;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;

    public AiSettingsController(
        IAiBehaviorSettingsService aiBehaviorSettingsService,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions)
    {
        _aiBehaviorSettingsService = aiBehaviorSettingsService;
        _openAiSecurityOptions = openAiSecurityOptions;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profile = await _aiBehaviorSettingsService.GetActiveAsync(cancellationToken);

        var viewModel = new AiSettingsPageViewModel
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
        };

        PopulateConnectionStatus(viewModel);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AiSettingsPageViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PopulateConnectionStatus(viewModel);
            viewModel.StatusMessage = "All AI behavior fields are required.";
            viewModel.StatusSucceeded = false;
            return View("Index", viewModel);
        }

        AiBehaviorProfile savedProfile;

        try
        {
            savedProfile = await _aiBehaviorSettingsService.SaveAsync(
                new AiBehaviorProfile(
                    viewModel.ProfileName,
                    viewModel.BehavioralInstructions,
                    viewModel.PrioritySignals,
                    viewModel.ExclusionSignals,
                    viewModel.OutputLanguageCode),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            PopulateConnectionStatus(viewModel);
            viewModel.StatusMessage = exception.Message;
            viewModel.StatusSucceeded = false;
            return View("Index", viewModel);
        }

        TempData["AiSettingsStatusMessage"] =
            $"Saved AI behavior profile '{savedProfile.ProfileName}' with {AiOutputLanguage.GetDisplayName(savedProfile.OutputLanguageCode)} output.";
        TempData["AiSettingsStatusSucceeded"] = bool.TrueString;

        return RedirectToAction(nameof(Index));
    }

    private void PopulateConnectionStatus(AiSettingsPageViewModel viewModel)
    {
        var options = _openAiSecurityOptions.Value;
        var validationMessage = options.ValidateForScoring();

        viewModel.OpenAiApiKeyConfigured = !string.IsNullOrWhiteSpace(options.ApiKey);
        viewModel.OpenAiModel = string.IsNullOrWhiteSpace(options.Model) ? null : options.Model.Trim();
        viewModel.OpenAiBaseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? "https://api.openai.com/v1"
            : options.BaseUrl.TrimEnd('/');
        viewModel.OpenAiConnectionReady = validationMessage is null;
        viewModel.OpenAiConnectionStatusMessage = validationMessage ?? "OpenAI connection settings are configured and ready for scoring.";
    }
}
