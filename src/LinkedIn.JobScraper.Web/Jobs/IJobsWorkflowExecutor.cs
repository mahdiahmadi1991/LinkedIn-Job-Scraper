namespace LinkedIn.JobScraper.Web.Jobs;

public interface IJobsWorkflowExecutor
{
    Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(
        string? progressConnectionId,
        string workflowId,
        string? correlationId);
}
