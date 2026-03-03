namespace LinkedIn.JobScraper.Web.Configuration;

public static class ConfigurationReadinessValidator
{
    public static IReadOnlyList<string> GetWarnings(
        SqlServerOptions sqlServerOptions,
        OpenAiSecurityOptions openAiSecurityOptions)
    {
        ArgumentNullException.ThrowIfNull(sqlServerOptions);
        ArgumentNullException.ThrowIfNull(openAiSecurityOptions);

        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(sqlServerOptions.ConnectionString))
        {
            warnings.Add(
                "SQL Server connection string is not configured. Database-backed features will stay unavailable until SqlServer:ConnectionString is provided via user-secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(openAiSecurityOptions.ApiKey))
        {
            warnings.Add(
                "OpenAI API key is not configured. AI scoring will stay unavailable until OpenAI:Security:ApiKey is provided via user-secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(openAiSecurityOptions.Model))
        {
            warnings.Add(
                "OpenAI model is not configured. AI scoring will stay unavailable until OpenAI:Security:Model is provided via user-secrets or environment variables.");
        }

        return warnings;
    }
}
