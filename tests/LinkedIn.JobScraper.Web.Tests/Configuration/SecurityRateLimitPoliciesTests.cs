using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Controllers;
using Microsoft.AspNetCore.RateLimiting;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class SecurityRateLimitPoliciesTests
{
    [Fact]
    public void SensitiveLocalActionsPolicyHasStableName()
    {
        Assert.Equal("SensitiveLocalActions", SecurityRateLimitPolicies.SensitiveLocalActions);
    }

    [Fact]
    public void FetchAndScoreActionHasSensitiveRateLimitPolicy()
    {
        var method = typeof(JobsController).GetMethod(nameof(JobsController.FetchAndScore));

        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true))
            as EnableRateLimitingAttribute;

        Assert.NotNull(attribute);
        Assert.Equal(SecurityRateLimitPolicies.SensitiveLocalActions, attribute.PolicyName);
    }

    [Theory]
    [InlineData(nameof(LinkedInSessionController.Capture))]
    [InlineData(nameof(LinkedInSessionController.Launch))]
    [InlineData(nameof(LinkedInSessionController.Verify))]
    [InlineData(nameof(LinkedInSessionController.Revoke))]
    public void LinkedInSessionSensitiveActionsHaveSensitiveRateLimitPolicy(string methodName)
    {
        var method = typeof(LinkedInSessionController).GetMethod(methodName);

        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true))
            as EnableRateLimitingAttribute;

        Assert.NotNull(attribute);
        Assert.Equal(SecurityRateLimitPolicies.SensitiveLocalActions, attribute.PolicyName);
    }
}
