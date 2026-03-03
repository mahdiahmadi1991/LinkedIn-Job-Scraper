using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public sealed class LinkedInJobSearchService : ILinkedInJobSearchService
{
    private const string JobPostingCardType = "com.linkedin.voyager.dash.jobs.JobPostingCard";
    private const int SearchPageCap = LinkedInRequestDefaults.DefaultSearchPageCap;
    private const int SearchJobCap = LinkedInRequestDefaults.DefaultSearchJobCap;
    private static readonly TimeSpan SearchPageDelay = TimeSpan.FromMilliseconds(
        LinkedInRequestDefaults.DefaultSearchPageDelayMilliseconds);

    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly ILogger<LinkedInJobSearchService> _logger;
    private readonly ILinkedInSearchSettingsService _linkedInSearchSettingsService;
    private readonly ILinkedInSessionStore _sessionStore;

    public LinkedInJobSearchService(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionStore sessionStore,
        ILinkedInSearchSettingsService linkedInSearchSettingsService,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        ILogger<LinkedInJobSearchService> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _sessionStore = sessionStore;
        _linkedInSearchSettingsService = linkedInSearchSettingsService;
        _fetchDiagnosticsOptions = fetchDiagnosticsOptions.Value;
        _logger = logger;
    }

    public async Task<LinkedInJobSearchFetchResult> FetchCurrentSearchAsync(CancellationToken cancellationToken)
    {
        var diagnosticsEnabled = _fetchDiagnosticsOptions.Enabled;
        var canLogDiagnostics = diagnosticsEnabled && _logger.IsEnabled(LogLevel.Information);
        var sessionSnapshot = await _sessionStore.GetCurrentAsync(cancellationToken);

        if (sessionSnapshot is null)
        {
            return LinkedInJobSearchFetchResult.Failed(
                "No active LinkedIn session is available. Use the session control to capture a new one before fetching jobs.",
                StatusCodes.Status502BadGateway);
        }

        var searchSettings = await _linkedInSearchSettingsService.GetActiveAsync(cancellationToken);
        var headers = MergeHeaders(sessionSnapshot, searchSettings);
        var aggregatedJobs = new List<LinkedInJobSearchItem>(SearchJobCap);
        var seenJobIds = new HashSet<string>(StringComparer.Ordinal);
        var totalAvailableCount = 0;
        var pagesFetched = 0;

        if (canLogDiagnostics)
        {
            var sanitizedKeywords = SensitiveDataRedaction.SanitizeForMessage(searchSettings.Keywords, 256);
            var jobTypesSummary = searchSettings.JobTypeCodes.Count == 0
                ? "(default)"
                : string.Join(',', searchSettings.JobTypeCodes);
            var workplaceTypesSummary = searchSettings.WorkplaceTypeCodes.Count == 0
                ? "(default)"
                : string.Join(',', searchSettings.WorkplaceTypeCodes);
            Log.LinkedInFetchDiagnosticsStarted(
                _logger,
                sessionSnapshot.Source,
                SearchPageCap,
                SearchJobCap,
                sanitizedKeywords,
                searchSettings.LocationGeoId ?? "(default)",
                searchSettings.EasyApply,
                jobTypesSummary,
                workplaceTypesSummary);
        }

        for (var pageIndex = 0; pageIndex < SearchPageCap && aggregatedJobs.Count < SearchJobCap; pageIndex++)
        {
            var remainingCapacity = SearchJobCap - aggregatedJobs.Count;
            var requestedCount = Math.Min(LinkedInRequestDefaults.DefaultSearchPageSize, remainingCapacity);
            var start = pageIndex * LinkedInRequestDefaults.DefaultSearchPageSize;

            if (totalAvailableCount > 0 && start >= totalAvailableCount)
            {
                if (canLogDiagnostics)
                {
                    Log.LinkedInFetchDiagnosticsStoppedBeforePage(
                        _logger,
                        pageIndex,
                        start,
                        totalAvailableCount,
                        aggregatedJobs.Count);
                }

                break;
            }

            if (canLogDiagnostics)
            {
                Log.LinkedInFetchDiagnosticsRequestingPage(
                    _logger,
                    pageIndex,
                    start,
                    requestedCount,
                    remainingCapacity,
                    aggregatedJobs.Count);
            }

            var response = await _linkedInApiClient.GetAsync(
                LinkedInRequestDefaults.BuildSearchUri(
                    searchSettings.Keywords,
                    searchSettings.LocationGeoId,
                    searchSettings.EasyApply,
                    searchSettings.JobTypeCodes,
                    searchSettings.WorkplaceTypeCodes,
                    start,
                    requestedCount),
                headers,
                cancellationToken);

            if (response.StatusCode != (int)HttpStatusCode.OK)
            {
                if (canLogDiagnostics)
                {
                    Log.LinkedInFetchDiagnosticsReceivedNonSuccessPageResponse(
                        _logger,
                        pageIndex,
                        response.StatusCode,
                        pagesFetched,
                        aggregatedJobs.Count);
                }

                Log.LinkedInSearchReturnedNonSuccessStatusCode(_logger, response.StatusCode);

                if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    await _sessionStore.InvalidateCurrentAsync(cancellationToken);

                    var expiredMessage =
                        "Stored LinkedIn session expired during job fetch. Use the session control to capture a new one.";

                    if (pagesFetched == 0)
                    {
                        return LinkedInJobSearchFetchResult.Failed(expiredMessage, response.StatusCode);
                    }

                    return LinkedInJobSearchFetchResult.Succeeded(
                        response.StatusCode,
                        pagesFetched,
                        aggregatedJobs.Count,
                        totalAvailableCount,
                        aggregatedJobs,
                        $"{expiredMessage} Partial results from earlier pages were kept.");
                }

                if (pagesFetched == 0)
                {
                    return LinkedInJobSearchFetchResult.Failed(
                        $"LinkedIn job search failed with HTTP {response.StatusCode}.",
                        response.StatusCode);
                }

                return LinkedInJobSearchFetchResult.Succeeded(
                    response.StatusCode,
                    pagesFetched,
                    aggregatedJobs.Count,
                    totalAvailableCount,
                    aggregatedJobs,
                    $"LinkedIn job search stopped early after page {pagesFetched} because LinkedIn returned HTTP {response.StatusCode}.");
            }

            LinkedInJobSearchPageResult pageResult;

            try
            {
                pageResult = ParsePage(response.Body);
            }
            catch (JsonException exception)
            {
                if (canLogDiagnostics)
                {
                    Log.LinkedInFetchDiagnosticsFailedToParsePageJson(
                        _logger,
                        pageIndex,
                        pagesFetched,
                        aggregatedJobs.Count,
                        response.Body.Length);
                }

                Log.LinkedInSearchFailedToParseJson(_logger, exception);

                if (pagesFetched == 0)
                {
                    return LinkedInJobSearchFetchResult.Failed(
                        "LinkedIn search response was not valid JSON.",
                        StatusCodes.Status502BadGateway);
                }

                return LinkedInJobSearchFetchResult.Succeeded(
                    response.StatusCode,
                    pagesFetched,
                    aggregatedJobs.Count,
                    totalAvailableCount,
                    aggregatedJobs,
                    "LinkedIn job search stopped early because a later page response could not be parsed.");
            }

            if (pageResult.TotalCount > 0)
            {
                totalAvailableCount = pageResult.TotalCount;
            }

            var aggregatedCountBeforeMerge = aggregatedJobs.Count;

            foreach (var job in pageResult.Jobs)
            {
                if (seenJobIds.Add(job.LinkedInJobId))
                {
                    aggregatedJobs.Add(job);
                }
            }

            pagesFetched++;
            var addedCount = aggregatedJobs.Count - aggregatedCountBeforeMerge;
            var duplicateCount = Math.Max(0, pageResult.Jobs.Count - addedCount);

            if (canLogDiagnostics)
            {
                Log.LinkedInFetchDiagnosticsParsedPage(
                    _logger,
                    pageIndex,
                    pageResult.ReturnedCount,
                    pageResult.Jobs.Count,
                    addedCount,
                    duplicateCount,
                    aggregatedJobs.Count,
                    totalAvailableCount);
            }

            if (pageResult.ReturnedCount == 0 ||
                pageResult.ReturnedCount < requestedCount ||
                aggregatedJobs.Count >= SearchJobCap)
            {
                if (canLogDiagnostics)
                {
                    var stopReason = pageResult.ReturnedCount == 0
                        ? "EmptyPage"
                        : pageResult.ReturnedCount < requestedCount
                            ? "ShortPage"
                            : "ReachedJobCap";

                    Log.LinkedInFetchDiagnosticsStoppedAfterPage(
                        _logger,
                        stopReason,
                        pageIndex,
                        pageResult.ReturnedCount,
                        requestedCount,
                        aggregatedJobs.Count);
                }

                break;
            }

            if (totalAvailableCount > 0 &&
                ((pageIndex + 1) * LinkedInRequestDefaults.DefaultSearchPageSize) >= totalAvailableCount)
            {
                if (canLogDiagnostics)
                {
                    Log.LinkedInFetchDiagnosticsReachedTotalAfterPage(
                        _logger,
                        pageIndex,
                        (pageIndex + 1) * LinkedInRequestDefaults.DefaultSearchPageSize,
                        totalAvailableCount,
                        aggregatedJobs.Count);
                }

                break;
            }

            if (canLogDiagnostics)
            {
                Log.LinkedInFetchDiagnosticsDelayingBeforeNextPage(
                    _logger,
                    (int)SearchPageDelay.TotalMilliseconds,
                    pageIndex + 1);
            }

            await Task.Delay(SearchPageDelay, cancellationToken);
        }

        if (canLogDiagnostics)
        {
            Log.LinkedInFetchDiagnosticsCompleted(
                _logger,
                pagesFetched,
                aggregatedJobs.Count,
                totalAvailableCount);
        }

        return LinkedInJobSearchFetchResult.Succeeded(
            StatusCodes.Status200OK,
            pagesFetched,
            aggregatedJobs.Count,
            totalAvailableCount,
            aggregatedJobs);
    }

    private static LinkedInJobSearchPageResult ParsePage(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var dataNode))
        {
            throw new JsonException("Response JSON did not contain a top-level 'data' node.");
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

        return new LinkedInJobSearchPageResult(returnedCardUrns.Count, totalCount, orderedJobs);
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

    private static Dictionary<string, string> MergeHeaders(
        LinkedInSessionSnapshot sessionSnapshot,
        LinkedInSearchSettings searchSettings)
    {
        var merged = new Dictionary<string, string>(sessionSnapshot.Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
            ["Referer"] = LinkedInRequestDefaults.BuildSearchReferer(
                searchSettings.Keywords,
                searchSettings.LocationGeoId,
                searchSettings.EasyApply,
                searchSettings.JobTypeCodes,
                searchSettings.WorkplaceTypeCodes),
            ["x-li-pem-metadata"] = LinkedInRequestDefaults.SearchPemMetadata
        };

        return merged;
    }
}

