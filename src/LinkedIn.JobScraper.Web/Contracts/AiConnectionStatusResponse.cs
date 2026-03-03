namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record AiConnectionStatusResponse(
    bool Success,
    string Message,
    AiConnectionStateResponse State);

public sealed record AiConnectionStateResponse(
    bool ApiKeyConfigured,
    string? Model,
    string BaseUrl,
    bool Ready);
