using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class ConfigurationReadinessHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsyncReturnsHealthyWhenRequiredConfigurationExists()
    {
        var healthCheck = new ConfigurationReadinessHealthCheck(
            Options.Create(new SqlServerOptions
            {
                ConnectionString = "Server=.;Database=LinkedInJobScraper;Trusted_Connection=True;"
            }),
            Options.Create(new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini"
            }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "configuration_readiness",
                healthCheck,
                HealthStatus.Degraded,
                tags: ["ready"])
        });

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Configuration readiness checks passed.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsyncReturnsFailureStatusWhenConfigurationIsMissing()
    {
        var healthCheck = new ConfigurationReadinessHealthCheck(
            Options.Create(new SqlServerOptions()),
            Options.Create(new OpenAiSecurityOptions()));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "configuration_readiness",
                healthCheck,
                HealthStatus.Degraded,
                tags: ["ready"])
        });

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Description);
        Assert.Contains("SQL Server connection string", result.Description, StringComparison.Ordinal);
    }
}
