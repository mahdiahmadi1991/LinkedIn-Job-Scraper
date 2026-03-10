using System.Net;

using System.Diagnostics;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.LinkedIn.Api;

public sealed class LinkedInApiClient : ILinkedInApiClient
{
    private static readonly SemaphoreSlim RequestThrottleGate = new(1, 1);
    private static DateTimeOffset _nextAllowedRequestUtc = DateTimeOffset.MinValue;

    private static readonly HashSet<string> SkippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept-Encoding",
        "Connection",
        "Host",
        "TE"
    };

    private readonly HttpClient _httpClient;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly LinkedInRequestSafetyOptions _requestSafetyOptions;
    private readonly ILinkedInSessionResetRequirementTracker _linkedInSessionResetRequirementTracker;
    private readonly ILinkedInSessionStore _linkedInSessionStore;
    private readonly ILogger<LinkedInApiClient> _logger;

    public LinkedInApiClient(
        HttpClient httpClient,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        IOptions<LinkedInRequestSafetyOptions> requestSafetyOptions,
        ILinkedInSessionResetRequirementTracker linkedInSessionResetRequirementTracker,
        ILinkedInSessionStore linkedInSessionStore,
        ILogger<LinkedInApiClient> logger)
    {
        _httpClient = httpClient;
        _fetchDiagnosticsOptions = fetchDiagnosticsOptions.Value;
        _requestSafetyOptions = requestSafetyOptions.Value;
        _linkedInSessionResetRequirementTracker = linkedInSessionResetRequirementTracker;
        _linkedInSessionStore = linkedInSessionStore;
        _logger = logger;
    }

    public async Task<LinkedInApiResponse> GetAsync(
        Uri requestUri,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        requestMessage.Version = HttpVersion.Version20;
        requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        var diagnosticsEnabled = _fetchDiagnosticsOptions.Enabled;
        string? requestPathAndQuery = null;
        var minimumDelayMilliseconds = _requestSafetyOptions.GetMinimumDelayMilliseconds();
        var maxJitterMilliseconds = _requestSafetyOptions.GetMaxJitterMilliseconds();

        foreach (var header in headers)
        {
            if (SkippedHeaders.Contains(header.Key))
            {
                continue;
            }

            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (diagnosticsEnabled && _logger.IsEnabled(LogLevel.Information))
        {
            requestPathAndQuery = SensitiveDataRedaction.SanitizeForMessage(
                requestUri.PathAndQuery,
                maxLength: 1200);
            Log.LinkedInApiGetStarted(
                _logger,
                requestPathAndQuery,
                headers.Count);
        }

        var throttleDelay = await WaitForSafetyWindowAsync(
            minimumDelayMilliseconds,
            maxJitterMilliseconds,
            cancellationToken);

        if (diagnosticsEnabled && _logger.IsEnabled(LogLevel.Information))
        {
            requestPathAndQuery ??= SensitiveDataRedaction.SanitizeForMessage(
                requestUri.PathAndQuery,
                maxLength: 1200);
            Log.LinkedInApiSafetyThrottleApplied(
                _logger,
                requestPathAndQuery,
                (int)throttleDelay.TotalMilliseconds,
                minimumDelayMilliseconds,
                maxJitterMilliseconds);
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();
        await ApplyResetRequirementFromResponseAsync((int)response.StatusCode, cancellationToken);

        if (diagnosticsEnabled && _logger.IsEnabled(LogLevel.Information))
        {
            requestPathAndQuery ??= SensitiveDataRedaction.SanitizeForMessage(
                requestUri.PathAndQuery,
                maxLength: 1200);
            Log.LinkedInApiGetCompleted(
                _logger,
                requestPathAndQuery,
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                body.Length,
                stopwatch.ElapsedMilliseconds);

            if (_fetchDiagnosticsOptions.LogResponseBodies)
            {
                var bodySample = SensitiveDataRedaction.SanitizeForMessage(
                    body,
                    _fetchDiagnosticsOptions.GetResponseBodyMaxLength());
                Log.LinkedInApiResponseBodySample(
                    _logger,
                    requestPathAndQuery,
                    bodySample);
            }
        }

        return new LinkedInApiResponse((int)response.StatusCode, response.IsSuccessStatusCode, body);
    }

    private async Task ApplyResetRequirementFromResponseAsync(int statusCode, CancellationToken cancellationToken)
    {
        try
        {
            if (statusCode == (int)HttpStatusCode.Unauthorized)
            {
                await _linkedInSessionStore.InvalidateCurrentAsync(cancellationToken);
                _linkedInSessionResetRequirementTracker.MarkRequired(
                    LinkedInSessionResetReasonCodes.SessionUnauthorized,
                    "LinkedIn rejected this session with HTTP 401 (Unauthorized). Reset Session, then reconnect to continue.",
                    statusCode);

                Log.LinkedInApiTriggeredSessionResetRequirement(
                    _logger,
                    statusCode,
                    LinkedInSessionResetReasonCodes.SessionUnauthorized);
                return;
            }

            if (statusCode == (int)HttpStatusCode.Forbidden)
            {
                _linkedInSessionResetRequirementTracker.MarkRequired(
                    LinkedInSessionResetReasonCodes.SessionForbidden,
                    "LinkedIn rejected this session with HTTP 403 (Forbidden). Reset Session, then reconnect to continue.",
                    statusCode);

                Log.LinkedInApiTriggeredSessionResetRequirement(
                    _logger,
                    statusCode,
                    LinkedInSessionResetReasonCodes.SessionForbidden);
            }
        }
        catch (Exception exception)
        {
            Log.LinkedInApiFailedToApplyResetRequirement(_logger, statusCode, exception);
        }
    }

    private async Task<TimeSpan> WaitForSafetyWindowAsync(
        int minimumDelayMilliseconds,
        int maxJitterMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!_requestSafetyOptions.Enabled)
        {
            return TimeSpan.Zero;
        }

        await RequestThrottleGate.WaitAsync(cancellationToken);

        try
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var delay = _nextAllowedRequestUtc - nowUtc;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var jitter = maxJitterMilliseconds == 0
                ? 0
                : Random.Shared.Next(0, maxJitterMilliseconds + 1);

            _nextAllowedRequestUtc = DateTimeOffset.UtcNow.AddMilliseconds(minimumDelayMilliseconds + jitter);

            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }
        finally
        {
            RequestThrottleGate.Release();
        }
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "LinkedIn API GET started. RequestPathAndQuery={RequestPathAndQuery}, HeaderCount={HeaderCount}")]
    public static partial void LinkedInApiGetStarted(ILogger logger, string requestPathAndQuery, int headerCount);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Information,
        Message = "LinkedIn API GET completed. RequestPathAndQuery={RequestPathAndQuery}, StatusCode={StatusCode}, Success={Success}, BodyLength={BodyLength}, ElapsedMilliseconds={ElapsedMilliseconds}")]
    public static partial void LinkedInApiGetCompleted(
        ILogger logger,
        string requestPathAndQuery,
        int statusCode,
        bool success,
        int bodyLength,
        long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Information,
        Message = "LinkedIn API GET response body sample. RequestPathAndQuery={RequestPathAndQuery}, BodySample={BodySample}")]
    public static partial void LinkedInApiResponseBodySample(ILogger logger, string requestPathAndQuery, string bodySample);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Information,
        Message = "LinkedIn API safety throttle applied. RequestPathAndQuery={RequestPathAndQuery}, WaitMilliseconds={WaitMilliseconds}, MinimumDelayMilliseconds={MinimumDelayMilliseconds}, MaxJitterMilliseconds={MaxJitterMilliseconds}")]
    public static partial void LinkedInApiSafetyThrottleApplied(
        ILogger logger,
        string requestPathAndQuery,
        int waitMilliseconds,
        int minimumDelayMilliseconds,
        int maxJitterMilliseconds);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Warning,
        Message = "LinkedIn API response triggered session reset-required state. StatusCode={StatusCode}, ReasonCode={ReasonCode}")]
    public static partial void LinkedInApiTriggeredSessionResetRequirement(
        ILogger logger,
        int statusCode,
        string reasonCode);

    [LoggerMessage(
        EventId = 3106,
        Level = LogLevel.Error,
        Message = "LinkedIn API failed while applying reset-required state. StatusCode={StatusCode}")]
    public static partial void LinkedInApiFailedToApplyResetRequirement(
        ILogger logger,
        int statusCode,
        Exception exception);
}
