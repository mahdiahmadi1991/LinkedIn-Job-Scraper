using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace LinkedIn.JobScraper.Web.Jobs;

public sealed class JobsWorkflowProgressHub : Hub
{
}

public interface IJobsWorkflowProgressNotifier
{
    Task PublishAsync(
        int userId,
        string? connectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken);
}

public interface IJobsWorkflowStateStore
{
    JobsWorkflowRegistrationResult RegisterWorkflow(int userId, string workflowId, CancellationToken outerCancellationToken);

    bool RequestCancellation(int userId, string workflowId);

    void Append(int userId, JobsWorkflowProgressUpdate update);

    void ReleaseWorkflow(int userId, string workflowId);

    JobsWorkflowProgressBatch GetBatch(int userId, string workflowId, long afterSequence);
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
        int userId,
        string? connectionId,
        JobsWorkflowProgressUpdate update,
        CancellationToken cancellationToken)
    {
        _workflowStateStore.Append(userId, update);

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
    double Percent,
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
    bool CancellationRequested,
    bool WorkflowFound);

public sealed record JobsWorkflowRegistrationResult(
    bool Accepted,
    string? ActiveWorkflowId,
    CancellationToken CancellationToken);

public sealed class InMemoryJobsWorkflowStateStore : IJobsWorkflowStateStore
{
    private readonly ConcurrentDictionary<string, WorkflowState> _workflows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, string> _activeWorkflowIds = new();
    private readonly object _registrationSync = new();

    public JobsWorkflowRegistrationResult RegisterWorkflow(
        int userId,
        string workflowId,
        CancellationToken outerCancellationToken)
    {
        lock (_registrationSync)
        {
            if (_activeWorkflowIds.TryGetValue(userId, out var activeWorkflowId) &&
                !string.IsNullOrWhiteSpace(activeWorkflowId))
            {
                return new JobsWorkflowRegistrationResult(false, activeWorkflowId, CancellationToken.None);
            }

            var key = ToWorkflowKey(userId, workflowId);
            var state = new WorkflowState();
            state.ReplaceCancellationSource(outerCancellationToken);
            _workflows.AddOrUpdate(key, state, (_, _) => state);
            _activeWorkflowIds[userId] = workflowId;
            return new JobsWorkflowRegistrationResult(true, null, state.Token);
        }
    }

    public bool RequestCancellation(int userId, string workflowId)
    {
        if (!_workflows.TryGetValue(ToWorkflowKey(userId, workflowId), out var state))
        {
            return false;
        }

        state.RequestCancellation();
        return true;
    }

    public JobsWorkflowProgressBatch GetBatch(int userId, string workflowId, long afterSequence)
    {
        if (!_workflows.TryGetValue(ToWorkflowKey(userId, workflowId), out var state))
        {
            return new JobsWorkflowProgressBatch([], 1, false, false);
        }

        return state.GetBatch(afterSequence);
    }

    public void Append(int userId, JobsWorkflowProgressUpdate update)
    {
        var workflowKey = ToWorkflowKey(userId, update.WorkflowId);
        var state = _workflows.GetOrAdd(workflowKey, static _ => new WorkflowState());
        state.Append(update);

        if (IsTerminalState(update.State))
        {
            ReleaseWorkflow(userId, update.WorkflowId);
        }
    }

    public void ReleaseWorkflow(int userId, string workflowId)
    {
        lock (_registrationSync)
        {
            if (!_activeWorkflowIds.TryGetValue(userId, out var activeWorkflowId))
            {
                return;
            }

            if (!string.Equals(activeWorkflowId, workflowId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeWorkflowIds.TryRemove(userId, out _);
        }
    }

    private static string ToWorkflowKey(int userId, string workflowId)
    {
        return $"{userId}:{workflowId}";
    }

    private static bool IsTerminalState(string? state)
    {
        return string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, "cancelled", StringComparison.OrdinalIgnoreCase);
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

                return new JobsWorkflowProgressBatch(events, _nextSequence, CancellationRequested, true);
            }
        }
    }
}
