using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSessionCurlImportServiceTests
{
    [Fact]
    public async Task ImportAsyncSavesValidatedSessionFromBashCurl()
    {
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "existing" },
                DateTimeOffset.UtcNow.AddMinutes(-10),
                "Existing"));
        var verificationService = new SuccessfulLinkedInSessionVerificationService();
        var service = new LinkedInSessionCurlImportService(
            sessionStore,
            verificationService,
            NullLogger<LinkedInSessionCurlImportService>.Instance);

        const string curlText =
            """
            curl 'https://www.linkedin.com/voyager/api/graphql' \
              -H 'cookie: li_at=test-cookie; JSESSIONID="ajax:123"' \
              -H 'csrf-token: ajax:123' \
              -H 'user-agent: test-agent' \
              -H 'referer: https://www.linkedin.com/jobs/search/?keywords=C%23%20.Net' \
              -H 'x-li-track: {"clientVersion":"1.13.42597"}' \
              -H 'x-li-page-instance: urn:li:page:d_flagship3_search_srp_jobs;abc123' \
              --compressed
            """;

        var result = await service.ImportAsync(curlText, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(sessionStore.SaveCalled);
        Assert.True(sessionStore.MarkValidatedCalled);
        Assert.NotNull(sessionStore.CurrentSnapshot);
        Assert.Equal("CurlImport", sessionStore.CurrentSnapshot!.Source);
        Assert.Equal("li_at=test-cookie; JSESSIONID=\"ajax:123\"", sessionStore.CurrentSnapshot.Headers["Cookie"]);
        Assert.Equal("ajax:123", sessionStore.CurrentSnapshot.Headers["csrf-token"]);
        Assert.Equal("test-agent", sessionStore.CurrentSnapshot.Headers["User-Agent"]);
        Assert.Equal("https://www.linkedin.com/jobs/search/?keywords=C%23%20.Net", sessionStore.CurrentSnapshot.Headers["Referer"]);
        Assert.Equal("{\"clientVersion\":\"1.13.42597\"}", sessionStore.CurrentSnapshot.Headers["x-li-track"]);
        Assert.Equal("urn:li:page:d_flagship3_search_srp_jobs;abc123", sessionStore.CurrentSnapshot.Headers["x-li-page-instance"]);
        Assert.Equal(1, verificationService.VerifyCallCount);
    }

    [Fact]
    public async Task ImportAsyncDoesNotOverwriteStoredSessionWhenVerificationFails()
    {
        var existingSnapshot = new LinkedInSessionSnapshot(
            new Dictionary<string, string> { ["Cookie"] = "existing" },
            DateTimeOffset.UtcNow.AddMinutes(-10),
            "Existing");
        var sessionStore = new FakeLinkedInSessionStore(existingSnapshot);
        var service = new LinkedInSessionCurlImportService(
            sessionStore,
            new FailingLinkedInSessionVerificationService(),
            NullLogger<LinkedInSessionCurlImportService>.Instance);

        const string curlText =
            "curl 'https://www.linkedin.com/feed/' -H 'cookie: li_at=test-cookie' -H 'csrf-token: ajax:123'";

        var result = await service.ImportAsync(curlText, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(sessionStore.SaveCalled);
        Assert.False(sessionStore.MarkValidatedCalled);
        Assert.Same(existingSnapshot, sessionStore.CurrentSnapshot);
    }

    [Fact]
    public async Task ImportAsyncFailsWhenRequiredHeadersAreMissing()
    {
        var sessionStore = new FakeLinkedInSessionStore(null);
        var verificationService = new SuccessfulLinkedInSessionVerificationService();
        var service = new LinkedInSessionCurlImportService(
            sessionStore,
            verificationService,
            NullLogger<LinkedInSessionCurlImportService>.Instance);

        const string curlText =
            "curl 'https://www.linkedin.com/feed/' -H 'cookie: li_at=test-cookie; JSESSIONID=\"\"'";

        var result = await service.ImportAsync(curlText, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("csrf-token", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(sessionStore.SaveCalled);
        Assert.Equal(0, verificationService.VerifyCallCount);
    }

    [Fact]
    public async Task ImportAsyncDerivesCsrfTokenFromJSessionCookieWhenHeaderIsMissing()
    {
        var sessionStore = new FakeLinkedInSessionStore(null);
        var verificationService = new SuccessfulLinkedInSessionVerificationService();
        var service = new LinkedInSessionCurlImportService(
            sessionStore,
            verificationService,
            NullLogger<LinkedInSessionCurlImportService>.Instance);

        const string curlText =
            "curl 'https://www.linkedin.com/voyager/api/graphql' -H 'cookie: li_at=test-cookie; JSESSIONID=\"ajax:789\"'";

        var result = await service.ImportAsync(curlText, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(sessionStore.CurrentSnapshot);
        Assert.Equal("ajax:789", sessionStore.CurrentSnapshot!.Headers["csrf-token"]);
    }

    [Fact]
    public async Task ImportAsyncReturnsActionableMessageForFetchInput()
    {
        var sessionStore = new FakeLinkedInSessionStore(null);
        var verificationService = new SuccessfulLinkedInSessionVerificationService();
        var service = new LinkedInSessionCurlImportService(
            sessionStore,
            verificationService,
            NullLogger<LinkedInSessionCurlImportService>.Instance);

        const string fetchText =
            "fetch(\"https://www.linkedin.com/voyager/api/graphql\", { method: \"GET\" })";

        var result = await service.ImportAsync(fetchText, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Copy as cURL", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(sessionStore.SaveCalled);
    }

    private sealed class FakeLinkedInSessionStore : ILinkedInSessionStore
    {
        public FakeLinkedInSessionStore(LinkedInSessionSnapshot? currentSnapshot)
        {
            CurrentSnapshot = currentSnapshot;
        }

        public LinkedInSessionSnapshot? CurrentSnapshot { get; private set; }

        public bool SaveCalled { get; private set; }

        public bool MarkValidatedCalled { get; private set; }

        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CurrentSnapshot);
        }

        public Task InvalidateCurrentAsync(CancellationToken cancellationToken)
        {
            CurrentSnapshot = null;
            return Task.CompletedTask;
        }

        public Task MarkCurrentValidatedAsync(DateTimeOffset validatedAtUtc, CancellationToken cancellationToken)
        {
            MarkValidatedCalled = true;
            return Task.CompletedTask;
        }

        public Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
        {
            SaveCalled = true;
            CurrentSnapshot = sessionSnapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class SuccessfulLinkedInSessionVerificationService : ILinkedInSessionVerificationService
    {
        public int VerifyCallCount { get; private set; }

        public Task<LinkedInSessionVerificationResult> VerifyAsync(
            LinkedInSessionSnapshot sessionSnapshot,
            CancellationToken cancellationToken)
        {
            VerifyCallCount++;
            return Task.FromResult(LinkedInSessionVerificationResult.Succeeded("ok", matchedLocationName: "Cyprus"));
        }

        public Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingLinkedInSessionVerificationService : ILinkedInSessionVerificationService
    {
        public Task<LinkedInSessionVerificationResult> VerifyAsync(
            LinkedInSessionSnapshot sessionSnapshot,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LinkedInSessionVerificationResult.Failed(
                    "Stored session verification failed with HTTP 401.",
                    StatusCodes.Status401Unauthorized));
        }

        public Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
