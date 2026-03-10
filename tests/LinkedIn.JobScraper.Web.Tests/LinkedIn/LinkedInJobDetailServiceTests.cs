using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.LinkedIn.Details;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInJobDetailServiceTests
{
    [Fact]
    public async Task FetchAsyncReturnsSessionGuidanceWhenNoStoredSessionExists()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(null);
        var service = new LinkedInJobDetailService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInJobDetailService>.Instance);

        var result = await service.FetchAsync("4379963196", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("No active LinkedIn session is available", result.Message, StringComparison.Ordinal);
        Assert.Contains("import cURL", result.Message, StringComparison.Ordinal);
        Assert.Null(result.Job);
    }

    [Fact]
    public async Task FetchAsyncReturnsFailureWhenSessionExpires()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(401, false, "{}"));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobDetailService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInJobDetailService>.Instance);

        var result = await service.FetchAsync("4379963196", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.True(sessionStore.InvalidateCalled);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Job);
    }

    [Fact]
    public async Task FetchAsyncReturnsFailureWhenJsonIsInvalid()
    {
        var apiClient = new FakeLinkedInApiClient(
            new LinkedInApiResponse(200, true, "{not-json"));
        var sessionStore = new FakeLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string> { ["Cookie"] = "li_at=test" },
                DateTimeOffset.UtcNow,
                "Test"));
        var service = new LinkedInJobDetailService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInJobDetailService>.Instance);

        var result = await service.FetchAsync("4379963196", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("not valid JSON", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(sessionStore.InvalidateCalled);
        Assert.Null(result.Job);
    }

    [Fact]
    public async Task FetchAsyncReturnsFailureWhenRequestedJobNodeIsMissing()
    {
        const string responseBody =
            """
            {
              "data": {
                "data": {
                  "*jobsDashJobPostingsById:dummy": "urn:li:fsd_jobPosting:4379963196"
                },
                "errors": [
                  {
                    "message": "jobBudget forbidden"
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
        var service = new LinkedInJobDetailService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInJobDetailService>.Instance);

        var result = await service.FetchAsync("4379963196", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("missing the requested job node", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Warnings);
        Assert.Equal("jobBudget forbidden", result.Warnings[0]);
        Assert.Null(result.Job);
    }

    [Fact]
    public async Task FetchAsyncParsesSuccessfulPayloadIncludingUpdatedAt()
    {
        const long listedAt = 1735689600000;
        const long updatedAt = 1735776000000;

        const string responseBody =
            """
            {
              "data": {
                "data": {
                  "*jobsDashJobPostingsById:dummy": "urn:li:fsd_jobPosting:4379963196"
                }
              },
              "included": [
                {
                  "$type": "com.linkedin.voyager.dash.hiring.EmploymentStatus",
                  "entityUrn": "urn:li:employmentStatus:FULL_TIME",
                  "localizedName": "Full-time"
                },
                {
                  "$type": "com.linkedin.voyager.dash.common.Geo",
                  "entityUrn": "urn:li:geo:106394980",
                  "defaultLocalizedName": "Limassol, Cyprus"
                },
                {
                  "$type": "com.linkedin.voyager.dash.jobs.JobPosting",
                  "entityUrn": "urn:li:fsd_jobPosting:4379963196",
                  "title": "Senior .NET Engineer",
                  "companyDetails": {
                    "name": "Acme"
                  },
                  "*location": "urn:li:geo:106394980",
                  "*employmentStatus": "urn:li:employmentStatus:FULL_TIME",
                  "description": {
                    "text": "Build APIs"
                  },
                  "companyApplyUrl": "https://example.com/apply",
                  "listedAt": 1735689600000,
                  "lastUpdatedAt": 1735776000000
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
        var service = new LinkedInJobDetailService(
            apiClient,
            sessionStore,
            new FakeLinkedInSearchSettingsService(),
            Options.Create(new LinkedInRequestOptions()),
            NullLogger<LinkedInJobDetailService>.Instance);

        var result = await service.FetchAsync("4379963196", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Empty(result.Warnings);
        Assert.NotNull(result.Job);
        Assert.Equal("4379963196", result.Job!.LinkedInJobId);
        Assert.Equal("Senior .NET Engineer", result.Job.Title);
        Assert.Equal("Acme", result.Job.CompanyName);
        Assert.Equal("Limassol, Cyprus", result.Job.LocationName);
        Assert.Equal("Full-time", result.Job.EmploymentStatus);
        Assert.Equal("Build APIs", result.Job.Description);
        Assert.Equal("https://example.com/apply", result.Job.CompanyApplyUrl);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(listedAt), result.Job.ListedAtUtc);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(updatedAt), result.Job.LinkedInUpdatedAtUtc);
    }

    private sealed class FakeLinkedInApiClient : ILinkedInApiClient
    {
        private readonly LinkedInApiResponse _response;

        public FakeLinkedInApiClient(LinkedInApiResponse response)
        {
            _response = response;
        }

        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private sealed class FakeLinkedInSearchSettingsService : ILinkedInSearchSettingsService
    {
        public Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new LinkedInSearchSettings(
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
