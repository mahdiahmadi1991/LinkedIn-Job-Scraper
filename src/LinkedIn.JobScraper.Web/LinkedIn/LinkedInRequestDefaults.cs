namespace LinkedIn.JobScraper.Web.LinkedIn;

public static class LinkedInRequestDefaults
{
    public const string SearchDecorationId =
        "com.linkedin.voyager.dash.deco.jobs.search.JobSearchCardsCollection-220";

    public const string SearchPemMetadata =
        "Voyager - Careers - Jobs Search=jobs-search-results,Voyager - Careers - Critical - careers-api=jobs-search-results";

    public const string JobDetailPemMetadata =
        "Voyager - Careers - Job Details=job-posting";

    public const string JobDetailQueryId =
        "voyagerJobsDashJobPostings.891aed7916d7453a37e4bbf5f1f60de4";

    public const int DefaultSearchPageSize = 25;

    private const string DefaultSearchQuery =
        "(origin:JOB_SEARCH_PAGE_JOB_FILTER,keywords:C%23%20.Net,locationUnion:(geoId:106394980),selectedFilters:(sortBy:List(R),distance:List(25.0),applyWithLinkedin:List(true),jobType:List(F,P,C,T,I,O),workplaceType:List(2,1,3)),spellCorrectionEnabled:true)";

    private const string DefaultSearchReferer =
        "https://www.linkedin.com/jobs/search/?distance=25.0&f_AL=true&f_JT=F%2CP%2CC%2CT%2CI%2CO&f_WT=1%2C3%2C2&geoId=106394980&keywords=C%23%20.Net&origin=JOB_SEARCH_PAGE_LOCATION_HISTORY&refresh=true&sortBy=R";

    private const string DefaultDetailRefererPrefix =
        "https://www.linkedin.com/jobs/search/?currentJobId=";

    private const string DefaultDetailRefererSuffix =
        "&distance=25.0&geoId=106394980&keywords=C%23%20.Net&origin=JOB_SEARCH_PAGE_JOB_FILTER";

    public static Uri BuildSearchUri(int start = 0, int count = DefaultSearchPageSize)
    {
        var rawUri =
            $"https://www.linkedin.com/voyager/api/voyagerJobsDashJobCards?decorationId={SearchDecorationId}&count={count}&q=jobSearch&query={DefaultSearchQuery}&start={start}";

        return new Uri(rawUri, UriKind.Absolute);
    }

    public static Uri BuildJobDetailUri(string linkedInJobId)
    {
        var jobPostingUrn = $"urn:li:fsd_jobPosting:{linkedInJobId}";
        var encodedJobPostingUrn = Uri.EscapeDataString(jobPostingUrn);
        var rawUri =
            $"https://www.linkedin.com/voyager/api/graphql?variables=(jobPostingUrn:{encodedJobPostingUrn})&queryId={JobDetailQueryId}";

        return new Uri(rawUri, UriKind.Absolute);
    }

    public static string BuildSearchReferer()
    {
        return DefaultSearchReferer;
    }

    public static string BuildJobDetailReferer(string linkedInJobId)
    {
        return $"{DefaultDetailRefererPrefix}{Uri.EscapeDataString(linkedInJobId)}{DefaultDetailRefererSuffix}";
    }
}
