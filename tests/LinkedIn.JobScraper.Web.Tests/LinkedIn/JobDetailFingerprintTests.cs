using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Details;
using System.Globalization;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class JobDetailFingerprintTests
{
    [Fact]
    public void ComputeReturnsSameFingerprintForEquivalentWhitespace()
    {
        var detailA = new LinkedInJobDetailData(
            "1",
            "urn:li:fsd_jobPosting:1",
            "Senior Engineer",
            "Acme",
            "Limassol",
            "Full-time",
            "Build APIs",
            "https://example.com/apply",
            DateTimeOffset.Parse("2026-03-04T10:00:00+00:00", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-03-04T12:00:00+00:00", CultureInfo.InvariantCulture));
        var detailB = detailA with
        {
            Title = "  Senior Engineer  ",
            Description = "Build APIs  "
        };

        var first = JobDetailFingerprint.Compute(detailA);
        var second = JobDetailFingerprint.Compute(detailB);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeChangesWhenUpdatedTimestampChanges()
    {
        var detailA = new LinkedInJobDetailData(
            "1",
            "urn:li:fsd_jobPosting:1",
            "Senior Engineer",
            "Acme",
            "Limassol",
            "Full-time",
            "Build APIs",
            "https://example.com/apply",
            DateTimeOffset.Parse("2026-03-04T10:00:00+00:00", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-03-04T12:00:00+00:00", CultureInfo.InvariantCulture));
        var detailB = detailA with
        {
            LinkedInUpdatedAtUtc = DateTimeOffset.Parse("2026-03-05T12:00:00+00:00", CultureInfo.InvariantCulture)
        };

        var first = JobDetailFingerprint.Compute(detailA);
        var second = JobDetailFingerprint.Compute(detailB);

        Assert.NotEqual(first, second);
    }
}
