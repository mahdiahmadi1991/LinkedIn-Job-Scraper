namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record FetchAndScoreAjaxResponse(
    bool Success,
    string Severity,
    string Message,
    string RedirectUrl);
