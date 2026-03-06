using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSessionVerificationServiceTests
{
    [Fact]
    public async Task VerifyCurrentAsyncReturnsFailureWhenNoStoredSessionExists()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(null);
        var service = new LinkedInSessionVerificationService(
            apiClient,
            sessionStore,
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains("No stored LinkedIn session is available", result.Message, StringComparison.Ordinal);
        Assert.Contains("Connect Session", result.Message, StringComparison.Ordinal);
        Assert.False(sessionStore.InvalidateCalled);
        Assert.False(sessionStore.MarkValidatedCalled);
        Assert.Equal(0, apiClient.CallCount);
    }

    [Fact]
    public async Task VerifyCurrentAsyncInvalidatesSessionOnUnauthorized()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(401, false, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInSessionVerificationService(
            apiClient,
            sessionStore,
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.True(sessionStore.InvalidateCalled);
        Assert.False(sessionStore.MarkValidatedCalled);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.CallCount);
        Assert.Equal("/voyager/api/voyagerJobsDashJobCards", apiClient.LastRequestUri?.AbsolutePath);
        Assert.DoesNotContain("queryId=", apiClient.LastRequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyCurrentAsyncMarksSessionValidatedWhenPayloadIsUsable()
    {
        const string responseBody =
            """
            {
              "data": {
                "paging": {
                  "count": 1
                }
              },
              "included": [
                {
                  "$type": "com.linkedin.voyager.dash.jobs.JobPosting"
                }
              ]
            }
            """;

        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, responseBody));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInSessionVerificationService(
            apiClient,
            sessionStore,
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.MatchedLocationName);
        Assert.Contains("jobs search responded normally", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(sessionStore.MarkValidatedCalled);
        Assert.False(sessionStore.InvalidateCalled);
        Assert.Equal("/voyager/api/voyagerJobsDashJobCards", apiClient.LastRequestUri?.AbsolutePath);
        Assert.DoesNotContain("queryId=", apiClient.LastRequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyCurrentAsyncReturnsFailureWhenJsonIsInvalid()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, "{not-json"));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInSessionVerificationService(
            apiClient,
            sessionStore,
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains("invalid JSON", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(sessionStore.MarkValidatedCalled);
        Assert.False(sessionStore.InvalidateCalled);
    }

    [Fact]
    public async Task VerifyCurrentAsyncReturnsConflictStyleFailureWhenPayloadShapeIsUnexpected()
    {
        const string responseBody =
            """
            {
              "data": {
                "errors": [
                  {
                    "message": "Unexpected request shape"
                  }
                ]
              },
              "included": []
            }
            """;

        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, responseBody));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInSessionVerificationService(
            apiClient,
            sessionStore,
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains("unexpected payload", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(sessionStore.MarkValidatedCalled);
        Assert.False(sessionStore.InvalidateCalled);
    }

    private sealed class FakeLinkedInApiClient : ILinkedInApiClient
    {
        private readonly LinkedInApiResponse _response;

        public FakeLinkedInApiClient(LinkedInApiResponse response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = requestUri;
            return Task.FromResult(_response);
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

        public bool MarkValidatedCalled { get; private set; }

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
            MarkValidatedCalled = true;
            return Task.CompletedTask;
        }

        public Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
