using Microsoft.AspNetCore.SignalR;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobsWorkflowProgressHub : Hub
{
}

public interface IJobsWorkflowProgressNotifier
{
    Task PublishAsync(
        string? connectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken);
}

public sealed class SignalRJobsWorkflowProgressNotifier : IJobsWorkflowProgressNotifier
{
    private readonly IHubContext<JobsWorkflowProgressHub> _hubContext;

    public SignalRJobsWorkflowProgressNotifier(IHubContext<JobsWorkflowProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(
        string? connectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Client(connectionId)
            .SendAsync("WorkflowProgress", update, cancellationToken);
    }
}

public sealed record JobsWorkflowProgressUpdate(
    string State,
    string Stage,
    int Percent,
    string Message,
    int? RequestedCount = null,
    int? ProcessedCount = null,
    int? SucceededCount = null,
    int? FailedCount = null);
