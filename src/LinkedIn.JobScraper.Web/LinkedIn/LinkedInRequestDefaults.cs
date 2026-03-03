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

    public const string GeoTypeaheadQueryId =
        "voyagerSearchDashReusableTypeahead.4c7caa85341b17b470153ad3d1a29caf";

    public const string GeoTypeaheadPemMetadata =
        "Voyager - Search Single Typeahead=jobs-geo";

    public const int DefaultSearchPageSize = 25;
    public const int DefaultSearchPageCap = 5;
    public const int DefaultSearchJobCap = 125;
    public const int DefaultSearchPageDelayMilliseconds = 650;

    public static Uri BuildSearchUri(
        string keywords,
        string? locationGeoId,
        bool easyApply,
        IReadOnlyCollection<string> jobTypeCodes,
        IReadOnlyCollection<string> workplaceTypeCodes,
        int start = 0,
        int count = DefaultSearchPageSize)
    {
        var query = BuildSearchQuery(
            keywords,
            locationGeoId,
            easyApply,
            jobTypeCodes,
            workplaceTypeCodes);

        var rawUri =
            $"https://www.linkedin.com/voyager/api/voyagerJobsDashJobCards?decorationId={SearchDecorationId}&count={count}&q=jobSearch&query={query}&start={start}";

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

    public static string BuildSearchReferer(
        string keywords,
        string? locationGeoId,
        bool easyApply,
        IReadOnlyCollection<string> jobTypeCodes,
        IReadOnlyCollection<string> workplaceTypeCodes)
    {
        var queryParts = new List<string>
        {
            "distance=25.0",
            $"geoId={Uri.EscapeDataString(string.IsNullOrWhiteSpace(locationGeoId) ? "106394980" : locationGeoId)}",
            $"keywords={Uri.EscapeDataString(keywords)}",
            "origin=JOB_SEARCH_PAGE_LOCATION_HISTORY",
            "refresh=true",
            "sortBy=R"
        };

        if (easyApply)
        {
            queryParts.Add("f_AL=true");
        }

        var jobTypeValues = NormalizeCodes(jobTypeCodes, ["F", "P", "C", "T", "I", "O"]);

        if (jobTypeValues.Length > 0)
        {
            queryParts.Add($"f_JT={string.Join("%2C", jobTypeValues)}");
        }

        var workplaceValues = NormalizeCodes(workplaceTypeCodes, ["1", "2", "3"]);

        if (workplaceValues.Length > 0)
        {
            queryParts.Add($"f_WT={string.Join("%2C", workplaceValues)}");
        }

        return $"https://www.linkedin.com/jobs/search/?{string.Join("&", queryParts)}";
    }

    public static string BuildJobDetailReferer(
        string linkedInJobId,
        string keywords,
        string? locationGeoId)
    {
        var safeGeoId = string.IsNullOrWhiteSpace(locationGeoId) ? "106394980" : locationGeoId;

        return
            $"https://www.linkedin.com/jobs/search/?currentJobId={Uri.EscapeDataString(linkedInJobId)}&distance=25.0&geoId={Uri.EscapeDataString(safeGeoId)}&keywords={Uri.EscapeDataString(keywords)}&origin=JOB_SEARCH_PAGE_JOB_FILTER";
    }

    public static Uri BuildGeoTypeaheadUri(string query)
    {
        const string typeaheadFilter =
            "POSTCODE_1,POSTCODE_2,POPULATED_PLACE,ADMIN_DIVISION_1,ADMIN_DIVISION_2,COUNTRY_REGION,MARKET_AREA,COUNTRY_CLUSTER";

        var safeQuery = Uri.EscapeDataString(query);
        var rawUri =
            $"https://www.linkedin.com/voyager/api/graphql?variables=(keywords:{safeQuery},query:(typeaheadFilterQuery:(geoSearchTypes:List({typeaheadFilter})),typeaheadUseCase:JOBS),type:GEO)&queryId={GeoTypeaheadQueryId}";

        return new Uri(rawUri, UriKind.Absolute);
    }

    private static string BuildSearchQuery(
        string keywords,
        string? locationGeoId,
        bool easyApply,
        IReadOnlyCollection<string> jobTypeCodes,
        IReadOnlyCollection<string> workplaceTypeCodes)
    {
        var safeKeywords = Uri.EscapeDataString(keywords);
        var safeLocationGeoId = string.IsNullOrWhiteSpace(locationGeoId) ? "106394980" : locationGeoId;
        var selectedFilters = new List<string>
        {
            "sortBy:List(R)",
            "distance:List(25.0)"
        };

        if (easyApply)
        {
            selectedFilters.Add("applyWithLinkedin:List(true)");
        }

        var jobTypeValues = NormalizeCodes(jobTypeCodes, ["F", "P", "C", "T", "I", "O"]);

        if (jobTypeValues.Length > 0)
        {
            selectedFilters.Add($"jobType:List({string.Join(',', jobTypeValues)})");
        }

        var workplaceValues = NormalizeCodes(workplaceTypeCodes, ["1", "2", "3"]);

        if (workplaceValues.Length > 0)
        {
            selectedFilters.Add($"workplaceType:List({string.Join(',', workplaceValues)})");
        }

        return
            $"(origin:JOB_SEARCH_PAGE_JOB_FILTER,keywords:{safeKeywords},locationUnion:(geoId:{safeLocationGeoId}),selectedFilters:({string.Join(',', selectedFilters)}),spellCorrectionEnabled:true)";
    }

    private static string[] NormalizeCodes(IReadOnlyCollection<string> values, string[] fallback)
    {
        var normalized = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? fallback : normalized;
    }
}
