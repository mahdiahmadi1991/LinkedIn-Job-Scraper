using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme)]
public sealed class AiSettingsController : Controller
{
    private readonly IAiBehaviorSettingsService _aiBehaviorSettingsService;
    private readonly IOpenAiEffectiveSecurityOptionsResolver _openAiEffectiveSecurityOptionsResolver;

    public AiSettingsController(
        IAiBehaviorSettingsService aiBehaviorSettingsService,
        IOpenAiEffectiveSecurityOptionsResolver openAiEffectiveSecurityOptionsResolver)
    {
        _aiBehaviorSettingsService = aiBehaviorSettingsService;
        _openAiEffectiveSecurityOptionsResolver = openAiEffectiveSecurityOptionsResolver;
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
            await ResolveOpenAiConnectionStateAsync(cancellationToken));

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
                await ResolveOpenAiConnectionStateAsync(cancellationToken));
            viewModel.StatusMessage = "All AI behavior fields are required.";
            viewModel.StatusSucceeded = false;

            if (IsAjaxRequest())
            {
                var details = new ValidationProblemDetails(ModelState)
                {
                    Title = "AI settings validation failed",
                    Detail = viewModel.StatusMessage,
                    Status = StatusCodes.Status400BadRequest
                };

                return BadRequest(details);
            }

            return View("Index", viewModel);
        }

        var guardrailEvaluation = AiBehaviorInputGuardrails.Evaluate(
            viewModel.BehavioralInstructions,
            viewModel.PrioritySignals,
            viewModel.ExclusionSignals);

        viewModel.BehavioralInstructions = guardrailEvaluation.BehavioralInstructions;
        viewModel.PrioritySignals = guardrailEvaluation.PrioritySignals;
        viewModel.ExclusionSignals = guardrailEvaluation.ExclusionSignals;

        if (guardrailEvaluation.IsBlocked)
        {
            AddGuardrailBlockingErrors(guardrailEvaluation);

            AiSettingsViewModelAdapter.PopulateConnectionStatus(
                viewModel,
                await ResolveOpenAiConnectionStateAsync(cancellationToken));
            viewModel.StatusMessage = "AI settings were blocked by guardrails. Review highlighted fields.";
            viewModel.StatusSucceeded = false;

            if (IsAjaxRequest())
            {
                var details = new ValidationProblemDetails(ModelState)
                {
                    Title = "AI settings guardrails blocked save",
                    Detail = viewModel.StatusMessage,
                    Status = StatusCodes.Status400BadRequest
                };

                return BadRequest(details);
            }

            return View("Index", viewModel);
        }

        AiBehaviorProfile savedProfile;

        try
        {
            savedProfile = await _aiBehaviorSettingsService.SaveAsync(
                new AiBehaviorProfile(
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
                await ResolveOpenAiConnectionStateAsync(cancellationToken));
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

        var statusMessage =
            $"Saved AI behavior settings with {AiOutputLanguage.GetDisplayName(savedProfile.OutputLanguageCode)} output.";
        if (guardrailEvaluation.SoftWarnings.Count > 0)
        {
            statusMessage += $" Warning: {string.Join(' ', guardrailEvaluation.SoftWarnings)}";
        }

        TempData["AiSettingsStatusMessage"] = statusMessage;
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
    public async Task<IActionResult> ConnectionStatus(CancellationToken cancellationToken)
    {
        var payload = await ResolveOpenAiConnectionStateAsync(cancellationToken);

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

    private async Task<OpenAiConnectionStateData> ResolveOpenAiConnectionStateAsync(CancellationToken cancellationToken)
    {
        var effectiveSecurityOptions = await _openAiEffectiveSecurityOptionsResolver.ResolveAsync(cancellationToken);
        return AiSettingsViewModelAdapter.CreateConnectionState(effectiveSecurityOptions);
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            HttpContext?.Request.Headers.XRequestedWith.ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private void AddGuardrailBlockingErrors(AiBehaviorGuardrailEvaluation evaluation)
    {
        foreach (var (field, messages) in evaluation.BlockingErrors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }
    }
}
