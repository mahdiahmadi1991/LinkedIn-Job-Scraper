using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;

namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public sealed class LinkedInJobSearchService : ILinkedInJobSearchService
{
    private const string JobPostingCardType = "com.linkedin.voyager.dash.jobs.JobPostingCard";

    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly ILogger<LinkedInJobSearchService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInJobSearchService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        ILogger<LinkedInJobSearchService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task<LinkedInJobSearchFetchResult> FetchCurrentSearchAsync(CancellationToken cancellationToken)
    {
        var sessionSnapshot = await _sessionStore.GetCurrentAsync(cancellationToken);

        if (sessionSnapshot is null)
        {
            return LinkedInJobSearchFetchResult.Failed(
                "No stored LinkedIn session is available yet.",
                StatusCodes.Status502BadGateway);
        }

        var headers = MergeHeaders(sessionSnapshot);
        var response = await _linkedInApiClient.GetAsync(
            LinkedInRequestDefaults.BuildSearchUri(),
            headers,
            cancellationToken);

        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            Log.LinkedInSearchReturnedNonSuccessStatusCode(_logger, response.StatusCode);

            return LinkedInJobSearchFetchResult.Failed(
                $"LinkedIn job search failed with HTTP {response.StatusCode}.",
                response.StatusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var dataNode))
            {
                return LinkedInJobSearchFetchResult.Failed(
                    "Response JSON did not contain a top-level 'data' node.",
                    StatusCodes.Status502BadGateway);
            }

            var returnedCardUrns = ReadReturnedCardUrns(dataNode);
            var totalCount = ReadTotalCount(dataNode);
            var cardsByUrn = ReadIncludedCards(root);

            var orderedJobs = new List<LinkedInJobSearchItem>(returnedCardUrns.Count);

            foreach (var cardUrn in returnedCardUrns)
            {
                if (cardsByUrn.TryGetValue(cardUrn, out var item))
                {
                    orderedJobs.Add(item);
                }
            }

            return LinkedInJobSearchFetchResult.Succeeded(
                response.StatusCode,
                returnedCardUrns.Count,
                totalCount,
                orderedJobs);
        }
        catch (JsonException exception)
        {
            Log.LinkedInSearchFailedToParseJson(_logger, exception);

            return LinkedInJobSearchFetchResult.Failed(
                "LinkedIn search response was not valid JSON.",
                StatusCodes.Status502BadGateway);
        }
    }

    private static Dictionary<string, LinkedInJobSearchItem> ReadIncludedCards(JsonElement root)
    {
        var results = new Dictionary<string, LinkedInJobSearchItem>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("included", out var includedNode) ||
            includedNode.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var includedItem in includedNode.EnumerateArray())
        {
            if (!HasType(includedItem, JobPostingCardType) ||
                !includedItem.TryGetProperty("entityUrn", out var entityUrnNode))
            {
                continue;
            }

            var cardUrn = entityUrnNode.GetString();

            if (string.IsNullOrWhiteSpace(cardUrn))
            {
                continue;
            }

            var postingUrn = ReadString(includedItem, "*jobPosting");

            if (string.IsNullOrWhiteSpace(postingUrn))
            {
                continue;
            }

            var linkedInJobId = ExtractJobId(postingUrn);

            if (string.IsNullOrWhiteSpace(linkedInJobId))
            {
                continue;
            }

            results[cardUrn] = new LinkedInJobSearchItem(
                linkedInJobId,
                postingUrn,
                cardUrn,
                ReadTextValue(includedItem, "title") ?? linkedInJobId,
                ReadTextValue(includedItem, "primaryDescription"),
                ReadTextValue(includedItem, "secondaryDescription"),
                ReadListedAtUtc(includedItem));
        }

        return results;
    }

    private static List<string> ReadReturnedCardUrns(JsonElement dataNode)
    {
        var urns = new List<string>();

        if (!dataNode.TryGetProperty("elements", out var elementsNode) ||
            elementsNode.ValueKind != JsonValueKind.Array)
        {
            return urns;
        }

        foreach (var element in elementsNode.EnumerateArray())
        {
            if (!element.TryGetProperty("jobCardUnion", out var unionNode) ||
                !unionNode.TryGetProperty("*jobPostingCard", out var cardUrnNode))
            {
                continue;
            }

            var cardUrn = cardUrnNode.GetString();

            if (!string.IsNullOrWhiteSpace(cardUrn))
            {
                urns.Add(cardUrn);
            }
        }

        return urns;
    }

    private static int ReadTotalCount(JsonElement dataNode)
    {
        if (dataNode.TryGetProperty("paging", out var pagingNode) &&
            pagingNode.TryGetProperty("total", out var totalNode) &&
            totalNode.TryGetInt32(out var totalCount))
        {
            return totalCount;
        }

        return 0;
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

    private static string? ReadTextValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyNode) ||
            propertyNode.ValueKind != JsonValueKind.Object ||
            !propertyNode.TryGetProperty("text", out var textNode) ||
            textNode.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return textNode.GetString();
    }

    private static DateTimeOffset? ReadListedAtUtc(JsonElement element)
    {
        if (!element.TryGetProperty("footerItems", out var footerItemsNode) ||
            footerItemsNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var footerItem in footerItemsNode.EnumerateArray())
        {
            if (!footerItem.TryGetProperty("type", out var typeNode) ||
                !string.Equals(typeNode.GetString(), "LISTED_DATE", StringComparison.Ordinal) ||
                !footerItem.TryGetProperty("timeAt", out var timeAtNode) ||
                !timeAtNode.TryGetInt64(out var unixMilliseconds))
            {
                continue;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }

        return null;
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

    private static Dictionary<string, string> MergeHeaders(LinkedInSessionSnapshot sessionSnapshot)
    {
        var merged = new Dictionary<string, string>(sessionSnapshot.Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["Referer"] = LinkedInRequestDefaults.BuildSearchReferer(),
            ["x-li-pem-metadata"] = LinkedInRequestDefaults.SearchPemMetadata
        };

        return merged;
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "LinkedIn job search returned non-success status code {StatusCode}.")]
    public static partial void LinkedInSearchReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Error,
        Message = "Failed to parse LinkedIn job search response JSON.")]
    public static partial void LinkedInSearchFailedToParseJson(ILogger logger, Exception exception);
}
