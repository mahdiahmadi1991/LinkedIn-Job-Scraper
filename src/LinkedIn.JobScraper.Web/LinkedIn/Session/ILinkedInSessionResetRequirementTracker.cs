using System.Collections.Concurrent;
using LinkedIn.JobScraper.Web.Authentication;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInSessionResetRequirementTracker
{
    LinkedInSessionResetRequirementState GetCurrent();

    void MarkRequired(string reasonCode, string message, int? statusCode = null);

    void Clear();
}

public sealed class InMemoryLinkedInSessionResetRequirementTracker : ILinkedInSessionResetRequirementTracker
{
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly ILogger<InMemoryLinkedInSessionResetRequirementTracker> _logger;
    private readonly ConcurrentDictionary<int, LinkedInSessionResetRequirementState> _resetRequirements = new();

    public InMemoryLinkedInSessionResetRequirementTracker(
        ICurrentAppUserContext currentAppUserContext,
        ILogger<InMemoryLinkedInSessionResetRequirementTracker> logger)
    {
        _currentAppUserContext = currentAppUserContext;
        _logger = logger;
    }

    public LinkedInSessionResetRequirementState GetCurrent()
    {
        if (!_currentAppUserContext.TryGetUserId(out var userId))
        {
            return LinkedInSessionResetRequirementState.NotRequired;
        }

        return _resetRequirements.TryGetValue(userId, out var state)
            ? state
            : LinkedInSessionResetRequirementState.NotRequired;
    }

    public void MarkRequired(string reasonCode, string message, int? statusCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!_currentAppUserContext.TryGetUserId(out var userId))
        {
            return;
        }

        var state = new LinkedInSessionResetRequirementState(
            true,
            reasonCode,
            message,
            statusCode,
            DateTimeOffset.UtcNow);

        _resetRequirements.AddOrUpdate(userId, state, (_, _) => state);
        Log.LinkedInSessionResetRequiredStateSet(
            _logger,
            userId,
            reasonCode,
            statusCode);
    }

    public void Clear()
    {
        if (!_currentAppUserContext.TryGetUserId(out var userId))
        {
            return;
        }

        if (_resetRequirements.TryRemove(userId, out _))
        {
            Log.LinkedInSessionResetRequiredStateCleared(_logger, userId);
        }
    }
}

public sealed record LinkedInSessionResetRequirementState(
    bool Required,
    string? ReasonCode,
    string? Message,
    int? StatusCode,
    DateTimeOffset? RequiredAtUtc)
{
    public static LinkedInSessionResetRequirementState NotRequired { get; } =
        new(false, null, null, null, null);
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 2402,
        Level = LogLevel.Information,
        Message = "LinkedIn session reset-required state set. AppUserId={AppUserId}, ReasonCode={ReasonCode}, StatusCode={StatusCode}")]
    public static partial void LinkedInSessionResetRequiredStateSet(
        ILogger logger,
        int appUserId,
        string reasonCode,
        int? statusCode);

    [LoggerMessage(
        EventId = 2403,
        Level = LogLevel.Information,
        Message = "LinkedIn session reset-required state cleared. AppUserId={AppUserId}")]
    public static partial void LinkedInSessionResetRequiredStateCleared(
        ILogger logger,
        int appUserId);
}
