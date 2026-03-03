namespace LinkedIn.JobScraper.Web.AI;

public interface IJobScoringGateway
{
    Task<JobScoringGatewayResult> ScoreAsync(
        JobScoringGatewayRequest request,
        CancellationToken cancellationToken);
}

public sealed record JobScoringGatewayRequest(
    string JobTitle,
    string JobDescription,
    string BehavioralInstructions,
    string PrioritySignals,
    string ExclusionSignals,
    string OutputLanguageCode,
    string? CompanyName,
    string? LocationName,
    string? EmploymentStatus);

public sealed record JobScoringGatewayResult(
    bool CanScore,
    string Message,
    int? StatusCode = null,
    int? Score = null,
    string? Label = null,
    string? Summary = null,
    string? WhyMatched = null,
    string? Concerns = null);
