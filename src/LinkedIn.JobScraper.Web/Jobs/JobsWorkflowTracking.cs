using System.Collections.Concurrent;
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

public interface IJobsWorkflowStateStore
{
    CancellationToken RegisterWorkflow(string workflowId, CancellationToken outerCancellationToken);

    bool RequestCancellation(string workflowId);

    void Append(JobsWorkflowProgressUpdate update);

    JobsWorkflowProgressBatch GetBatch(string workflowId, long afterSequence);
}

public sealed class SignalRJobsWorkflowProgressNotifier : IJobsWorkflowProgressNotifier
{
    private readonly IHubContext<JobsWorkflowProgressHub> _hubContext;
    private readonly IJobsWorkflowStateStore _workflowStateStore;

    public SignalRJobsWorkflowProgressNotifier(
        IHubContext<JobsWorkflowProgressHub> hubContext,
        IJobsWorkflowStateStore workflowStateStore)
    {
        _hubContext = hubContext;
        _workflowStateStore = workflowStateStore;
    }

    public Task PublishAsync(
        string? connectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken)
    {
        _workflowStateStore.Append(update);

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Client(connectionId)
            .SendAsync("WorkflowProgress", update, cancellationToken);
    }
}

public sealed record JobsWorkflowProgressUpdate(
    string WorkflowId,
    string CorrelationId,
    string State,
    string Stage,
    int Percent,
    string Message,
    int? RequestedCount = null,
    int? ProcessedCount = null,
    int? SucceededCount = null,
    int? FailedCount = null);

public sealed record JobsWorkflowProgressEvent(
    long Sequence,
    DateTimeOffset OccurredAtUtc,
    JobsWorkflowProgressUpdate Update);

public sealed record JobsWorkflowProgressBatch(
    IReadOnlyList<JobsWorkflowProgressEvent> Events,
    long NextSequence,
    bool CancellationRequested);

public sealed class InMemoryJobsWorkflowStateStore : IJobsWorkflowStateStore
{
    private readonly ConcurrentDictionary<string, WorkflowState> _workflows = new(StringComparer.OrdinalIgnoreCase);

    public CancellationToken RegisterWorkflow(string workflowId, CancellationToken outerCancellationToken)
    {
        var state = new WorkflowState();
        state.ReplaceCancellationSource(outerCancellationToken);
        _workflows.AddOrUpdate(workflowId, state, (_, _) => state);
        return state.Token;
    }

    public bool RequestCancellation(string workflowId)
    {
        if (!_workflows.TryGetValue(workflowId, out var state))
        {
            return false;
        }

        state.RequestCancellation();
        return true;
    }

    public JobsWorkflowProgressBatch GetBatch(string workflowId, long afterSequence)
    {
        if (!_workflows.TryGetValue(workflowId, out var state))
        {
            return new JobsWorkflowProgressBatch([], 1, false);
        }

        return state.GetBatch(afterSequence);
    }

    public void Append(JobsWorkflowProgressUpdate update)
    {
        var state = _workflows.GetOrAdd(update.WorkflowId, static _ => new WorkflowState());
        state.Append(update);
    }

    private sealed class WorkflowState
    {
        private readonly object _sync = new();
        private readonly List<JobsWorkflowProgressEvent> _events = [];
        private CancellationTokenSource? _cts;
        private long _nextSequence = 1;

        public CancellationToken Token
        {
            get
            {
                lock (_sync)
                {
                    return _cts?.Token ?? CancellationToken.None;
                }
            }
        }

        public bool CancellationRequested { get; private set; }

        public void ReplaceCancellationSource(CancellationToken outerCancellationToken)
        {
            lock (_sync)
            {
                _cts?.Dispose();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken);
                CancellationRequested = false;
            }
        }

        public void RequestCancellation()
        {
            lock (_sync)
            {
                if (CancellationRequested)
                {
                    return;
                }

                CancellationRequested = true;
                _cts?.Cancel();
            }
        }

        public void Append(JobsWorkflowProgressUpdate update)
        {
            lock (_sync)
            {
                _events.Add(
                    new JobsWorkflowProgressEvent(
                        _nextSequence++,
                        DateTimeOffset.UtcNow,
                        update));
            }
        }

        public JobsWorkflowProgressBatch GetBatch(long afterSequence)
        {
            lock (_sync)
            {
                var events = _events
                    .Where(item => item.Sequence > afterSequence)
                    .ToArray();

                return new JobsWorkflowProgressBatch(events, _nextSequence, CancellationRequested);
            }
        }
    }
}
