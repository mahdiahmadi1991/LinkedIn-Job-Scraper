namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record WorkflowCancellationResponse(
    bool Success,
    string Message);
