using System.Security.Claims;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Composition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Authentication;

public sealed class AppAuthorizationPolicyTests
{
    [Fact]
    public void SuperAdminOnlyPolicyIsRegistered()
    {
        using var provider = BuildServices();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var policy = options.GetPolicy(AppAuthorizationPolicies.SuperAdminOnly);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is ClaimsAuthorizationRequirement claimsRequirement &&
                           string.Equals(claimsRequirement.ClaimType, AppUserClaimTypes.IsSuperAdmin, StringComparison.Ordinal) &&
                           claimsRequirement.AllowedValues?.Contains("true") == true);
    }

    [Fact]
    public async Task SuperAdminOnlyPolicyAuthorizesOnlySuperAdminClaim()
    {
        using var provider = BuildServices();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var superAdminPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(AppUserClaimTypes.IsSuperAdmin, "true")],
                AppAuthenticationDefaults.CookieScheme));

        var nonSuperAdminPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(AppUserClaimTypes.IsSuperAdmin, "false")],
                AppAuthenticationDefaults.CookieScheme));

        var superAdminResult = await authorizationService.AuthorizeAsync(
            superAdminPrincipal,
            resource: null,
            AppAuthorizationPolicies.SuperAdminOnly);
        var nonSuperAdminResult = await authorizationService.AuthorizeAsync(
            nonSuperAdminPrincipal,
            resource: null,
            AppAuthorizationPolicies.SuperAdminOnly);

        Assert.True(superAdminResult.Succeeded);
        Assert.False(nonSuperAdminResult.Succeeded);
    }

    [Fact]
    public async Task SuperAdminOnlyPolicyRejectsAnonymousPrincipal()
    {
        using var provider = BuildServices();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await authorizationService.AuthorizeAsync(
            anonymousPrincipal,
            resource: null,
            AppAuthorizationPolicies.SuperAdminOnly);

        Assert.False(result.Succeeded);
    }

    private static ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvpApplication(configuration);

        return services.BuildServiceProvider();
    }
}
