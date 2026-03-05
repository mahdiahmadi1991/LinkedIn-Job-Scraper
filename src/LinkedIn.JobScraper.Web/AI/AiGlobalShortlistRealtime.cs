using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class AiGlobalShortlistProgressHub : Hub
{
}

public interface IAiGlobalShortlistProgressNotifier
{
    Task PublishAsync(
        string? connectionId,
        AiGlobalShortlistProgressUpdate update,
        CancellationToken cancellationToken);
}

public interface IAiGlobalShortlistProgressStateStore
{
    AiGlobalShortlistProgressEvent Append(AiGlobalShortlistProgressUpdate update);

    AiGlobalShortlistProgressBatch GetBatch(Guid runId, long afterSequence);
}

public sealed class SignalRAiGlobalShortlistProgressNotifier : IAiGlobalShortlistProgressNotifier
{
    private readonly IHubContext<AiGlobalShortlistProgressHub> _hubContext;
    private readonly IAiGlobalShortlistProgressStateStore _progressStateStore;

    public SignalRAiGlobalShortlistProgressNotifier(
        IHubContext<AiGlobalShortlistProgressHub> hubContext,
        IAiGlobalShortlistProgressStateStore progressStateStore)
    {
        _hubContext = hubContext;
        _progressStateStore = progressStateStore;
    }

    public Task PublishAsync(
        string? connectionId,
        AiGlobalShortlistProgressUpdate update,
        CancellationToken cancellationToken)
    {
        var progressEvent = _progressStateStore.Append(update);

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Client(connectionId)
            .SendAsync("GlobalShortlistProgress", progressEvent, cancellationToken);
    }
}

public sealed record AiGlobalShortlistProgressUpdate(
    Guid RunId,
    string State,
    string Stage,
    string Message,
    int? CandidateCount = null,
    int? ProcessedCount = null,
    int? AcceptedCount = null,
    int? NeedsReviewCount = null,
    int? FailedCount = null,
    int? SequenceNumber = null,
    string? Decision = null,
    Guid? JobId = null,
    string? LinkedInJobId = null,
    string? JobTitle = null,
    string? CompanyName = null,
    string? LocationName = null,
    int? Score = null,
    int? Confidence = null,
    string? RecommendationReason = null,
    string? Concerns = null,
    string? ErrorCode = null);

public sealed record AiGlobalShortlistProgressEvent(
    long Sequence,
    DateTimeOffset OccurredAtUtc,
    AiGlobalShortlistProgressUpdate Update);

public sealed record AiGlobalShortlistProgressBatch(
    IReadOnlyList<AiGlobalShortlistProgressEvent> Events,
    long NextSequence,
    bool RunFound);

public sealed class InMemoryAiGlobalShortlistProgressStateStore : IAiGlobalShortlistProgressStateStore
{
    private readonly ConcurrentDictionary<Guid, RunState> _runs = new();

    public AiGlobalShortlistProgressEvent Append(AiGlobalShortlistProgressUpdate update)
    {
        var state = _runs.GetOrAdd(update.RunId, static _ => new RunState());
        return state.Append(update);
    }

    public AiGlobalShortlistProgressBatch GetBatch(Guid runId, long afterSequence)
    {
        if (!_runs.TryGetValue(runId, out var state))
        {
            return new AiGlobalShortlistProgressBatch([], 1, false);
        }

        return state.GetBatch(afterSequence);
    }

    private sealed class RunState
    {
        private readonly object _sync = new();
        private readonly List<AiGlobalShortlistProgressEvent> _events = [];
        private long _nextSequence = 1;

        public AiGlobalShortlistProgressEvent Append(AiGlobalShortlistProgressUpdate update)
        {
            lock (_sync)
            {
                var progressEvent = new AiGlobalShortlistProgressEvent(
                    _nextSequence++,
                    DateTimeOffset.UtcNow,
                    update);
                _events.Add(progressEvent);
                return progressEvent;
            }
        }

        public AiGlobalShortlistProgressBatch GetBatch(long afterSequence)
        {
            lock (_sync)
            {
                var events = _events
                    .Where(item => item.Sequence > afterSequence)
                    .ToArray();

                return new AiGlobalShortlistProgressBatch(events, _nextSequence, true);
            }
        }
    }
}
