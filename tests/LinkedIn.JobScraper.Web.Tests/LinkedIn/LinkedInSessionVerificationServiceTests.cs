using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSessionVerificationServiceTests
{
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
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.True(sessionStore.InvalidateCalled);
        Assert.False(sessionStore.MarkValidatedCalled);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.CallCount);
    }

    [Fact]
    public async Task VerifyCurrentAsyncMarksSessionValidatedWhenPayloadIsUsable()
    {
        const string responseBody =
            """
            {
              "data": {
                "data": {
                  "searchDashReusableTypeaheadByType": {
                    "elements": [
                      {
                        "title": {
                          "text": "Cyprus"
                        }
                      }
                    ]
                  }
                }
              }
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
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Cyprus", result.MatchedLocationName);
        Assert.True(sessionStore.MarkValidatedCalled);
        Assert.False(sessionStore.InvalidateCalled);
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
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInSessionVerificationService>.Instance);

        var result = await service.VerifyCurrentAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("invalid JSON", result.Message, StringComparison.OrdinalIgnoreCase);
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

        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            CallCount++;
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
