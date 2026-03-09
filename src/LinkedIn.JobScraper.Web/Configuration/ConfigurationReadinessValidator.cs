namespace LinkedIn.JobScraper.Web.Configuration;

public static class ConfigurationReadinessValidator
{
    public static IReadOnlyList<string> GetWarnings(
        OpenAiSecurityOptions openAiSecurityOptions)
    {
        ArgumentNullException.ThrowIfNull(openAiSecurityOptions);

        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(openAiSecurityOptions.ApiKey))
        {
            warnings.Add(
                "OpenAI API key is not configured. AI scoring will stay unavailable until Administration > OpenAI Setup is completed.");
        }

        if (string.IsNullOrWhiteSpace(openAiSecurityOptions.Model))
        {
            warnings.Add(
                "OpenAI model is not configured. AI scoring will stay unavailable until Administration > OpenAI Setup is completed.");
        }

        return warnings;
    }
}
