namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class OpenAiSecurityOptions
{
    public const string SectionName = "OpenAI:Security";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = string.Empty;

    public string? ValidateForScoring()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return "OpenAI API key is not configured. Set 'OpenAI:Security:ApiKey' with dotnet user-secrets for src/LinkedIn.JobScraper.Web or provide it via environment variables.";
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            return "OpenAI model is not configured. Set 'OpenAI:Security:Model' with dotnet user-secrets for src/LinkedIn.JobScraper.Web or provide it via environment variables.";
        }

        return null;
    }
}
