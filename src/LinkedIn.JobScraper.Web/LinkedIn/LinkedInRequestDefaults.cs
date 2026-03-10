namespace LinkedIn.JobScraper.Web.LinkedIn;

public static class LinkedInRequestDefaults
{
    public const string SearchDecorationId =
        "com.linkedin.voyager.dash.deco.jobs.search.JobSearchCardsCollection-220";

    public const int DefaultSearchPageSize = 100;
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

    public static Uri BuildJobDetailUri(string linkedInJobId, string? jobDetailQueryId = null)
    {
        var jobPostingUrn = $"urn:li:fsd_jobPosting:{linkedInJobId}";
        var encodedJobPostingUrn = Uri.EscapeDataString(jobPostingUrn);
        var rawUri = string.IsNullOrWhiteSpace(jobDetailQueryId)
            ? $"https://www.linkedin.com/voyager/api/graphql?variables=(jobPostingUrn:{encodedJobPostingUrn})"
            : $"https://www.linkedin.com/voyager/api/graphql?variables=(jobPostingUrn:{encodedJobPostingUrn})&queryId={Uri.EscapeDataString(jobDetailQueryId)}";

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
            $"keywords={Uri.EscapeDataString(keywords)}",
            "origin=JOB_SEARCH_PAGE_JOB_FILTER"
        };

        if (!string.IsNullOrWhiteSpace(locationGeoId))
        {
            queryParts.Add($"geoId={Uri.EscapeDataString(locationGeoId)}");
        }

        if (easyApply)
        {
            queryParts.Add("f_AL=true");
        }

        var jobTypeValues = NormalizeCodes(jobTypeCodes);

        if (jobTypeValues.Length > 0)
        {
            queryParts.Add($"f_JT={string.Join("%2C", jobTypeValues)}");
        }

        var workplaceValues = NormalizeCodes(workplaceTypeCodes);

        if (workplaceValues.Length > 0)
        {
            queryParts.Add($"f_WT={string.Join("%2C", workplaceValues)}");
        }

        return $"https://www.linkedin.com/jobs/search/?{string.Join("&", queryParts)}";
    }

    public static string BuildJobDetailReferer(
        string keywords,
        string? locationGeoId)
    {
        var queryParts = new List<string>
        {
            $"keywords={Uri.EscapeDataString(keywords)}",
            "origin=JOB_SEARCH_PAGE_JOB_FILTER"
        };

        if (!string.IsNullOrWhiteSpace(locationGeoId))
        {
            queryParts.Add($"geoId={Uri.EscapeDataString(locationGeoId)}");
        }

        return $"https://www.linkedin.com/jobs/search/?{string.Join("&", queryParts)}";
    }

    public static Uri BuildGeoTypeaheadUri(string query, string? graphQlQueryId = null)
    {
        var safeQuery = Uri.EscapeDataString(query);
        var rawUri = string.IsNullOrWhiteSpace(graphQlQueryId)
            ? $"https://www.linkedin.com/voyager/api/graphql?includeWebMetadata=true&variables=(keywords:{safeQuery},query:(typeaheadFilterQuery:(geoSearchTypes:List(POSTCODE_1,POSTCODE_2,POPULATED_PLACE,ADMIN_DIVISION_1,ADMIN_DIVISION_2,COUNTRY_REGION,MARKET_AREA,COUNTRY_CLUSTER)),typeaheadUseCase:JOBS),type:GEO)"
            : $"https://www.linkedin.com/voyager/api/graphql?includeWebMetadata=true&variables=(keywords:{safeQuery},query:(typeaheadFilterQuery:(geoSearchTypes:List(POSTCODE_1,POSTCODE_2,POPULATED_PLACE,ADMIN_DIVISION_1,ADMIN_DIVISION_2,COUNTRY_REGION,MARKET_AREA,COUNTRY_CLUSTER)),typeaheadUseCase:JOBS),type:GEO)&queryId={Uri.EscapeDataString(graphQlQueryId)}";

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
        var selectedFilters = new List<string>();

        if (easyApply)
        {
            selectedFilters.Add("applyWithLinkedin:List(true)");
        }

        var jobTypeValues = NormalizeCodes(jobTypeCodes);

        if (jobTypeValues.Length > 0)
        {
            selectedFilters.Add($"jobType:List({string.Join(',', jobTypeValues)})");
        }

        var workplaceValues = NormalizeCodes(workplaceTypeCodes);

        if (workplaceValues.Length > 0)
        {
            selectedFilters.Add($"workplaceType:List({string.Join(',', workplaceValues)})");
        }

        var queryParts = new List<string>
        {
            "origin:JOB_SEARCH_PAGE_JOB_FILTER",
            $"keywords:{safeKeywords}"
        };

        if (!string.IsNullOrWhiteSpace(locationGeoId))
        {
            queryParts.Add($"locationUnion:(geoId:{locationGeoId})");
        }

        if (selectedFilters.Count > 0)
        {
            queryParts.Add($"selectedFilters:({string.Join(',', selectedFilters)})");
        }

        queryParts.Add("spellCorrectionEnabled:true");

        return $"({string.Join(',', queryParts)})";
    }

    private static string[] NormalizeCodes(IReadOnlyCollection<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
