using System.Reflection;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class ControllerAuthorizationTests
{
    [Theory]
    [InlineData(typeof(JobsController))]
    [InlineData(typeof(AiSettingsController))]
    [InlineData(typeof(SearchSettingsController))]
    [InlineData(typeof(LinkedInSessionController))]
    [InlineData(typeof(DiagnosticsController))]
    public void ProtectedControllersRequireAppCookieAuthentication(Type controllerType)
    {
        var attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AppAuthenticationDefaults.CookieScheme, attribute.AuthenticationSchemes);
    }

    [Fact]
    public void AccountLoginActionsAllowAnonymousAccess()
    {
        var getLogin = typeof(AccountController).GetMethod(nameof(AccountController.Login), [typeof(string)]);
        var postLogin = typeof(AccountController).GetMethod(nameof(AccountController.Login), [typeof(LoginPageViewModel), typeof(CancellationToken)]);

        Assert.NotNull(getLogin?.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(postLogin?.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
