using LinkedIn.JobScraper.Web.LinkedIn;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInRequestDefaultsTests
{
    [Fact]
    public void BuildSearchUriUsesConfiguredPagingAndFilters()
    {
        var uri = LinkedInRequestDefaults.BuildSearchUri(
            "C# .NET",
            "106394980",
            easyApply: true,
            jobTypeCodes: ["F", "C"],
            workplaceTypeCodes: ["1", "3"],
            start: 50,
            count: 25);

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("www.linkedin.com", uri.Host);
        Assert.Equal("/voyager/api/voyagerJobsDashJobCards", uri.AbsolutePath);
        Assert.Contains("count=25", uri.Query, StringComparison.Ordinal);
        Assert.Contains("start=50", uri.Query, StringComparison.Ordinal);
        Assert.Contains("sortBy:List(DD)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.DoesNotContain("distance:List(25.0)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("applyWithLinkedin:List(true)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("jobType:List(F,C)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("workplaceType:List(1,3)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSearchUriOmitsOptionalFiltersWhenNoOverridesAreSaved()
    {
        var uri = LinkedInRequestDefaults.BuildSearchUri(
            "backend engineer",
            null,
            easyApply: false,
            jobTypeCodes: [],
            workplaceTypeCodes: []);

        Assert.Contains("sortBy:List(DD)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.DoesNotContain("distance:List(25.0)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.DoesNotContain("locationUnion:", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.DoesNotContain("jobType:List(", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.DoesNotContain("workplaceType:List(", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSearchRefererOmitsFallbackGeoAndStaticParameters()
    {
        var referer = LinkedInRequestDefaults.BuildSearchReferer(
            "backend engineer",
            null,
            easyApply: true,
            jobTypeCodes: [],
            workplaceTypeCodes: []);

        Assert.Contains("f_AL=true", referer, StringComparison.Ordinal);
        Assert.Contains("keywords=backend%20engineer", referer, StringComparison.Ordinal);
        Assert.Contains("origin=JOB_SEARCH_PAGE_JOB_FILTER", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("geoId=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("distance=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("sortBy=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("f_JT=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("f_WT=", referer, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildJobDetailUriOmitsQueryIdAndBuildJobDetailRefererOmitsCurrentJobId()
    {
        var uri = LinkedInRequestDefaults.BuildJobDetailUri(
            "4379963196",
            "voyagerJobsDashJobPostings.891aed7916d7453a37e4bbf5f1f60de4");
        var referer = LinkedInRequestDefaults.BuildJobDetailReferer("C# .NET", null);

        Assert.Contains("variables=", uri.Query, StringComparison.Ordinal);
        Assert.Contains("queryId=voyagerJobsDashJobPostings.891aed7916d7453a37e4bbf5f1f60de4", uri.Query, StringComparison.Ordinal);
        Assert.Contains("keywords=C%23%20.NET", referer, StringComparison.Ordinal);
        Assert.Contains("origin=JOB_SEARCH_PAGE_JOB_FILTER", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("currentJobId=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("geoId=", referer, StringComparison.Ordinal);
        Assert.DoesNotContain("distance=", referer, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildGeoTypeaheadUriUsesGeoTypeaheadQueryShape()
    {
        var uri = LinkedInRequestDefaults.BuildGeoTypeaheadUri(
            "Cyprus",
            "voyagerSearchDashReusableTypeahead.4c7caa85341b17b470153ad3d1a29caf");

        Assert.Contains("includeWebMetadata=true", uri.Query, StringComparison.Ordinal);
        Assert.Contains("variables=", uri.Query, StringComparison.Ordinal);
        Assert.Contains("queryId=voyagerSearchDashReusableTypeahead.4c7caa85341b17b470153ad3d1a29caf", uri.Query, StringComparison.Ordinal);
        Assert.Contains("typeaheadFilterQuery", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("typeaheadUseCase:JOBS", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("geoSearchTypes:List(POSTCODE_1,POSTCODE_2,POPULATED_PLACE,ADMIN_DIVISION_1,ADMIN_DIVISION_2,COUNTRY_REGION,MARKET_AREA,COUNTRY_CLUSTER)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
    }
}
