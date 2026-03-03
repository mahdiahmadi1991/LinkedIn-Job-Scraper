using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.LinkedIn.Details;

public sealed class LinkedInJobDetailService : ILinkedInJobDetailService
{
    private const string JobPostingType = "com.linkedin.voyager.dash.jobs.JobPosting";
    private const string EmploymentStatusType = "com.linkedin.voyager.dash.hiring.EmploymentStatus";
    private const string GeoType = "com.linkedin.voyager.dash.common.Geo";

    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly LinkedInRequestOptions _linkedInRequestOptions;
    private readonly ILinkedInSearchSettingsService _linkedInSearchSettingsService;
    private readonly ILogger<LinkedInJobDetailService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInJobDetailService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        ILinkedInSearchSettingsService linkedInSearchSettingsService,
        IOptions<LinkedInRequestOptions> linkedInRequestOptions,
        ILogger<LinkedInJobDetailService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _sessionStore = sessionStore;
        _linkedInSearchSettingsService = linkedInSearchSettingsService;
        _linkedInRequestOptions = linkedInRequestOptions.Value;
        _logger = logger;
    }

    public async Task<LinkedInJobDetailFetchResult> FetchAsync(
        string linkedInJobId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(linkedInJobId))
        {
            return LinkedInJobDetailFetchResult.Failed(
                "LinkedIn job id is required.",
                StatusCodes.Status400BadRequest);
        }

        var sessionSnapshot = await _sessionStore.GetCurrentAsync(cancellationToken);

        if (sessionSnapshot is null)
        {
            return LinkedInJobDetailFetchResult.Failed(
                "No active LinkedIn session is available. Use the session control to capture a new one before enriching jobs.",
                StatusCodes.Status502BadGateway);
        }

        var searchSettings = await _linkedInSearchSettingsService.GetActiveAsync(cancellationToken);
        var jobPostingUrn = $"urn:li:fsd_jobPosting:{linkedInJobId}";
        var requestUri = LinkedInRequestDefaults.BuildJobDetailUri(
            linkedInJobId,
            _linkedInRequestOptions.GraphQlQueryId);
        var headers = MergeHeaders(sessionSnapshot, searchSettings);

        var response = await _linkedInApiClient.GetAsync(requestUri, headers, cancellationToken);

        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            Log.LinkedInJobDetailReturnedNonSuccessStatusCode(_logger, response.StatusCode);

            if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                await _sessionStore.InvalidateCurrentAsync(cancellationToken);

                return LinkedInJobDetailFetchResult.Failed(
                    "Stored LinkedIn session expired during job enrichment. Use the session control to capture a new one.",
                    response.StatusCode);
            }

            return LinkedInJobDetailFetchResult.Failed(
                $"LinkedIn job detail request failed with HTTP {response.StatusCode}.",
                response.StatusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var envelopeNode))
            {
                return LinkedInJobDetailFetchResult.Failed(
                    "Response JSON did not contain a top-level 'data' node.",
                    StatusCodes.Status502BadGateway);
            }

            var warnings = ReadWarnings(envelopeNode);
            var targetPostingUrn = ReadTargetPostingUrn(envelopeNode) ?? jobPostingUrn;

            if (!TryReadJobDetail(root, targetPostingUrn, out var jobDetail))
            {
                var message = warnings.Count > 0
                    ? $"LinkedIn job detail payload was missing the requested job node. Warnings: {string.Join(" | ", warnings)}"
                    : "LinkedIn job detail payload was missing the requested job node.";

                return LinkedInJobDetailFetchResult.Failed(
                    message,
                    StatusCodes.Status502BadGateway,
                    warnings);
            }

            return LinkedInJobDetailFetchResult.Succeeded(response.StatusCode, jobDetail!, warnings);
        }
        catch (JsonException exception)
        {
            Log.LinkedInJobDetailFailedToParseJson(_logger, exception);

            return LinkedInJobDetailFetchResult.Failed(
                "LinkedIn job detail response was not valid JSON.",
                StatusCodes.Status502BadGateway);
        }
    }

    private static Dictionary<string, string> MergeHeaders(
        LinkedInSessionSnapshot sessionSnapshot,
        LinkedInSearchSettings searchSettings)
    {
        var merged = new Dictionary<string, string>(sessionSnapshot.Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["Referer"] = LinkedInRequestDefaults.BuildJobDetailReferer(
                searchSettings.Keywords,
                searchSettings.LocationGeoId)
        };

        return merged;
    }

    private static List<string> ReadWarnings(JsonElement envelopeNode)
    {
        var warnings = new List<string>();

        if (!envelopeNode.TryGetProperty("errors", out var errorsNode) ||
            errorsNode.ValueKind != JsonValueKind.Array)
        {
            return warnings;
        }

        foreach (var errorNode in errorsNode.EnumerateArray())
        {
            if (!errorNode.TryGetProperty("message", out var messageNode) ||
                messageNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var message = messageNode.GetString();

            if (!string.IsNullOrWhiteSpace(message))
            {
                warnings.Add(message);
            }
        }

        return warnings;
    }

    private static string? ReadTargetPostingUrn(JsonElement envelopeNode)
    {
        if (!envelopeNode.TryGetProperty("data", out var dataNode) ||
            dataNode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in dataNode.EnumerateObject())
        {
            if (!property.Name.StartsWith("*jobsDashJobPostingsById", StringComparison.Ordinal) ||
                property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            return property.Value.GetString();
        }

        return null;
    }

    private static bool TryReadJobDetail(
        JsonElement root,
        string targetPostingUrn,
        out LinkedInJobDetailData? result)
    {
        result = null;

        if (!root.TryGetProperty("included", out var includedNode) ||
            includedNode.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var includedItems = includedNode.EnumerateArray().ToArray();
        var employmentByUrn = includedItems
            .Where(static item => HasType(item, EmploymentStatusType))
            .Select(
                static item => new
                {
                    Urn = ReadString(item, "entityUrn"),
                    Name = ReadString(item, "localizedName")
                })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Urn))
            .ToDictionary(
                static item => item.Urn!,
                static item => item.Name,
                StringComparer.OrdinalIgnoreCase);

        var geoByUrn = includedItems
            .Where(static item => HasType(item, GeoType))
            .Select(
                static item => new
                {
                    Urn = ReadString(item, "entityUrn"),
                    Name = ReadString(item, "defaultLocalizedName") ??
                           ReadString(item, "abbreviatedLocalizedName")
                })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Urn))
            .ToDictionary(
                static item => item.Urn!,
                static item => item.Name,
                StringComparer.OrdinalIgnoreCase);

        var jobPostingNode = includedItems.FirstOrDefault(
            item => HasType(item, JobPostingType) &&
                    string.Equals(ReadString(item, "entityUrn"), targetPostingUrn, StringComparison.OrdinalIgnoreCase));

        if (jobPostingNode.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        var linkedInJobId = ExtractJobId(targetPostingUrn);
        var employmentStatusUrn = ReadString(jobPostingNode, "*employmentStatus");
        var locationUrn = ReadString(jobPostingNode, "*location");

        result = new LinkedInJobDetailData(
            linkedInJobId,
            targetPostingUrn,
            ReadString(jobPostingNode, "title") ?? linkedInJobId,
            ReadNestedString(jobPostingNode, "companyDetails", "name"),
            ResolveLookup(geoByUrn, locationUrn),
            ResolveLookup(employmentByUrn, employmentStatusUrn),
            ReadNestedText(jobPostingNode, "description"),
            ReadString(jobPostingNode, "companyApplyUrl"),
            ReadUnixMilliseconds(jobPostingNode, "listedAt") ??
            ReadUnixMilliseconds(jobPostingNode, "originalListedAt"));

        return true;
    }

    private static bool HasType(JsonElement element, string expectedType)
    {
        return element.TryGetProperty("$type", out var typeNode) &&
               string.Equals(typeNode.GetString(), expectedType, StringComparison.Ordinal);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyNode) ||
            propertyNode.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return propertyNode.GetString();
    }

    private static string? ReadNestedString(JsonElement element, string objectPropertyName, string propertyName)
    {
        if (!element.TryGetProperty(objectPropertyName, out var objectNode) ||
            objectNode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(objectNode, propertyName);
    }

    private static string? ReadNestedText(JsonElement element, string objectPropertyName)
    {
        if (!element.TryGetProperty(objectPropertyName, out var objectNode) ||
            objectNode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(objectNode, "text");
    }

    private static string? ResolveLookup(
        Dictionary<string, string?> values,
        string? urn)
    {
        if (string.IsNullOrWhiteSpace(urn))
        {
            return null;
        }

        return values.TryGetValue(urn, out var value) ? value : null;
    }

    private static DateTimeOffset? ReadUnixMilliseconds(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyNode) ||
            !propertyNode.TryGetInt64(out var milliseconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private static string ExtractJobId(string postingUrn)
    {
        var colonIndex = postingUrn.LastIndexOf(':');

        if (colonIndex < 0 || colonIndex == postingUrn.Length - 1)
        {
            return string.Empty;
        }

        return postingUrn[(colonIndex + 1)..];
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "LinkedIn job detail returned non-success status code {StatusCode}.")]
    public static partial void LinkedInJobDetailReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Failed to parse LinkedIn job detail response JSON.")]
    public static partial void LinkedInJobDetailFailedToParseJson(ILogger logger, Exception exception);
}
