using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(
    AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme,
    Policy = AppAuthorizationPolicies.SuperAdminOnly)]
[Route("admin")]
public sealed class AdminController : Controller
{
    public const string UsersTab = "users";
    public const string OpenAiTab = "openai";

    private readonly IAdminUserManagementService _adminUserManagementService;
    private readonly IOpenAiConnectionProbeService _openAiConnectionProbeService;
    private readonly IOpenAiRuntimeApiKeyService _openAiRuntimeApiKeyService;
    private readonly IOpenAiEffectiveSecurityOptionsResolver _openAiEffectiveSecurityOptionsResolver;
    private readonly IOpenAiRuntimeSettingsService _openAiRuntimeSettingsService;

    public AdminController(
        IAdminUserManagementService adminUserManagementService,
        IOpenAiConnectionProbeService openAiConnectionProbeService,
        IOpenAiRuntimeSettingsService openAiRuntimeSettingsService,
        IOpenAiRuntimeApiKeyService openAiRuntimeApiKeyService,
        IOpenAiEffectiveSecurityOptionsResolver openAiEffectiveSecurityOptionsResolver)
    {
        _adminUserManagementService = adminUserManagementService;
        _openAiConnectionProbeService = openAiConnectionProbeService;
        _openAiRuntimeSettingsService = openAiRuntimeSettingsService;
        _openAiRuntimeApiKeyService = openAiRuntimeApiKeyService;
        _openAiEffectiveSecurityOptionsResolver = openAiEffectiveSecurityOptionsResolver;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        var normalizedTab = NormalizeTab(tab);
        if (!string.Equals(normalizedTab, tab, StringComparison.Ordinal))
        {
            return RedirectToAction(nameof(Index), new { tab = normalizedTab });
        }

        var viewModel = await BuildPageViewModelAsync(normalizedTab, cancellationToken);
        return View("~/Views/AdminUsers/Index.cshtml", viewModel);
    }

    [HttpPost("openai-settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOpenAiSettings(
        [Bind(Prefix = "OpenAiSetupForm")] AdminOpenAiSetupFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                var details = new ValidationProblemDetails(ModelState)
                {
                    Title = "OpenAI setup validation failed",
                    Detail = "All OpenAI setup fields must be valid before saving.",
                    Status = StatusCodes.Status400BadRequest
                };

                return BadRequest(details);
            }

            var pageModel = await BuildPageViewModelAsync(OpenAiTab, cancellationToken);
            pageModel.OpenAiSetupForm = viewModel;
            pageModel.OpenAiStatusMessage = "All OpenAI setup fields must be valid before saving.";
            pageModel.OpenAiStatusSucceeded = false;
            return View("~/Views/AdminUsers/Index.cshtml", pageModel);
        }

        var probeResult = await _openAiConnectionProbeService.ProbeAsync(
            viewModel.ApiKey,
            viewModel.BaseUrl,
            cancellationToken);
        if (!probeResult.Success)
        {
            ModelState.AddModelError(nameof(AdminOpenAiSetupFormViewModel.ApiKey), probeResult.Message);

            if (IsAjaxRequest())
            {
                var details = new ValidationProblemDetails(ModelState)
                {
                    Title = "OpenAI setup validation failed",
                    Detail = probeResult.Message,
                    Status = StatusCodes.Status400BadRequest
                };

                return BadRequest(details);
            }

            var pageModel = await BuildPageViewModelAsync(OpenAiTab, cancellationToken);
            pageModel.OpenAiSetupForm = viewModel;
            pageModel.OpenAiStatusMessage = probeResult.Message;
            pageModel.OpenAiStatusSucceeded = false;
            return View("~/Views/AdminUsers/Index.cshtml", pageModel);
        }

        OpenAiRuntimeSettingsProfile savedProfile;

        try
        {
            savedProfile = await _openAiRuntimeSettingsService.SaveAsync(
                AdminOpenAiSetupViewModelAdapter.ToProfile(viewModel),
                cancellationToken);
            await _openAiRuntimeApiKeyService.SaveAsync(viewModel.ApiKey, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            if (IsAjaxRequest())
            {
                var statusCode = IsConcurrencyConflict(exception)
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;

                return Problem(
                    title: "OpenAI setup save failed",
                    detail: exception.Message,
                    statusCode: statusCode);
            }

            var pageModel = await BuildPageViewModelAsync(OpenAiTab, cancellationToken);
            pageModel.OpenAiSetupForm = viewModel;
            pageModel.OpenAiStatusMessage = exception.Message;
            pageModel.OpenAiStatusSucceeded = false;
            return View("~/Views/AdminUsers/Index.cshtml", pageModel);
        }

        TempData["AdminOpenAiStatusMessage"] = "OpenAI runtime settings were saved.";
        TempData["AdminOpenAiStatusSucceeded"] = bool.TrueString;

        if (IsAjaxRequest())
        {
            var redirectUrl = Url?.Action(nameof(Index), new { tab = OpenAiTab }) ?? "/admin?tab=openai";
            return Json(
                new SettingsSaveResponse(
                    true,
                    TempData["AdminOpenAiStatusMessage"] as string ?? "OpenAI runtime settings were saved.",
                    redirectUrl,
                    savedProfile.ConcurrencyToken));
        }

        return RedirectToAction(nameof(Index), new { tab = OpenAiTab });
    }

