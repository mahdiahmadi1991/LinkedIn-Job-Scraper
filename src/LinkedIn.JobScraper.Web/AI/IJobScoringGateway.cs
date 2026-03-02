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
    string BehavioralInstructions);

public sealed record JobScoringGatewayResult(
    bool CanScore,
    string Message);
