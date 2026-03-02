namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class OpenAiSecurityOptions
{
    public const string SectionName = "OpenAI:Security";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = string.Empty;
}
