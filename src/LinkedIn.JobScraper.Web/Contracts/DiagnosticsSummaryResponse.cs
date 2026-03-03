namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record DiagnosticsSummaryResponse(
    DiagnosticsConfigSummaryResponse Config,
    DiagnosticsSessionSummaryResponse Session);

public sealed record DiagnosticsConfigSummaryResponse(
    bool SqlServerConfigured,
    bool OpenAiApiKeyConfigured,
    bool OpenAiModelConfigured);

public sealed record DiagnosticsSessionSummaryResponse(
    bool StoredSessionAvailable,
    DateTimeOffset? CapturedAtUtc,
    string? Source,
    string? ReadError);
