using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence.Entities;

namespace LinkedIn.JobScraper.Web.Jobs;

public interface IJobsDashboardService
{
    Task<JobsDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(CancellationToken cancellationToken);

    Task<JobStatusChangeResult> UpdateStatusAsync(
        Guid jobId,
        JobWorkflowStatus status,
        CancellationToken cancellationToken);
}

public sealed record JobsDashboardSnapshot(
    int TotalJobs,
    int ScoredJobs,
    int StrongMatches,
    int UnscoredJobs,
    IReadOnlyList<JobDashboardRow> Jobs);

public sealed record JobDashboardRow(
    Guid Id,
    string Title,
    string? CompanyName,
    string? LocationName,
    string? EmploymentStatus,
    DateTimeOffset? ListedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    JobWorkflowStatus CurrentStatus,
    int? AiScore,
    string? AiLabel,
    string? AiSummary,
    string? CompanyApplyUrl);

public sealed record FetchAndScoreWorkflowResult(
    bool Success,
    string Message,
    string Severity,
    JobImportResult ImportResult,
    JobEnrichmentResult? EnrichmentResult,
    JobBatchScoringResult? ScoringResult);

public sealed record JobStatusChangeResult(
    bool Success,
    string Message,
    string Severity);
