namespace LinkedIn.JobScraper.Web.Logging;

internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Information,
        Message = "Per-run application log file: {PerRunLogFilePath}")]
    public static partial void PerRunLogFileInitialized(ILogger logger, string perRunLogFilePath);
}