internal sealed record LinkedInJobSearchPageResult(
    int ReturnedCount,
    int TotalCount,
    IReadOnlyList<LinkedInJobSearchItem> Jobs);

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

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics started. SessionSource={SessionSource}, SearchPageCap={SearchPageCap}, SearchJobCap={SearchJobCap}, Keywords={Keywords}, LocationGeoId={LocationGeoId}, EasyApply={EasyApply}, JobTypes={JobTypes}, WorkplaceTypes={WorkplaceTypes}")]
    public static partial void LinkedInFetchDiagnosticsStarted(
        ILogger logger,
        string sessionSource,
        int searchPageCap,
        int searchJobCap,
        string keywords,
        string locationGeoId,
        bool easyApply,
        string jobTypes,
        string workplaceTypes);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics stopped before page request. Reason=ReachedTotalCount, PageIndex={PageIndex}, Start={Start}, TotalAvailableCount={TotalAvailableCount}, AggregatedCount={AggregatedCount}")]
    public static partial void LinkedInFetchDiagnosticsStoppedBeforePage(
        ILogger logger,
        int pageIndex,
        int start,
        int totalAvailableCount,
        int aggregatedCount);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics requesting page. PageIndex={PageIndex}, Start={Start}, RequestedCount={RequestedCount}, RemainingCapacity={RemainingCapacity}, AggregatedCount={AggregatedCount}")]
    public static partial void LinkedInFetchDiagnosticsRequestingPage(
        ILogger logger,
        int pageIndex,
        int start,
        int requestedCount,
        int remainingCapacity,
        int aggregatedCount);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics received non-success page response. PageIndex={PageIndex}, StatusCode={StatusCode}, PagesFetched={PagesFetched}, AggregatedCount={AggregatedCount}")]
    public static partial void LinkedInFetchDiagnosticsReceivedNonSuccessPageResponse(
        ILogger logger,
        int pageIndex,
        int statusCode,
        int pagesFetched,
        int aggregatedCount);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics failed to parse page JSON. PageIndex={PageIndex}, PagesFetched={PagesFetched}, AggregatedCount={AggregatedCount}, BodyLength={BodyLength}")]
    public static partial void LinkedInFetchDiagnosticsFailedToParsePageJson(
        ILogger logger,
        int pageIndex,
        int pagesFetched,
        int aggregatedCount,
        int bodyLength);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics parsed page. PageIndex={PageIndex}, ReturnedCardCount={ReturnedCardCount}, ParsedJobCount={ParsedJobCount}, AddedCount={AddedCount}, DuplicateCount={DuplicateCount}, AggregatedCount={AggregatedCount}, TotalAvailableCount={TotalAvailableCount}")]
    public static partial void LinkedInFetchDiagnosticsParsedPage(
        ILogger logger,
        int pageIndex,
        int returnedCardCount,
        int parsedJobCount,
        int addedCount,
        int duplicateCount,
        int aggregatedCount,
        int totalAvailableCount);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics stopped after page. Reason={StopReason}, PageIndex={PageIndex}, ReturnedCardCount={ReturnedCardCount}, RequestedCount={RequestedCount}, AggregatedCount={AggregatedCount}")]
    public static partial void LinkedInFetchDiagnosticsStoppedAfterPage(
        ILogger logger,
        string stopReason,
        int pageIndex,
        int returnedCardCount,
        int requestedCount,
        int aggregatedCount);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics stopped after page. Reason=ReachedTotalCount, PageIndex={PageIndex}, NextStart={NextStart}, TotalAvailableCount={TotalAvailableCount}, AggregatedCount={AggregatedCount}")]
    public static partial void LinkedInFetchDiagnosticsReachedTotalAfterPage(
        ILogger logger,
        int pageIndex,
        int nextStart,
        int totalAvailableCount,
        int aggregatedCount);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics delaying before next page. DelayMilliseconds={DelayMilliseconds}, NextPageIndex={NextPageIndex}")]
    public static partial void LinkedInFetchDiagnosticsDelayingBeforeNextPage(
        ILogger logger,
        int delayMilliseconds,
        int nextPageIndex);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Information,
        Message = "LinkedIn fetch diagnostics completed. PagesFetched={PagesFetched}, AggregatedCount={AggregatedCount}, TotalAvailableCount={TotalAvailableCount}")]
    public static partial void LinkedInFetchDiagnosticsCompleted(
        ILogger logger,
        int pagesFetched,
        int aggregatedCount,
        int totalAvailableCount);
}
