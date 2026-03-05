using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInJobSearchServiceTests
{
    [Fact]
    public async Task FetchCurrentSearchAsyncReturnsFailureWhenNoSessionExists()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(null);
        var service = new LinkedInJobSearchService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(new LinkedInFetchLimitsOptions()),
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Empty(result.Jobs);
        Assert.Equal(0, apiClient.CallCount);
        Assert.False(sessionStore.InvalidateCalled);
    }

    [Fact]
    public async Task FetchCurrentSearchAsyncReturnsFailureWhenKeywordsAreMissing()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobSearchService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(
                new LinkedInSearchSettings(
                    "Default",
                    string.Empty,
                    null,
                    null,
                    null,
                    false,
                    [],
                    [])),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(new LinkedInFetchLimitsOptions()),
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Contains("Search Settings", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, apiClient.CallCount);
        Assert.False(sessionStore.InvalidateCalled);
    }

    [Fact]
    public async Task FetchCurrentSearchAsyncInvalidatesSessionOnUnauthorized()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(401, false, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobSearchService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(new LinkedInFetchLimitsOptions()),
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(0, result.PagesFetched);
        Assert.Equal(0, result.ReturnedCount);
        Assert.True(sessionStore.InvalidateCalled);
        Assert.Equal(1, apiClient.CallCount);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchCurrentSearchAsyncUsesDefaultFetchCapsWhenOverridesAreNotConfigured()
    {
        var apiClient = new FakeLinkedInApiClient(
            CreateSuccessfulSearchPage(pageIndex: 0, returnedCount: 100, totalCount: 1300),
            CreateSuccessfulSearchPage(pageIndex: 1, returnedCount: 25, totalCount: 1300));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobSearchService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(new LinkedInFetchLimitsOptions()),
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(LinkedInRequestDefaults.DefaultSearchJobCap, result.ReturnedCount);
        Assert.Equal(LinkedInRequestDefaults.DefaultSearchJobCap, result.Jobs.Count);
        Assert.Equal(1300, result.TotalCount);
        Assert.Equal(2, apiClient.CallCount);
    }

    [Fact]
    public async Task FetchCurrentSearchAsyncUsesConfiguredFetchCapsWhenProvided()
    {
        const int searchPageCap = 6;
        const int searchJobCap = 150;

        var apiClient = new FakeLinkedInApiClient(
            CreateSuccessfulSearchPage(pageIndex: 0, returnedCount: 100, totalCount: 1300),
            CreateSuccessfulSearchPage(pageIndex: 1, returnedCount: 50, totalCount: 1300));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobSearchService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(
                new LinkedInFetchLimitsOptions
                {
                    SearchPageCap = searchPageCap,
                    SearchJobCap = searchJobCap
                }),
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(searchJobCap, result.ReturnedCount);
        Assert.Equal(searchJobCap, result.Jobs.Count);
        Assert.Equal(1300, result.TotalCount);
        Assert.Equal(2, apiClient.CallCount);
    }

    [Fact]
    public async Task FetchCurrentSearchAsyncStopsEarlyWhenExternalPolicyRequestsStop()
    {
        var apiClient = new FakeLinkedInApiClient(
            CreateSuccessfulSearchPage(pageIndex: 0, returnedCount: 100, totalCount: 1300),
            CreateSuccessfulSearchPage(pageIndex: 1, returnedCount: 100, totalCount: 1300),
            CreateSuccessfulSearchPage(pageIndex: 2, returnedCount: 100, totalCount: 1300));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobSearchService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(
                new LinkedInFetchLimitsOptions
                {
                    SearchPageCap = 10,
                    SearchJobCap = 1000
                }),
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(
            CancellationToken.None,
            new LinkedInJobSearchFetchRequest(
                stopContext => stopContext.PagesFetched >= 2,
                "Stopped by test policy."));

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(200, result.ReturnedCount);
        Assert.Equal(200, result.Jobs.Count);
        Assert.Equal(1300, result.TotalCount);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Contains("Stopped by test policy.", result.Message, StringComparison.Ordinal);
    }

    private sealed class FakeLinkedInApiClient : ILinkedInApiClient
    {
        private readonly LinkedInApiResponse[] _responses;

        public FakeLinkedInApiClient(params LinkedInApiResponse[] responses)
        {
            _responses = responses;
        }

        public int CallCount { get; private set; }

        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var responseIndex = Math.Min(CallCount - 1, _responses.Length - 1);
            return Task.FromResult(_responses[responseIndex]);
        }
    }

    private sealed class FakeLinkedInSearchSettingsService : ILinkedInSearchSettingsService
    {
        private readonly LinkedInSearchSettings _settings;

        public FakeLinkedInSearchSettingsService(LinkedInSearchSettings? settings = null)
        {
            _settings = settings ?? new LinkedInSearchSettings(
                "Default",
                "C# .Net",
                "Limassol, Cyprus",
                "Limassol, Cyprus",
                "106394980",
                true,
                ["1", "2", "3"],
                ["F", "P"]);
        }

        public Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_settings);
        }

        public Task<LinkedInSearchSettings> SaveAsync(LinkedInSearchSettings settings, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeLinkedInSessionStore : ILinkedInSessionStore
    {
        private readonly LinkedInSessionSnapshot? _snapshot;

        public FakeLinkedInSessionStore(LinkedInSessionSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public bool InvalidateCalled { get; private set; }

        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshot);
        }

        public Task InvalidateCurrentAsync(CancellationToken cancellationToken)
        {
            InvalidateCalled = true;
            return Task.CompletedTask;
        }

        public Task MarkCurrentValidatedAsync(DateTimeOffset validatedAtUtc, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private static LinkedInApiResponse CreateSuccessfulSearchPage(int pageIndex, int returnedCount, int totalCount)
    {
        var elements = string.Join(
            ",",
            Enumerable.Range(0, returnedCount)
                .Select(
                    offset =>
                    {
                        var jobNumber = (pageIndex * LinkedInRequestDefaults.DefaultSearchPageSize) + offset + 1;
                        return $"{{\"jobCardUnion\":{{\"*jobPostingCard\":\"urn:li:fsd_jobCard:{jobNumber}\"}}}}";
                    }));

        var included = string.Join(
            ",",
            Enumerable.Range(0, returnedCount)
                .Select(
                    offset =>
                    {
                        var jobNumber = (pageIndex * LinkedInRequestDefaults.DefaultSearchPageSize) + offset + 1;

                        return
                            "{" +
                            "\"$type\":\"com.linkedin.voyager.dash.jobs.JobPostingCard\"," +
                            $"\"entityUrn\":\"urn:li:fsd_jobCard:{jobNumber}\"," +
                            $"\"*jobPosting\":\"urn:li:fsd_jobPosting:{jobNumber}\"," +
                            $"\"title\":{{\"text\":\"Job {jobNumber}\"}}," +
                            $"\"primaryDescription\":{{\"text\":\"Company {jobNumber}\"}}," +
                            $"\"secondaryDescription\":{{\"text\":\"Location {jobNumber}\"}}," +
                            "\"footerItems\":[{\"type\":\"LISTED_DATE\",\"timeAt\":1735689600000}]" +
                            "}";
                    }));

        var body =
            "{" +
            "\"data\":{" +
            $"\"elements\":[{elements}]," +
            $"\"paging\":{{\"total\":{totalCount}}}" +
            "}," +
            $"\"included\":[{included}]" +
            "}";

        return new LinkedInApiResponse(200, true, body);
    }
}
