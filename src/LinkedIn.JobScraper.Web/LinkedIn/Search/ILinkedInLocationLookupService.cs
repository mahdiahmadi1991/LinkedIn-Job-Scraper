using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public interface ILinkedInLocationLookupService
{
    Task<LinkedInLocationLookupResult> SearchAsync(string query, CancellationToken cancellationToken);
}

public sealed class LinkedInLocationLookupService : ILinkedInLocationLookupService
{
    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly LinkedInRequestOptions _linkedInRequestOptions;
    private readonly ILogger<LinkedInLocationLookupService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInLocationLookupService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        IOptions<LinkedInRequestOptions> linkedInRequestOptions,
        ILogger<LinkedInLocationLookupService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _linkedInRequestOptions = linkedInRequestOptions.Value;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task<LinkedInLocationLookupResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LinkedInLocationLookupResult.Failed("A location query is required.");
        }

        var sessionSnapshot = await _sessionStore.GetCurrentAsync(cancellationToken);

        if (sessionSnapshot is null)
        {
            return LinkedInLocationLookupResult.Failed("A stored LinkedIn session is required before location lookup.");
        }

        var response = await _linkedInApiClient.GetAsync(
            LinkedInRequestDefaults.BuildGeoTypeaheadUri(
                query.Trim(),
                _linkedInRequestOptions.GeoTypeaheadQueryId),
            BuildHeaders(sessionSnapshot),
            cancellationToken);

        if (response.StatusCode != StatusCodes.Status200OK)
        {
            Log.LinkedInLocationLookupReturnedNonSuccessStatusCode(_logger, response.StatusCode);

            return LinkedInLocationLookupResult.Failed(
                $"LinkedIn location lookup failed with HTTP {response.StatusCode}.",
                response.StatusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var root = document.RootElement;

            if (HasGraphQlErrors(root))
            {
                return LinkedInLocationLookupResult.Failed(
                    "LinkedIn location lookup returned an error payload.",
                    StatusCodes.Status502BadGateway);
            }

            if (!TryReadSuggestions(root, out var suggestions))
            {
                return LinkedInLocationLookupResult.Failed(
                    "LinkedIn location lookup returned an unexpected payload.",
                    StatusCodes.Status502BadGateway);
            }

            return LinkedInLocationLookupResult.Succeeded(suggestions);
        }
        catch (JsonException exception)
        {
            Log.LinkedInLocationLookupFailedToParseJson(_logger, exception);

            return LinkedInLocationLookupResult.Failed(
                "LinkedIn location lookup returned invalid JSON.",
                StatusCodes.Status502BadGateway);
        }
    }

    private static Dictionary<string, string> BuildHeaders(LinkedInSessionSnapshot sessionSnapshot)
    {
        var headers = new Dictionary<string, string>(sessionSnapshot.Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["x-li-pem-metadata"] = "Voyager - Search Single Typeahead=jobs-geo"
        };

        if (!headers.TryGetValue("Referer", out var referer) || string.IsNullOrWhiteSpace(referer))
        {
            headers["Referer"] = "https://www.linkedin.com/jobs/search/";
        }

        return headers;
    }

    private static bool HasGraphQlErrors(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataNode) ||
            dataNode.ValueKind != JsonValueKind.Object ||
            !dataNode.TryGetProperty("errors", out var errorsNode))
        {
            return false;
        }

        return errorsNode.ValueKind == JsonValueKind.Array && errorsNode.GetArrayLength() > 0;
    }

    private static bool TryReadSuggestions(
        JsonElement root,
        out IReadOnlyList<LinkedInLocationSuggestion> suggestions)
    {
        suggestions = Array.Empty<LinkedInLocationSuggestion>();

        if (!root.TryGetProperty("data", out var dataNode) ||
            dataNode.ValueKind != JsonValueKind.Object ||
            !dataNode.TryGetProperty("data", out var nestedDataNode) ||
            nestedDataNode.ValueKind != JsonValueKind.Object ||
            !nestedDataNode.TryGetProperty("searchDashReusableTypeaheadByType", out var typeaheadNode) ||
            typeaheadNode.ValueKind != JsonValueKind.Object ||
            !typeaheadNode.TryGetProperty("elements", out var elementsNode) ||
            elementsNode.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsedSuggestions = new List<LinkedInLocationSuggestion>();

        foreach (var element in elementsNode.EnumerateArray())
        {
            if (!element.TryGetProperty("title", out var titleNode) ||
                !titleNode.TryGetProperty("text", out var textNode) ||
                textNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!element.TryGetProperty("target", out var targetNode) ||
                !targetNode.TryGetProperty("*geo", out var geoUrnNode) ||
                geoUrnNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var displayName = textNode.GetString();
            var geoUrn = geoUrnNode.GetString();
            var geoId = ExtractGeoId(geoUrn);

            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(geoId))
            {
                continue;
            }

            parsedSuggestions.Add(new LinkedInLocationSuggestion(geoId, displayName));
        }

        suggestions = parsedSuggestions;
        return true;
    }

    private static string? ExtractGeoId(string? geoUrn)
    {
        if (string.IsNullOrWhiteSpace(geoUrn))
        {
            return null;
        }

        var colonIndex = geoUrn.LastIndexOf(':');

        if (colonIndex < 0 || colonIndex == geoUrn.Length - 1)
        {
            return null;
        }

        return geoUrn[(colonIndex + 1)..];
    }
}

public sealed record LinkedInLocationLookupResult(
    bool Success,
    string Message,
    int StatusCode,
    IReadOnlyList<LinkedInLocationSuggestion> Suggestions)
{
    public static LinkedInLocationLookupResult Failed(string message, int statusCode = StatusCodes.Status400BadRequest) =>
        new(false, message, statusCode, Array.Empty<LinkedInLocationSuggestion>());

    public static LinkedInLocationLookupResult Succeeded(IReadOnlyList<LinkedInLocationSuggestion> suggestions) =>
        new(true, "LinkedIn location lookup completed.", StatusCodes.Status200OK, suggestions);
}

public sealed record LinkedInLocationSuggestion(
    string GeoId,
    string DisplayName);

internal static partial class Log
{
    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Warning,
        Message = "LinkedIn location lookup returned non-success status code {StatusCode}.")]
    public static partial void LinkedInLocationLookupReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 3202,
        Level = LogLevel.Error,
        Message = "LinkedIn location lookup failed to parse JSON.")]
    public static partial void LinkedInLocationLookupFailedToParseJson(ILogger logger, Exception exception);
}
