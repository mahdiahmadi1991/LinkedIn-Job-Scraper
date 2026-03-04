using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.LinkedIn;
using LinkedIn.JobScraper.Web.LinkedIn.Api;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class LinkedInSessionVerificationService : ILinkedInSessionVerificationService
{
    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly ILogger<LinkedInSessionVerificationService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInSessionVerificationService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        ILogger<LinkedInSessionVerificationService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
    {
        var sessionSnapshot = await _sessionStore.GetCurrentAsync(cancellationToken);

        if (sessionSnapshot is null)
        {
            return LinkedInSessionVerificationResult.Failed(
                "No stored LinkedIn session is available yet.");
        }

        var result = await VerifySnapshotAsync(sessionSnapshot, cancellationToken);

        if (!result.Success && result.StatusCode == (int)HttpStatusCode.Unauthorized)
        {
            await _sessionStore.InvalidateCurrentAsync(cancellationToken);
            Log.LinkedInSessionVerificationInvalidatedExpiredSession(_logger);

            return LinkedInSessionVerificationResult.Failed(
                "Stored LinkedIn session has expired. Use the session control to capture a new one.",
                result.StatusCode);
        }

        if (result.Success)
        {
            await _sessionStore.MarkCurrentValidatedAsync(DateTimeOffset.UtcNow, cancellationToken);
        }

        return result;
    }

    public Task<LinkedInSessionVerificationResult> VerifyAsync(
        LinkedInSessionSnapshot sessionSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionSnapshot);

        return VerifySnapshotAsync(sessionSnapshot, cancellationToken);
    }

    private async Task<LinkedInSessionVerificationResult> VerifySnapshotAsync(
        LinkedInSessionSnapshot sessionSnapshot,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildVerificationUri();
        var headers = BuildHeaders(sessionSnapshot);
        var response = await _linkedInApiClient.GetAsync(requestUri, headers, cancellationToken);

        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            Log.LinkedInSessionVerificationReturnedNonSuccessStatusCode(_logger, response.StatusCode);

            return LinkedInSessionVerificationResult.Failed(
                $"Stored session verification failed with HTTP {response.StatusCode}.",
                response.StatusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var resultCardCount = TryReadResultCardCount(document.RootElement);

            if (resultCardCount is null)
            {
                return LinkedInSessionVerificationResult.Failed(
                    "Stored session verification failed because LinkedIn returned an unexpected payload.");
            }

            return LinkedInSessionVerificationResult.Succeeded(
                $"Stored session is valid. LinkedIn jobs search responded normally and returned {resultCardCount.Value} included records.",
                response.StatusCode);
        }
        catch (JsonException exception)
        {
            Log.LinkedInSessionVerificationFailedToParseJson(_logger, exception);

            return LinkedInSessionVerificationResult.Failed(
                "Stored session verification failed because LinkedIn returned invalid JSON.");
        }
    }

    private static Uri BuildVerificationUri()
    {
        return LinkedInRequestDefaults.BuildSearchUri(
            "Cyprus",
            locationGeoId: null,
            easyApply: false,
            jobTypeCodes: [],
            workplaceTypeCodes: [],
            start: 0,
            count: 1);
    }

    private static Dictionary<string, string> BuildHeaders(LinkedInSessionSnapshot sessionSnapshot)
    {
        var headers = new Dictionary<string, string>(sessionSnapshot.Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["Referer"] = LinkedInRequestDefaults.BuildSearchReferer(
                "Cyprus",
                locationGeoId: null,
                easyApply: false,
                jobTypeCodes: [],
                workplaceTypeCodes: [])
        };

        return headers;
    }

    private static int? TryReadResultCardCount(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataNode) ||
            dataNode.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("included", out var includedNode) ||
            includedNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        if (dataNode.TryGetProperty("errors", out var errorsNode) &&
            errorsNode.ValueKind == JsonValueKind.Array &&
            errorsNode.GetArrayLength() > 0)
        {
            return null;
        }

        return includedNode.GetArrayLength();
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "LinkedIn session verification returned non-success status code {StatusCode}.")]
    public static partial void LinkedInSessionVerificationReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Error,
        Message = "LinkedIn session verification failed to parse JSON.")]
    public static partial void LinkedInSessionVerificationFailedToParseJson(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Warning,
        Message = "Stored LinkedIn session was invalidated after a 401 response during verification.")]
    public static partial void LinkedInSessionVerificationInvalidatedExpiredSession(ILogger logger);
}
