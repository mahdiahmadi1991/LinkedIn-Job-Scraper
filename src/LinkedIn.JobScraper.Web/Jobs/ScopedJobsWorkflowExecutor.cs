using Microsoft.Extensions.DependencyInjection;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class ScopedJobsWorkflowExecutor : IJobsWorkflowExecutor
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ScopedJobsWorkflowExecutor(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
        string? progressConnectionId,
        string workflowId,
        string? correlationId)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var jobsDashboardService = scope.ServiceProvider.GetRequiredService<IJobsDashboardService>();

        return await jobsDashboardService.RunFetchAndScoreAsync(
            progressConnectionId,
            workflowId,
            correlationId,
            CancellationToken.None);
    }
}
