using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Http;
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
            ConcurrencyToken = profile.ConcurrencyToken,
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

            if (IsAjaxRequest())
            {
                return Problem(
                    title: "AI settings validation failed",
                    detail: viewModel.StatusMessage,
                    statusCode: StatusCodes.Status400BadRequest);
            }

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
                    viewModel.OutputLanguageCode,
                    viewModel.ConcurrencyToken),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            PopulateConnectionStatus(viewModel);
            viewModel.StatusMessage = exception.Message;
            viewModel.StatusSucceeded = false;

            if (IsAjaxRequest())
            {
                return Problem(
                    title: "AI settings save failed",
                    detail: viewModel.StatusMessage,
                    statusCode: StatusCodes.Status409Conflict);
            }

            return View("Index", viewModel);
        }

        viewModel.ConcurrencyToken = savedProfile.ConcurrencyToken;

        TempData["AiSettingsStatusMessage"] =
            $"Saved AI behavior profile '{savedProfile.ProfileName}' with {AiOutputLanguage.GetDisplayName(savedProfile.OutputLanguageCode)} output.";
        TempData["AiSettingsStatusSucceeded"] = bool.TrueString;

        if (IsAjaxRequest())
        {
            return Json(
                new SettingsSaveResponse(
                    true,
                    TempData["AiSettingsStatusMessage"] as string ?? "AI behavior settings were saved.",
                    Url.Action(nameof(Index)) ?? "/AiSettings"));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ConnectionStatus()
    {
        var payload = CreateConnectionStatusPayload();

        if (!payload.Ready)
        {
            return Problem(
                title: "AI connection is not ready",
                detail: payload.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Json(
            new AiConnectionStatusResponse(
                true,
                payload.Message,
                new AiConnectionStateResponse(
                    payload.ApiKeyConfigured,
                    payload.Model,
                    payload.BaseUrl,
                    payload.Ready)));
    }

    private void PopulateConnectionStatus(AiSettingsPageViewModel viewModel)
    {
        var payload = CreateConnectionStatusPayload();

        viewModel.OpenAiApiKeyConfigured = payload.ApiKeyConfigured;
        viewModel.OpenAiModel = payload.Model;
        viewModel.OpenAiBaseUrl = payload.BaseUrl;
        viewModel.OpenAiConnectionReady = payload.Ready;
        viewModel.OpenAiConnectionStatusMessage = payload.Message;
    }

    private OpenAiConnectionStatusPayload CreateConnectionStatusPayload()
    {
        var options = _openAiSecurityOptions.Value;
        var validationMessage = options.ValidateForScoring();

        return new OpenAiConnectionStatusPayload(
            ApiKeyConfigured: !string.IsNullOrWhiteSpace(options.ApiKey),
            Model: string.IsNullOrWhiteSpace(options.Model) ? null : options.Model.Trim(),
            BaseUrl: string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://api.openai.com/v1"
                : options.BaseUrl.TrimEnd('/'),
            Ready: validationMessage is null,
            Message: validationMessage ?? "OpenAI connection settings are configured and ready for scoring.");
    }

    private sealed record OpenAiConnectionStatusPayload(
        bool ApiKeyConfigured,
        string? Model,
        string BaseUrl,
        bool Ready,
        string Message);

    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers.XRequestedWith.ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }
}
