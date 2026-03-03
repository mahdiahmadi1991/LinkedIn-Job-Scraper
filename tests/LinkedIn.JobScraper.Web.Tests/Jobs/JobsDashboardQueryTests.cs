using LinkedIn.JobScraper.Web.Jobs;

namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobsDashboardQueryTests
{
    [Theory]
    [InlineData("listed", "listed")]
    [InlineData("score", "score")]
    [InlineData("title", "title")]
    [InlineData("company", "company")]
    [InlineData("last-seen", "last-seen")]
    [InlineData("unexpected", "last-seen")]
    [InlineData("", "last-seen")]
    public void GetNormalizedSortByReturnsExpectedValue(string input, string expected)
    {
        var query = new JobsDashboardQuery
        {
            SortBy = input
        };

        var result = query.GetNormalizedSortBy();

        Assert.Equal(expected, result);
    }
}