    [HttpGet("openai-connection-status")]
    public async Task<IActionResult> OpenAiConnectionStatus(CancellationToken cancellationToken)
    {
        var effectiveSecurityOptions = await _openAiEffectiveSecurityOptionsResolver.ResolveAsync(cancellationToken);
        var payload = AdminOpenAiSetupViewModelAdapter.CreateConnectionState(effectiveSecurityOptions);

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

    [HttpPost("openai-connection-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenAiConnectionStatusDraft(
        [Bind(Prefix = "OpenAiSetupForm")] AdminOpenAiSetupFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var details = new ValidationProblemDetails(ModelState)
            {
                Title = "OpenAI readiness check validation failed",
                Detail = "OpenAI setup fields must be valid before readiness check.",
                Status = StatusCodes.Status400BadRequest
            };

            return BadRequest(details);
        }

        var probeResult = await _openAiConnectionProbeService.ProbeAsync(
            viewModel.ApiKey,
            viewModel.BaseUrl,
            cancellationToken);
        if (!probeResult.Success)
        {
            return Problem(
                title: "AI connection is not ready",
                detail: probeResult.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var normalizedModel = string.IsNullOrWhiteSpace(viewModel.Model)
            ? null
            : viewModel.Model.Trim();
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(viewModel.BaseUrl)
            ? "https://api.openai.com/v1"
            : viewModel.BaseUrl.Trim().TrimEnd('/');

        return Json(
            new AiConnectionStatusResponse(
                true,
                probeResult.Message,
                new AiConnectionStateResponse(
                    !string.IsNullOrWhiteSpace(viewModel.ApiKey),
                    normalizedModel,
                    normalizedBaseUrl,
                    true)));
    }

    private async Task<AdminUsersPageViewModel> BuildPageViewModelAsync(
        string activeTab,
        CancellationToken cancellationToken)
    {
        var usersTask = _adminUserManagementService.GetUsersAsync(cancellationToken);
        var runtimeSettingsTask = _openAiRuntimeSettingsService.GetActiveAsync(cancellationToken);
        var apiKeyTask = _openAiRuntimeApiKeyService.GetActiveAsync(cancellationToken);
        await Task.WhenAll(usersTask, runtimeSettingsTask, apiKeyTask);

        var runtimeSettings = runtimeSettingsTask.Result;
        var runtimeApiKey = apiKeyTask.Result;
        var effectiveSecurityOptions = await _openAiEffectiveSecurityOptionsResolver.ResolveAsync(
            runtimeSettings,
            runtimeApiKey,
            cancellationToken);
        var connectionState = AdminOpenAiSetupViewModelAdapter.CreateConnectionState(effectiveSecurityOptions);
        var openAiSetupForm = AdminOpenAiSetupViewModelAdapter.ToViewModel(runtimeSettings);
        openAiSetupForm.ApiKey = runtimeApiKey ?? string.Empty;

        var viewModel = new AdminUsersPageViewModel
        {
            ActiveTab = activeTab,
            OpenAiSetupForm = openAiSetupForm,
            OpenAiStatusMessage = TempData["AdminOpenAiStatusMessage"] as string,
            OpenAiStatusSucceeded = string.Equals(
                TempData["AdminOpenAiStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase),
            CreateForm = new AdminUserCreateFormViewModel
            {
                IsActive = true
            },
            UpdateForm = new AdminUserUpdateFormViewModel(),
            ToggleActiveForm = new AdminUserSetActiveStateFormViewModel(),
            SoftDeleteForm = new AdminUserSoftDeleteFormViewModel(),
            Users = usersTask.Result.Select(
                    static user => new AdminUserListItemViewModel(
                        user.Id,
                        user.UserName,
                        user.DisplayName,
                        user.IsActive,
                        user.IsSuperAdmin,
                        user.ExpiresAtUtc,
                        user.CreatedAtUtc,
                        user.UpdatedAtUtc))
                .ToArray(),
            StatusMessage = TempData["AdminUsersStatusMessage"] as string,
            StatusSucceeded = string.Equals(
                TempData["AdminUsersStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase)
        };

        AdminOpenAiSetupViewModelAdapter.PopulateConnectionStatus(viewModel, connectionState);
        return viewModel;
    }

    private static bool IsConcurrencyConflict(InvalidOperationException exception)
    {
        return exception.Message.Contains("updated by another operation", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            HttpContext?.Request.Headers.XRequestedWith.ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
        {
            return UsersTab;
        }

        var normalizedTab = tab.Trim().ToLowerInvariant();
        return normalizedTab switch
        {
            UsersTab => UsersTab,
            OpenAiTab => OpenAiTab,
            _ => UsersTab
        };
    }
}
