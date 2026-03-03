namespace LinkedIn.JobScraper.Web.Logging;

public static class PerRunLogFilePath
{
    public static string Create(string contentRootPath, DateTimeOffset utcNow, int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var logsDirectory = Path.Combine(contentRootPath, "logs");
        var fileName = $"run-{utcNow:yyyyMMdd-HHmmss}-pid{processId}.log";

        return Path.Combine(logsDirectory, fileName);
    }
}
