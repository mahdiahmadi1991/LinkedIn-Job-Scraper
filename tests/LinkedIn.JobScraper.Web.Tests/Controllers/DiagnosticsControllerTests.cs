using System.Text.Json;
using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class DiagnosticsControllerTests
{
    [Fact]
    public async Task SummaryReturnsSafeReadinessAndSessionShape()
    {
        var controller = new DiagnosticsController(
            new LinkedInFeasibilityProbe(
                new FakeLinkedInApiClient(),
                new FakeLinkedInSessionVerificationService(),
                NullLogger<LinkedInFeasibilityProbe>.Instance),
            new FakeJobImportService(),
            new FakeJobEnrichmentService(),
            new FakeJobBatchScoringService(),
            Options.Create(new SqlServerOptions
            {
                ConnectionString = "Server=.;Database=LinkedInJobScraper;Trusted_Connection=True;"
            }),
            Options.Create(new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini"
            }),
            new FakeLinkedInSessionStore());

        var result = await controller.Summary(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));

        Assert.True(document.RootElement.GetProperty("config").GetProperty("sqlServerConfigured").GetBoolean());
        Assert.True(document.RootElement.GetProperty("config").GetProperty("openAiApiKeyConfigured").GetBoolean());
        Assert.True(document.RootElement.GetProperty("config").GetProperty("openAiModelConfigured").GetBoolean());
        Assert.True(document.RootElement.GetProperty("session").GetProperty("storedSessionAvailable").GetBoolean());
        Assert.Equal("PlaywrightManualLogin", document.RootElement.GetProperty("session").GetProperty("source").GetString());
        Assert.False(document.RootElement.GetProperty("session").TryGetProperty("cookie", out _));
        Assert.False(document.RootElement.GetProperty("session").TryGetProperty("headers", out _));
    }

    private sealed class FakeLinkedInSessionStore : ILinkedInSessionStore
    {
        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<LinkedInSessionSnapshot?>(
                new LinkedInSessionSnapshot(
                    new Dictionary<string, string> { ["csrf-token"] = "secret" },
                    DateTimeOffset.UtcNow,
                    "PlaywrightManualLogin"));
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

    private sealed class FakeLinkedInSessionVerificationService : ILinkedInSessionVerificationService
    {
        public Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LinkedInSessionVerificationResult.Succeeded("ok"));
        }
    }

    private sealed class FakeLinkedInApiClient : ILinkedInApiClient
    {
        public Task<LinkedInApiResponse> GetAsync(
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new LinkedInApiResponse(200, true, "User-agent: *"));
        }
    }

    private sealed class FakeJobImportService : IJobImportService
    {
        public Task<JobImportResult> ImportCurrentSearchAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
