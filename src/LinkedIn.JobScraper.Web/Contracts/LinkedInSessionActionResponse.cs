namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record LinkedInSessionActionResponse(
    bool Success,
    string? Message,
    LinkedInSessionStateResponse State);

public sealed record LinkedInSessionStateResponse(
    bool StoredSessionAvailable,
    DateTimeOffset? StoredSessionCapturedAtUtc,
    string? StoredSessionSource,
    DateTimeOffset? StoredSessionEstimatedExpiresAtUtc,
    string? StoredSessionExpirySource,
    string SessionIndicatorLabel,
    string SessionIndicatorClass,
    LinkedInSessionResetRequirementResponse ResetRequirement);

public sealed record LinkedInSessionResetRequirementResponse(
    bool Required,
    string? ReasonCode,
    string? Message,
    int? StatusCode,
    DateTimeOffset? RequiredAtUtc);
