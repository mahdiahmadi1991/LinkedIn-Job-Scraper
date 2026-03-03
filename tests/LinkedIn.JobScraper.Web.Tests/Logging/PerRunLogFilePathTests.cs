using LinkedIn.JobScraper.Web.Logging;

namespace LinkedIn.JobScraper.Web.Tests.Logging;

public sealed class PerRunLogFilePathTests
{
    [Fact]
    public void CreateBuildsPredictableLogFilePath()
    {
        var path = PerRunLogFilePath.Create(
            "/tmp/ljs",
            new DateTimeOffset(2026, 3, 3, 12, 45, 9, TimeSpan.Zero),
            4321);

        Assert.Equal("/tmp/ljs/logs/run-20260303-124509-pid4321.log", path);
    }
}
