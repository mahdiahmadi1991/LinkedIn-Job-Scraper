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
        Assert.Contains("applyWithLinkedin:List(true)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("jobType:List(F,C)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
        Assert.Contains("workplaceType:List(1,3)", Uri.UnescapeDataString(uri.Query), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSearchRefererFallsBackToDefaultGeoAndIncludesEasyApply()
    {
        var referer = LinkedInRequestDefaults.BuildSearchReferer(
            "backend engineer",
            null,
            easyApply: true,
            jobTypeCodes: [],
            workplaceTypeCodes: []);

        Assert.Contains("geoId=106394980", referer, StringComparison.Ordinal);
        Assert.Contains("f_AL=true", referer, StringComparison.Ordinal);
        Assert.Contains("keywords=backend%20engineer", referer, StringComparison.Ordinal);
    }
}
