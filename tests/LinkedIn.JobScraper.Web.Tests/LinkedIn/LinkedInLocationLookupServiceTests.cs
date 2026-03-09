using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInLocationLookupServiceTests
{
    [Fact]
    public async Task SearchAsyncReturnsSessionGuidanceWhenNoStoredSessionExists()
    {
        var apiClient = new FakeLinkedInApiClient(new LinkedInApiResponse(200, true, "{}"));
        var service = new LinkedInLocationLookupService(
            apiClient,
            new FakeLinkedInSessionStore(null),
            CreateRequestOptions(),
            NullLogger<LinkedInLocationLookupService>.Instance);

        var result = await service.SearchAsync("Limassol", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("stored LinkedIn session is required", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Connect Session", result.Message, StringComparison.Ordinal);
        Assert.Null(apiClient.LastRequestUri);
    }

    [Fact]
    public async Task SearchAsyncUsesGeoTypeaheadRequestShapeAndReturnsSuggestions()
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
                          "text": "Limassol, Cyprus"
                        },
                        "target": {
                          "*geo": "urn:li:geo:106394980"
                        }
                      }
                    ]
                  }
                }
              }
            }
            """;

        var apiClient = new FakeLinkedInApiClient(new LinkedInApiResponse(200, true, responseBody));
        var service = new LinkedInLocationLookupService(
            apiClient,
            new FakeLinkedInSessionStore(CreateSession()),
            CreateRequestOptions(),
            NullLogger<LinkedInLocationLookupService>.Instance);

        var result = await service.SearchAsync("Limassol", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Single(result.Suggestions);
        Assert.Equal("106394980", result.Suggestions[0].GeoId);
        Assert.Equal("Limassol, Cyprus", result.Suggestions[0].DisplayName);
        Assert.Equal("/voyager/api/graphql", apiClient.LastRequestUri?.AbsolutePath);
        Assert.Contains("includeWebMetadata=true", apiClient.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("queryId=voyagerSearchDashReusableTypeahead.4c7caa85341b17b470153ad3d1a29caf", apiClient.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains(
            "typeaheadFilterQuery:(geoSearchTypes:List(POSTCODE_1,POSTCODE_2,POPULATED_PLACE,ADMIN_DIVISION_1,ADMIN_DIVISION_2,COUNTRY_REGION,MARKET_AREA,COUNTRY_CLUSTER))",
            Uri.UnescapeDataString(apiClient.LastRequestUri?.Query ?? string.Empty),
            StringComparison.Ordinal);
        Assert.Equal("Voyager - Search Single Typeahead=jobs-geo", apiClient.LastHeaders?["x-li-pem-metadata"]);
        Assert.Equal("https://www.linkedin.com/jobs/search/?keywords=C%23%20.Net", apiClient.LastHeaders?["Referer"]);
    }

    [Fact]
    public async Task SearchAsyncReturnsFailureWhenGraphQlErrorsArePresent()
    {
        const string responseBody =
            """
            {
              "data": {
                "errors": [
                  {
                    "message": "Persisted query mismatch"
                  }
                ]
              }
            }
            """;

        var apiClient = new FakeLinkedInApiClient(new LinkedInApiResponse(200, true, responseBody));
        var service = new LinkedInLocationLookupService(
            apiClient,
            new FakeLinkedInSessionStore(CreateSession()),
            CreateRequestOptions(),
            NullLogger<LinkedInLocationLookupService>.Instance);

        var result = await service.SearchAsync("Limassol", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Empty(result.Suggestions);
        Assert.Contains("error payload", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsyncReturnsFailureWhenPayloadShapeIsUnexpected()
    {
        const string responseBody =
            """
            {
              "data": {
                "data": {
                  "searchDashReusableTypeaheadByType": {
                    "items": []
                  }
                }
              }
            }
            """;

        var apiClient = new FakeLinkedInApiClient(new LinkedInApiResponse(200, true, responseBody));
        var service = new LinkedInLocationLookupService(
            apiClient,
            new FakeLinkedInSessionStore(CreateSession()),
            CreateRequestOptions(),
            NullLogger<LinkedInLocationLookupService>.Instance);

        var result = await service.SearchAsync("Limassol", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Empty(result.Suggestions);
        Assert.Contains("unexpected payload", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LinkedInSessionSnapshot CreateSession()
    {
        return new LinkedInSessionSnapshot(
            new Dictionary<string, string>
            {
                ["Cookie"] = "li_at=test",
                ["Referer"] = "https://www.linkedin.com/jobs/search/?keywords=C%23%20.Net"
            },
            DateTimeOffset.UtcNow,
            "Test");
    }

    private static IOptions<LinkedInRequestOptions> CreateRequestOptions()
    {
        return Options.Create(
            new LinkedInRequestOptions
            {
                GraphQlQueryId = "voyagerJobsDashJobPostings.891aed7916d7453a37e4bbf5f1f60de4",
                GeoTypeaheadQueryId = "voyagerSearchDashReusableTypeahead.4c7caa85341b17b470153ad3d1a29caf"
            });
    }

    private sealed class FakeLinkedInApiClient : ILinkedInApiClient
    {
        private readonly LinkedInApiResponse _response;

        public FakeLinkedInApiClient(LinkedInApiResponse response)
        {
            _response = response;
        }

        public Uri? LastRequestUri { get; private set; }

        public Dictionary<string, string>? LastHeaders { get; private set; }

        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            LastRequestUri = requestUri;
            LastHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
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

        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshot);
        }

        public Task InvalidateCurrentAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
