using System.Net;
using System.Net.Http;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInApiClientTests
{
    [Fact]
    public async Task GetAsyncInvalidatesSessionAndMarksResetRequiredOnUnauthorized()
    {
        var store = new FakeLinkedInSessionStore();
        var tracker = new FakeLinkedInSessionResetRequirementTracker();
        var client = CreateClient(HttpStatusCode.Unauthorized, store, tracker);

        var response = await client.GetAsync(
            new Uri("https://www.linkedin.com/voyager/api/graphql"),
            new Dictionary<string, string> { ["Cookie"] = "li_at=test; JSESSIONID=\"ajax:1\"" },
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.True(store.InvalidateCalled);

        var resetState = tracker.GetCurrent();
        Assert.True(resetState.Required);
        Assert.Equal(LinkedInSessionResetReasonCodes.SessionUnauthorized, resetState.ReasonCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, resetState.StatusCode);
    }

    [Fact]
    public async Task GetAsyncMarksResetRequiredOnForbiddenWithoutInvalidation()
    {
        var store = new FakeLinkedInSessionStore();
        var tracker = new FakeLinkedInSessionResetRequirementTracker();
        var client = CreateClient(HttpStatusCode.Forbidden, store, tracker);

        var response = await client.GetAsync(
            new Uri("https://www.linkedin.com/voyager/api/graphql"),
            new Dictionary<string, string> { ["Cookie"] = "li_at=test; JSESSIONID=\"ajax:1\"" },
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.False(store.InvalidateCalled);

        var resetState = tracker.GetCurrent();
        Assert.True(resetState.Required);
        Assert.Equal(LinkedInSessionResetReasonCodes.SessionForbidden, resetState.ReasonCode);
        Assert.Equal(StatusCodes.Status403Forbidden, resetState.StatusCode);
    }

    private static LinkedInApiClient CreateClient(
        HttpStatusCode statusCode,
        ILinkedInSessionStore sessionStore,
        ILinkedInSessionResetRequirementTracker resetRequirementTracker)
    {
        var httpClient = new HttpClient(new StaticResponseMessageHandler(statusCode))
        {
            BaseAddress = new Uri("https://www.linkedin.com")
        };

        return new LinkedInApiClient(
            httpClient,
            Options.Create(new LinkedInFetchDiagnosticsOptions()),
            Options.Create(new LinkedInRequestSafetyOptions { Enabled = false }),
            resetRequirementTracker,
            sessionStore,
            NullLogger<LinkedInApiClient>.Instance);
    }

    private sealed class StaticResponseMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StaticResponseMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent("{\"ok\":true}")
                });
        }
    }

    private sealed class FakeLinkedInSessionStore : ILinkedInSessionStore
    {
        public bool InvalidateCalled { get; private set; }

        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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

    private sealed class FakeLinkedInSessionResetRequirementTracker : ILinkedInSessionResetRequirementTracker
    {
        private LinkedInSessionResetRequirementState _current = LinkedInSessionResetRequirementState.NotRequired;

        public LinkedInSessionResetRequirementState GetCurrent()
        {
            return _current;
        }

        public void MarkRequired(string reasonCode, string message, int? statusCode)
        {
            _current = new LinkedInSessionResetRequirementState(
                true,
                reasonCode,
                message,
                statusCode,
                DateTimeOffset.UtcNow);
        }

        public void Clear()
        {
            _current = LinkedInSessionResetRequirementState.NotRequired;
        }
    }
}
