using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence.Entities;

namespace LinkedIn.JobScraper.Web.Jobs;

public interface IJobsDashboardService
{
    Task<JobsDashboardSnapshot> GetSnapshotAsync(
        JobsDashboardQuery query,
        CancellationToken cancellationToken);

    Task<JobDetailsSnapshot?> GetJobDetailsAsync(
        Guid jobId,
        CancellationToken cancellationToken);

    Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(CancellationToken cancellationToken);

    Task<JobStatusChangeResult> UpdateStatusAsync(
        Guid jobId,
        JobWorkflowStatus status,
        CancellationToken cancellationToken);
}

public sealed record JobsDashboardSnapshot(
    int TotalJobs,
    int FilteredJobs,
    int ScoredJobs,
    int StrongMatches,
    int UnscoredJobs,
    JobsDashboardQuery Query,
    IReadOnlyList<JobDashboardRow> Jobs);

public sealed record JobDetailsSnapshot(
    Guid Id,
    string Title,
    string? CompanyName,
    string? LocationName,
    string? EmploymentStatus,
    DateTimeOffset? ListedAtUtc,
    DateTimeOffset FirstDiscoveredAtUtc,
    DateTimeOffset LastSeenAtUtc,
    JobWorkflowStatus CurrentStatus,
    string? Description,
    string? CompanyApplyUrl,
    int? AiScore,
    string? AiLabel,
    string? AiSummary,
    string? AiWhyMatched,
    string? AiConcerns);

public sealed class JobsDashboardQuery
{
    public string? Search { get; set; }

    public JobWorkflowStatus? FilterStatus { get; set; }

    public string? AiLabel { get; set; }

    public bool OnlyUnscored { get; set; }

    public int? MinScore { get; set; }

    public string SortBy { get; set; } = "last-seen";

    public string GetNormalizedSortBy()
    {
        return SortBy switch
        {
            "listed" => "listed",
            "score" => "score",
            "title" => "title",
            "company" => "company",
            _ => "last-seen"
        };
    }
}

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
    string? AiWhyMatched,
    string? AiConcerns,
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
