namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class LinkedInBrowserAutomationOptions
{
    public const string SectionName = "LinkedIn:BrowserAutomation";

    public string BrowserChannel { get; set; } = string.Empty;

    public bool Headless { get; set; }

    public string LoginUrl { get; set; } = "https://www.linkedin.com/login";

    public string LinkedInBaseUrl { get; set; } = "https://www.linkedin.com";

    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

    public string AcceptLanguage { get; set; } = "en-US,en;q=0.9";

    public string LinkedInLanguage { get; set; } = "en_US";
}
