namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class LinkedInRequestOptions
{
    public const string SectionName = "LinkedIn:RequestOptions";

    public string GraphQlQueryId { get; set; } = string.Empty;
}
