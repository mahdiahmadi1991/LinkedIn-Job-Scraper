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
        var viewModel = AiSettingsViewModelAdapter.ToViewModel(
            profile,
            TempData["AiSettingsStatusMessage"] as string,
            string.Equals(
                TempData["AiSettingsStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase));

        AiSettingsViewModelAdapter.PopulateConnectionStatus(
            viewModel,
            AiSettingsViewModelAdapter.CreateConnectionState(_openAiSecurityOptions.Value));

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
            AiSettingsViewModelAdapter.PopulateConnectionStatus(
                viewModel,
                AiSettingsViewModelAdapter.CreateConnectionState(_openAiSecurityOptions.Value));
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
            AiSettingsViewModelAdapter.PopulateConnectionStatus(
                viewModel,
                AiSettingsViewModelAdapter.CreateConnectionState(_openAiSecurityOptions.Value));
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
            var redirectUrl = Url is null ? "/AiSettings" : Url.Action(nameof(Index)) ?? "/AiSettings";

            return Json(
                new SettingsSaveResponse(
                    true,
                    TempData["AiSettingsStatusMessage"] as string ?? "AI behavior settings were saved.",
                    redirectUrl,
                    savedProfile.ConcurrencyToken));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ConnectionStatus()
    {
        var payload = AiSettingsViewModelAdapter.CreateConnectionState(_openAiSecurityOptions.Value);

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

    private bool IsAjaxRequest()
    {
        return string.Equals(
            HttpContext?.Request.Headers.XRequestedWith.ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }
}
