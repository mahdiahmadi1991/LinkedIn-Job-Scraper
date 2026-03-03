using LinkedIn.JobScraper.Web.LinkedIn;

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class LinkedInFetchLimitsOptions
{
    public const string SectionName = "LinkedIn:FetchLimits";

    public int? SearchPageCap { get; set; }

    public int? SearchJobCap { get; set; }

    public int GetSearchPageCap() => SearchPageCap is > 0
        ? SearchPageCap.Value
        : LinkedInRequestDefaults.DefaultSearchPageCap;

    public int GetSearchJobCap() => SearchJobCap is > 0
        ? SearchJobCap.Value
        : LinkedInRequestDefaults.DefaultSearchJobCap;
}
