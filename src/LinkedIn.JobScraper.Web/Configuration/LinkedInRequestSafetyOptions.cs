namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class LinkedInRequestSafetyOptions
{
    public const string SectionName = "LinkedIn:RequestSafety";

    public bool Enabled { get; set; } = true;

    public int MinimumDelayMilliseconds { get; set; } = 2500;

    public int MaxJitterMilliseconds { get; set; } = 1200;

    public int MaxRetryAttempts { get; set; }

    public int GetMinimumDelayMilliseconds() => MinimumDelayMilliseconds > 0
        ? MinimumDelayMilliseconds
        : 2500;

    public int GetMaxJitterMilliseconds() => MaxJitterMilliseconds >= 0
        ? MaxJitterMilliseconds
        : 0;

    public int GetMaxRetryAttempts() => Math.Clamp(MaxRetryAttempts, 0, 5);
}
