using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.AI;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AdminControllerTests
{
    [Fact]
    public async Task IndexRedirectsToCanonicalUsersTabWhenTabIsMissing()
    {
        var controller = CreateController(new FakeAdminUserManagementService());

        var result = await controller.Index(null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
    }

    [Fact]
    public async Task IndexRedirectsToCanonicalUsersTabWhenTabIsUnknown()
    {
        var controller = CreateController(new FakeAdminUserManagementService());

        var result = await controller.Index("unknown", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
    }

    [Fact]
    public async Task IndexRedirectsToCanonicalOpenAiTabWhenTabCasingDiffers()
    {
        var controller = CreateController(new FakeAdminUserManagementService());

        var result = await controller.Index("OpenAI", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(AdminController.OpenAiTab, redirect.RouteValues?["tab"]);
    }

    [Fact]
    public async Task IndexReturnsViewForUsersTab()
    {
        var service = new FakeAdminUserManagementService
        {
            Users =
            [
                new AdminUserListItem(
                    1,
                    "admin@mahdiahmadi.dev",
                    "Super Admin",
                    true,
                    true,
                    null,
                    DateTimeOffset.UtcNow.AddDays(-10),
                    DateTimeOffset.UtcNow.AddDays(-1))
            ]
        };
        var controller = CreateController(service);
        controller.TempData["AdminUsersStatusMessage"] = "Saved.";
        controller.TempData["AdminUsersStatusSucceeded"] = bool.TrueString;

        var result = await controller.Index(AdminController.UsersTab, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/AdminUsers/Index.cshtml", view.ViewName);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);
        Assert.Single(model.Users);
        Assert.Equal(AdminController.UsersTab, model.ActiveTab);
        Assert.Equal("gpt-5-mini", model.OpenAiSetupForm.Model);
        Assert.Equal("Saved.", model.StatusMessage);
        Assert.True(model.StatusSucceeded);
    }

    [Fact]
    public async Task IndexReturnsViewForOpenAiTab()
    {
        var service = new FakeAdminUserManagementService();
        var controller = CreateController(service);

        var result = await controller.Index(AdminController.OpenAiTab, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/AdminUsers/Index.cshtml", view.ViewName);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);
        Assert.Equal(AdminController.OpenAiTab, model.ActiveTab);
        Assert.Equal("gpt-5-mini", model.OpenAiSetupForm.Model);
    }

    [Fact]
    public async Task SaveOpenAiSettingsReturnsJsonForAjaxSuccess()
    {
        var runtimeSettingsService = new FakeOpenAiRuntimeSettingsService();
        var runtimeApiKeyService = new FixedOpenAiRuntimeApiKeyService("initial-key");
        var controller = CreateController(
            new FakeAdminUserManagementService(),
            runtimeSettingsService: runtimeSettingsService,
            runtimeApiKeyService: runtimeApiKeyService);
        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.SaveOpenAiSettings(
            new AdminOpenAiSetupFormViewModel
            {
                ApiKey = "updated-key",
                Model = "gpt-5",
                BaseUrl = "https://api.openai.com/v1",
                RequestTimeoutSeconds = 50,
                UseBackgroundMode = true,
                BackgroundPollingIntervalMilliseconds = 1500,
                BackgroundPollingTimeoutSeconds = 120,
                MaxConcurrentScoringRequests = 2
            },
            CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<SettingsSaveResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal("/admin?tab=openai", payload.RedirectUrl);
        Assert.Equal("token-saved", payload.ConcurrencyToken);
        Assert.Equal("gpt-5", runtimeSettingsService.LastSavedProfile?.Model);
        Assert.Equal("updated-key", runtimeApiKeyService.CurrentApiKey);
    }

    [Fact]
    public async Task SaveOpenAiSettingsReturnsValidationProblemForAjaxInvalidModel()
    {
        var controller = CreateController(new FakeAdminUserManagementService(), runtimeSettingsService: new FakeOpenAiRuntimeSettingsService());
        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        controller.ModelState.AddModelError(nameof(AdminOpenAiSetupFormViewModel.Model), "Required");

        var result = await controller.SaveOpenAiSettings(
            new AdminOpenAiSetupFormViewModel(),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("OpenAI setup validation failed", details.Title);
    }

    [Fact]
    public async Task SaveOpenAiSettingsReturnsValidationProblemWhenProbeFails()
    {
        var controller = CreateController(
            new FakeAdminUserManagementService(),
            probeService: new FixedOpenAiConnectionProbeService(
                new OpenAiConnectionProbeResult(false, "OpenAI API key was rejected by the server.")));
        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.SaveOpenAiSettings(
            new AdminOpenAiSetupFormViewModel
            {
                ApiKey = "sk-valid-length-key-for-probe-test",
                Model = "gpt-5-mini",
                BaseUrl = "https://api.openai.com/v1",
                RequestTimeoutSeconds = 45,
                UseBackgroundMode = true,
                BackgroundPollingIntervalMilliseconds = 1500,
                BackgroundPollingTimeoutSeconds = 120,
                MaxConcurrentScoringRequests = 2
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Contains(nameof(AdminOpenAiSetupFormViewModel.ApiKey), details.Errors.Keys);
    }

    [Fact]
    public async Task OpenAiConnectionStatusReturnsProblemWhenApiKeyIsMissing()
    {
        var controller = CreateController(
            new FakeAdminUserManagementService(),
            runtimeSettingsService: new FakeOpenAiRuntimeSettingsService(),
            openAiSecurityOptions: new OpenAiSecurityOptions
            {
                ApiKey = string.Empty
            });

        var result = await controller.OpenAiConnectionStatus(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("AI connection is not ready", details.Title);
    }

    [Fact]
    public async Task OpenAiConnectionStatusReturnsJsonWhenReady()
    {
        var controller = CreateController(
            new FakeAdminUserManagementService(),
            runtimeSettingsService: new FakeOpenAiRuntimeSettingsService(),
            openAiSecurityOptions: new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                BaseUrl = "https://api.openai.com/v1"
            });

        var result = await controller.OpenAiConnectionStatus(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiConnectionStatusResponse>(json.Value);
        Assert.True(payload.Success);
        Assert.True(payload.State.Ready);
        Assert.Equal("gpt-5-mini", payload.State.Model);
    }

    [Fact]
    public async Task OpenAiConnectionStatusDraftUsesDraftModelAndBaseUrl()
    {
        var controller = CreateController(
            new FakeAdminUserManagementService(),
            probeService: new FixedOpenAiConnectionProbeService(
                new OpenAiConnectionProbeResult(true, "OpenAI API key verification succeeded.")));

        var result = await controller.OpenAiConnectionStatusDraft(
            new AdminOpenAiSetupFormViewModel
            {
                ApiKey = "sk-test-key-value",
                Model = "gpt-4.1-mini",
                BaseUrl = "https://api.openai.com/v1"
            },
            CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiConnectionStatusResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal("gpt-4.1-mini", payload.State.Model);
        Assert.Equal("https://api.openai.com/v1", payload.State.BaseUrl);
        Assert.True(payload.State.Ready);
    }

    [Fact]
    public async Task OpenAiConnectionStatusDraftReturnsValidationProblemWhenModelStateIsInvalid()
    {
        var controller = CreateController(new FakeAdminUserManagementService());
        controller.ModelState.AddModelError(nameof(AdminOpenAiSetupFormViewModel.Model), "Required");

        var result = await controller.OpenAiConnectionStatusDraft(
            new AdminOpenAiSetupFormViewModel(),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("OpenAI readiness check validation failed", details.Title);
    }

    private static AdminController CreateController(
        IAdminUserManagementService userManagementService,
        IOpenAiConnectionProbeService? probeService = null,
        IOpenAiRuntimeSettingsService? runtimeSettingsService = null,
        IOpenAiRuntimeApiKeyService? runtimeApiKeyService = null,
        OpenAiSecurityOptions? openAiSecurityOptions = null)
    {
        var httpContext = new DefaultHttpContext();
        return new AdminController(
            userManagementService,
            probeService ?? new FixedOpenAiConnectionProbeService(new OpenAiConnectionProbeResult(true, "ok")),
            runtimeSettingsService ?? new FakeOpenAiRuntimeSettingsService(),
            runtimeApiKeyService ?? new FixedOpenAiRuntimeApiKeyService("test-key"),
            new FixedOpenAiEffectiveSecurityOptionsResolver(
                openAiSecurityOptions ?? new OpenAiSecurityOptions
                {
                    ApiKey = "test-key",
                    Model = "gpt-5-mini",
                    BaseUrl = "https://api.openai.com/v1"
                }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private sealed class FakeOpenAiRuntimeSettingsService : IOpenAiRuntimeSettingsService
    {
        public OpenAiRuntimeSettingsProfile ActiveProfile { get; set; } =
            new(
                "gpt-5-mini",
                "https://api.openai.com/v1",
                45,
                true,
                1500,
                120,
                2,
                "token-1");

        public OpenAiRuntimeSettingsProfile? LastSavedProfile { get; private set; }

        public Task<OpenAiRuntimeSettingsProfile> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ActiveProfile);
        }

        public Task<OpenAiRuntimeSettingsProfile> SaveAsync(
            OpenAiRuntimeSettingsProfile profile,
            CancellationToken cancellationToken)
        {
            LastSavedProfile = profile;
            ActiveProfile = profile with { ConcurrencyToken = "token-saved" };
            return Task.FromResult(ActiveProfile);
        }
    }

    private sealed class FixedOpenAiConnectionProbeService : IOpenAiConnectionProbeService
    {
        public FixedOpenAiConnectionProbeService(OpenAiConnectionProbeResult result)
        {
            Result = result;
        }

        public OpenAiConnectionProbeResult Result { get; set; }

        public Task<OpenAiConnectionProbeResult> ProbeAsync(
            string apiKey,
            string baseUrl,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAdminUserManagementService : IAdminUserManagementService
    {
        public IReadOnlyList<AdminUserListItem> Users { get; set; } = [];

        public Task<IReadOnlyList<AdminUserListItem>> GetUsersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Users);
        }

        public Task<AdminUserCreateResult> CreateUserAsync(AdminUserCreateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserCreateResult(false, "Not configured.", null));
        }

        public Task<AdminUserUpdateResult> UpdateUserProfileAsync(AdminUserUpdateProfileRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserUpdateResult(false, "Not configured.", null));
        }

        public Task<AdminUserUpdateResult> SetUserActiveStateAsync(AdminUserSetActiveStateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserUpdateResult(false, "Not configured.", null));
        }

        public Task<AdminUserDeleteResult> SoftDeleteUserAsync(AdminUserSoftDeleteRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserDeleteResult(false, "Not configured.", null, null));
        }
    }
}
