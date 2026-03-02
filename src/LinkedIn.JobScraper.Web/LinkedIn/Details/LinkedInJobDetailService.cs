using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace LinkedIn.JobScraper.Web.LinkedIn.Details;

public sealed class LinkedInJobDetailService : ILinkedInJobDetailService
{
    private const string JobPostingType = "com.linkedin.voyager.dash.jobs.JobPosting";
    private const string EmploymentStatusType = "com.linkedin.voyager.dash.hiring.EmploymentStatus";
    private const string GeoType = "com.linkedin.voyager.dash.common.Geo";

    private readonly IWebHostEnvironment _environment;
    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly ILogger<LinkedInJobDetailService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInJobDetailService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        IWebHostEnvironment environment,
        ILogger<LinkedInJobDetailService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _sessionStore = sessionStore;
        _environment = environment;
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
                "No stored LinkedIn session is available yet.",
                StatusCodes.Status502BadGateway);
        }

        var requestFilePath = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, "..", "..", "docs", "api-sample", "job-detail-request.txt"));

        if (!File.Exists(requestFilePath))
        {
            return LinkedInJobDetailFetchResult.Failed(
                $"Sample request file was not found at '{requestFilePath}'.",
                StatusCodes.Status500InternalServerError);
        }

        var fileContent = await File.ReadAllTextAsync(requestFilePath, cancellationToken);
        var parsedRequest = LinkedInCapturedRequestParser.Parse(fileContent);

        if (!parsedRequest.IsValid)
        {
            return LinkedInJobDetailFetchResult.Failed(
                parsedRequest.ErrorMessage!,
                StatusCodes.Status500InternalServerError);
        }

        var jobPostingUrn = $"urn:li:fsd_jobPosting:{linkedInJobId}";
        var requestUri = BuildRequestUri(parsedRequest.Url!, jobPostingUrn);
        var headers = MergeHeaders(parsedRequest.Headers, sessionSnapshot, linkedInJobId);

        var response = await _linkedInApiClient.GetAsync(requestUri, headers, cancellationToken);

        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            Log.LinkedInJobDetailReturnedNonSuccessStatusCode(_logger, response.StatusCode);

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

    private static Uri BuildRequestUri(Uri sampleUri, string jobPostingUrn)
    {
        var query = QueryHelpers.ParseQuery(sampleUri.Query);
        var queryId = query.TryGetValue("queryId", out var queryIdValues)
            ? queryIdValues.ToString()
            : string.Empty;

        var encodedJobPostingUrn = Uri.EscapeDataString(jobPostingUrn);
        var variablesValue = $"(jobPostingUrn:{encodedJobPostingUrn})";
        var queryText = $"variables={variablesValue}";

        if (!string.IsNullOrWhiteSpace(queryId))
        {
            queryText = $"{queryText}&queryId={Uri.EscapeDataString(queryId)}";
        }

        var builder = new UriBuilder(sampleUri)
        {
            Query = queryText
        };

        return builder.Uri;
    }

    private static Dictionary<string, string> MergeHeaders(
        IReadOnlyDictionary<string, string> requestHeaders,
        LinkedInSessionSnapshot sessionSnapshot,
        string linkedInJobId)
    {
        var merged = new Dictionary<string, string>(requestHeaders, StringComparer.OrdinalIgnoreCase);

        foreach (var header in sessionSnapshot.Headers)
        {
            merged[header.Key] = header.Value;
        }

        if (merged.TryGetValue("Referer", out var referer))
        {
            merged["Referer"] = ReplaceCurrentJobIdInReferer(referer, linkedInJobId);
        }

        return merged;
    }

    private static string ReplaceCurrentJobIdInReferer(string referer, string linkedInJobId)
    {
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            return referer;
        }

        var query = QueryHelpers.ParseQuery(refererUri.Query);
        var queryBuilder = new QueryBuilder();

        foreach (var pair in query)
        {
            var value = string.Equals(pair.Key, "currentJobId", StringComparison.OrdinalIgnoreCase)
                ? linkedInJobId
                : pair.Value.ToString();

            queryBuilder.Add(pair.Key, value);
        }

        var builder = new UriBuilder(refererUri)
        {
            Query = queryBuilder.ToQueryString().Value?.TrimStart('?')
        };

        return builder.Uri.AbsoluteUri;
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
