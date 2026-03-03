namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class LinkedInFetchDiagnosticsOptions
{
    public const string SectionName = "LinkedIn:FetchDiagnostics";

    public bool Enabled { get; set; }

    public bool LogResponseBodies { get; set; }

    public int ResponseBodyMaxLength { get; set; } = 2000;

    public int GetResponseBodyMaxLength() => ResponseBodyMaxLength > 0 ? ResponseBodyMaxLength : 2000;
}
