using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Configuration;
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
            NullLogger<LinkedInJobSearchService>.Instance);

        var result = await service.FetchCurrentSearchAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Empty(result.Jobs);
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

    private sealed class FakeLinkedInApiClient : ILinkedInApiClient
    {
        private readonly LinkedInApiResponse _response;

        public FakeLinkedInApiClient(LinkedInApiResponse response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }

        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_response);
        }
    }

    private sealed class FakeLinkedInSearchSettingsService : ILinkedInSearchSettingsService
    {
        public Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new LinkedInSearchSettings(
                    "Default",
                    "C# .Net",
                    "Limassol, Cyprus",
                    "Limassol, Cyprus",
                    "106394980",
                    true,
                    ["1", "2", "3"],
                    ["F", "P"]));
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
}
