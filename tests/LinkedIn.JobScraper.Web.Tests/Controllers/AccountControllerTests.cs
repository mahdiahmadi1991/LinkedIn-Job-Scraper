using System.Security.Claims;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task LoginPostReturnsViewWhenCredentialsAreInvalid()
    {
        var controller = CreateController(new FakeAuthenticationService(false));

        var result = await controller.Login(
            new LoginPageViewModel
            {
                UserName = "owner",
                Password = "bad-password"
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginPageViewModel>(view.Model);

        Assert.Equal(string.Empty, model.Password);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task LoginPostSignsInAndUsesPersistentCookieWhenRememberMeIsSelected()
    {
        var authenticationService = new FakeAuthenticationService(true);
        var httpAuthenticationService = new CapturingHttpAuthenticationService();
        var controller = CreateController(authenticationService, httpAuthenticationService);

        var result = await controller.Login(
            new LoginPageViewModel
            {
                UserName = "owner",
                Password = "Passw0rd!",
                RememberMe = true,
                ReturnUrl = "/Jobs?sortBy=score"
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);

        Assert.Equal("/Jobs?sortBy=score", redirect.Url);
        Assert.Equal(AppAuthenticationDefaults.CookieScheme, httpAuthenticationService.LastScheme);
        Assert.NotNull(httpAuthenticationService.LastProperties);
        Assert.True(httpAuthenticationService.LastProperties!.IsPersistent);
        Assert.NotNull(httpAuthenticationService.LastProperties.ExpiresUtc);
    }

    [Fact]
    public void LoginGetRedirectsAuthenticatedUsersToSafeReturnUrl()
    {
        var controller = CreateController(new FakeAuthenticationService(false));
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "owner")],
                AppAuthenticationDefaults.CookieScheme));

        var result = controller.Login("//malicious.example");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Jobs", redirect.Url);
    }

    [Fact]
    public async Task LogoutPostSignsOutAndRedirectsToLogin()
    {
        var httpAuthenticationService = new CapturingHttpAuthenticationService();
        var controller = CreateController(new FakeAuthenticationService(false), httpAuthenticationService);

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectResult>(result);

        Assert.Equal("/Account/Login", redirect.Url);
        Assert.Equal(AppAuthenticationDefaults.CookieScheme, httpAuthenticationService.LastSignOutScheme);
    }

    private static AccountController CreateController(
        IAppUserAuthenticationService authenticationService,
        IAuthenticationService? httpAuthenticationService = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAuthenticationService>(httpAuthenticationService ?? new CapturingHttpAuthenticationService());
        serviceCollection.AddSingleton<ITempDataDictionaryFactory, TestTempDataDictionaryFactory>();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceCollection.BuildServiceProvider()
        };

        return new AccountController(authenticationService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private sealed class TestTempDataDictionaryFactory : ITempDataDictionaryFactory
    {
        public ITempDataDictionary GetTempData(HttpContext context)
        {
            return new TempDataDictionary(context, new TestTempDataProvider());
        }
    }

    private sealed class FakeAuthenticationService : IAppUserAuthenticationService
    {
        private readonly bool _shouldSucceed;

        public FakeAuthenticationService(bool shouldSucceed)
        {
            _shouldSucceed = shouldSucceed;
        }

        public Task<AppUserAuthenticationResult> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
        {
            if (!_shouldSucceed)
            {
                return Task.FromResult(new AppUserAuthenticationResult(false, "The username or password is incorrect.", null));
            }

            return Task.FromResult(
                new AppUserAuthenticationResult(
                    true,
                    "Authentication succeeded.",
                    new AppUserIdentity(1, userName, "Local Owner")));
        }

        public ClaimsPrincipal CreatePrincipal(AppUserIdentity user)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, user.UserName)],
                AppAuthenticationDefaults.CookieScheme);

            return new ClaimsPrincipal(identity);
        }
    }

    private sealed class CapturingHttpAuthenticationService : IAuthenticationService
    {
        public string? LastScheme { get; private set; }

        public string? LastSignOutScheme { get; private set; }

        public AuthenticationProperties? LastProperties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            throw new NotSupportedException();
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            LastScheme = scheme;
            LastProperties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            LastSignOutScheme = scheme;
            return Task.CompletedTask;
        }
    }
}
