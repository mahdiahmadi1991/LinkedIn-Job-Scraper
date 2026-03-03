using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class LinkedInSessionVerificationService : ILinkedInSessionVerificationService
{
    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly LinkedInRequestOptions _linkedInRequestOptions;
    private readonly ILogger<LinkedInSessionVerificationService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInSessionVerificationService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        IOptions<LinkedInRequestOptions> linkedInRequestOptions,
        ILogger<LinkedInSessionVerificationService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _sessionStore = sessionStore;
        _linkedInRequestOptions = linkedInRequestOptions.Value;
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

        var requestUri = BuildVerificationUri(_linkedInRequestOptions.GraphQlQueryId);
        var headers = BuildHeaders(sessionSnapshot);
        var response = await _linkedInApiClient.GetAsync(requestUri, headers, cancellationToken);

        if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
        {
            await _sessionStore.InvalidateCurrentAsync(cancellationToken);
            Log.LinkedInSessionVerificationInvalidatedExpiredSession(_logger);

            return LinkedInSessionVerificationResult.Failed(
                "Stored LinkedIn session has expired. Use the session control to capture a new one.",
                response.StatusCode);
        }

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
            var matchedLocationName = TryReadFirstLocationName(document.RootElement);

            if (string.IsNullOrWhiteSpace(matchedLocationName))
            {
                return LinkedInSessionVerificationResult.Failed(
                    "Stored session verification failed because LinkedIn returned an unexpected payload.",
                    response.StatusCode);
            }

            await _sessionStore.MarkCurrentValidatedAsync(DateTimeOffset.UtcNow, cancellationToken);

            return LinkedInSessionVerificationResult.Succeeded(
                $"Stored session is valid. LinkedIn location lookup is responding normally for '{matchedLocationName}'.",
                response.StatusCode,
                matchedLocationName);
        }
        catch (JsonException exception)
        {
            Log.LinkedInSessionVerificationFailedToParseJson(_logger, exception);

            return LinkedInSessionVerificationResult.Failed(
                "Stored session verification failed because LinkedIn returned invalid JSON.",
                response.StatusCode);
        }
    }

    private static Uri BuildVerificationUri(string? graphQlQueryId)
    {
        return LinkedInRequestDefaults.BuildGeoTypeaheadUri("Cyprus", graphQlQueryId);
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

    private static string? TryReadFirstLocationName(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataNode) ||
            !dataNode.TryGetProperty("data", out var nestedDataNode) ||
            !nestedDataNode.TryGetProperty("searchDashReusableTypeaheadByType", out var typeaheadNode) ||
            !typeaheadNode.TryGetProperty("elements", out var elementsNode) ||
            elementsNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var element in elementsNode.EnumerateArray())
        {
            if (!element.TryGetProperty("title", out var titleNode) ||
                !titleNode.TryGetProperty("text", out var textNode) ||
                textNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var locationName = textNode.GetString();

            if (!string.IsNullOrWhiteSpace(locationName))
            {
                return locationName;
            }
        }

        return null;
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
