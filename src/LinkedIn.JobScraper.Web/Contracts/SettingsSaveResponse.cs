namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record SettingsSaveResponse(
    bool Success,
    string Message,
    string RedirectUrl);
