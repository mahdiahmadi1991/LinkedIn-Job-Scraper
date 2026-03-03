using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.LinkedIn.Api;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class LinkedInSessionVerificationService : ILinkedInSessionVerificationService
{
    private const string GeoTypeaheadQueryId = "voyagerSearchDashReusableTypeahead.4c7caa85341b17b470153ad3d1a29caf";

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
            var matchedLocationName = TryReadFirstLocationName(document.RootElement);

            if (string.IsNullOrWhiteSpace(matchedLocationName))
            {
                return LinkedInSessionVerificationResult.Failed(
                    "Stored session verification failed because LinkedIn returned an unexpected payload.",
                    response.StatusCode);
            }

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

    private static Uri BuildVerificationUri()
    {
        const string variables =
            "(keywords:Cyprus,query:(typeaheadFilterQuery:(geoSearchTypes:List(POSTCODE_1,POSTCODE_2,POPULATED_PLACE,ADMIN_DIVISION_1,ADMIN_DIVISION_2,COUNTRY_REGION,MARKET_AREA,COUNTRY_CLUSTER)),typeaheadUseCase:JOBS),type:GEO)";
        var rawUri = $"https://www.linkedin.com/voyager/api/graphql?variables={variables}&queryId={GeoTypeaheadQueryId}";
        return new Uri(rawUri, UriKind.Absolute);
    }

    private static Dictionary<string, string> BuildHeaders(LinkedInSessionSnapshot sessionSnapshot)
    {
        var headers = new Dictionary<string, string>(sessionSnapshot.Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["x-li-pem-metadata"] = "Voyager - Search Single Typeahead=jobs-geo",
            ["Referer"] =
                "https://www.linkedin.com/jobs/search/?keywords=C%23%20.Net&origin=JOB_SEARCH_PAGE_LOCATION_AUTOCOMPLETE&refresh=true"
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
}
