using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiJobScoringGateway : IJobScoringGateway
{
    private readonly IOptions<OpenAiSecurityOptions> _securityOptions;

    public OpenAiJobScoringGateway(IOptions<OpenAiSecurityOptions> securityOptions)
    {
        _securityOptions = securityOptions;
    }

    public Task<JobScoringGatewayResult> ScoreAsync(
        JobScoringGatewayRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_securityOptions.Value.ApiKey))
        {
            return Task.FromResult(
                new JobScoringGatewayResult(
                    false,
                    "OpenAI API key is not configured yet."));
        }

        return Task.FromResult(
            new JobScoringGatewayResult(
                false,
                "OpenAI scoring is not implemented yet."));
    }
}
