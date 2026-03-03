using LinkedIn.JobScraper.Web.LinkedIn.Session;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSessionHeaderSanitizerTests
{
    [Fact]
    public async Task InMemorySessionStoreKeepsOnlyRequiredHeaders()
    {
        var store = new InMemoryLinkedInSessionStore();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cookie"] = "li_at=secret; JSESSIONID=\"ajax:123\"",
            ["csrf-token"] = "ajax:123",
            ["User-Agent"] = "Mozilla/5.0",
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["x-li-lang"] = "en_US",
            ["x-restli-protocol-version"] = "2.0.0",
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["Referer"] = "https://www.linkedin.com/jobs/",
            ["x-li-pem-metadata"] = "Voyager - Something"
        };

        await store.SaveAsync(
            new LinkedInSessionSnapshot(headers, DateTimeOffset.UtcNow, "Test"),
            CancellationToken.None);
        var snapshot = await store.GetCurrentAsync(CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(6, snapshot.Headers.Count);
        Assert.Equal("li_at=secret; JSESSIONID=\"ajax:123\"", snapshot.Headers["Cookie"]);
        Assert.Equal("ajax:123", snapshot.Headers["csrf-token"]);
        Assert.DoesNotContain("Accept", snapshot.Headers.Keys);
        Assert.DoesNotContain("Referer", snapshot.Headers.Keys);
        Assert.DoesNotContain("x-li-pem-metadata", snapshot.Headers.Keys);
    }

    [Fact]
    public async Task InMemorySessionStoreRemovesBlankAllowedValues()
    {
        var store = new InMemoryLinkedInSessionStore();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cookie"] = " ",
            ["csrf-token"] = "ajax:123"
        };

        await store.SaveAsync(
            new LinkedInSessionSnapshot(headers, DateTimeOffset.UtcNow, "Test"),
            CancellationToken.None);
        var snapshot = await store.GetCurrentAsync(CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Headers);
        Assert.Equal("ajax:123", snapshot.Headers["csrf-token"]);
    }
}
