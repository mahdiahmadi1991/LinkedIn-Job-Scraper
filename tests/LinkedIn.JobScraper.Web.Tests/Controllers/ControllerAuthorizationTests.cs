using System.Reflection;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class ControllerAuthorizationTests
{
    [Theory]
    [InlineData(typeof(JobsController))]
    [InlineData(typeof(AiSettingsController))]
    [InlineData(typeof(SearchSettingsController))]
    [InlineData(typeof(LinkedInSessionController))]
    [InlineData(typeof(DiagnosticsController))]
    [InlineData(typeof(AdminController))]
    [InlineData(typeof(AdminUsersController))]
    public void ProtectedControllersRequireAppCookieAuthentication(Type controllerType)
    {
        var attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AppAuthenticationDefaults.CookieScheme, attribute.AuthenticationSchemes);
    }

    [Fact]
    public void AdminControllerRequiresSuperAdminPolicy()
    {
        var attribute = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AppAuthorizationPolicies.SuperAdminOnly, attribute.Policy);
    }

    [Fact]
    public void AdminControllerHasStableAdminRoute()
    {
        var routeAttribute = typeof(AdminController).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(routeAttribute);
        Assert.Equal("admin", routeAttribute!.Template);
    }

    [Fact]
    public void AdminUsersControllerRequiresSuperAdminPolicy()
    {
        var attribute = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AppAuthorizationPolicies.SuperAdminOnly, attribute.Policy);
    }

    [Fact]
    public void AdminUsersControllerHasStableAdminRoute()
    {
        var routeAttribute = typeof(AdminUsersController).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(routeAttribute);
        Assert.Equal("admin/users", routeAttribute!.Template);
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
