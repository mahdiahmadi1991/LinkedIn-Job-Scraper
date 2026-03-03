using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class ConfigurationReadinessValidatorTests
{
    [Fact]
    public void GetWarningsReturnsExpectedMessagesForMissingSettings()
    {
        var warnings = ConfigurationReadinessValidator.GetWarnings(
            new SqlServerOptions(),
            new OpenAiSecurityOptions());

        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, warning => warning.Contains("SQL Server connection string", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("OpenAI API key", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("OpenAI model", StringComparison.Ordinal));
    }

    [Fact]
    public void GetWarningsReturnsNoMessagesWhenRequiredSettingsExist()
    {
        var warnings = ConfigurationReadinessValidator.GetWarnings(
            new SqlServerOptions
            {
                ConnectionString = "Server=.;Database=LinkedInJobScraper;Trusted_Connection=True;"
            },
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini"
            });

        Assert.Empty(warnings);
    }
}
