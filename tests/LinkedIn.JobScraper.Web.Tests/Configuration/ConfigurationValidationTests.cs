using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void SqlServerOptionsThrowsActionableMessageWhenConnectionStringIsMissing()
    {
        var options = new SqlServerOptions
        {
            ConnectionString = ""
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetRequiredConnectionString());

        Assert.Contains("SqlServer:ConnectionString", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets set", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredSqlServerConnectionStringProviderUsesOptionsValidation()
    {
        var provider = new ConfiguredSqlServerConnectionStringProvider(
            Options.Create(
                new SqlServerOptions
                {
                    ConnectionString = ""
                }));

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredConnectionString());

        Assert.Contains("SqlServer:ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsActionableMessageWhenApiKeyIsMissing()
    {
        var gateway = new OpenAiJobScoringGateway(
            new HttpClient(),
            Options.Create(
                new OpenAiSecurityOptions
                {
                    ApiKey = "",
                    Model = "gpt-5-mini"
                }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiJobScoringGateway>.Instance);

        var result = await gateway.ScoreAsync(
            new JobScoringGatewayRequest(
                "Title",
                "Description",
                "Behavior",
                "Priority",
                "Exclusion",
                "en",
                "Company",
                "Location",
                "Full-time"),
            CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Contains("OpenAI:Security:ApiKey", result.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsActionableMessageWhenModelIsMissing()
    {
        var gateway = new OpenAiJobScoringGateway(
            new HttpClient(),
            Options.Create(
                new OpenAiSecurityOptions
                {
                    ApiKey = "test-key",
                    Model = ""
                }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiJobScoringGateway>.Instance);

        var result = await gateway.ScoreAsync(
            new JobScoringGatewayRequest(
                "Title",
                "Description",
                "Behavior",
                "Priority",
                "Exclusion",
                "en",
                "Company",
                "Location",
                "Full-time"),
            CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Contains("OpenAI:Security:Model", result.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets", result.Message, StringComparison.Ordinal);
    }
}
