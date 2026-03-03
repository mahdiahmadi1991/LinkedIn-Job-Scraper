using System.Text.Json;
using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public async Task LinkedInFeasibilityReturnsProblemDetailsWhenProbeFails()
    {
        var controller = CreateController(
            linkedInSessionVerificationService: new FailingLinkedInSessionVerificationService());

        var result = await controller.LinkedInFeasibility(true, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("LinkedIn diagnostics probe failed", details.Title);
        Assert.Equal("Stored session is unavailable.", details.Detail);
    }

    [Fact]
    public async Task ImportCurrentSearchReturnsProblemDetailsWhenImportFails()
    {
        var controller = CreateController(jobImportService: new FailingJobImportService());

        var result = await controller.ImportCurrentSearch(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("LinkedIn import diagnostics failed", details.Title);
        Assert.Equal("Import failed.", details.Detail);
    }

    [Fact]
    public async Task EnrichIncompleteJobsReturnsProblemDetailsWhenEnrichmentFails()
    {
        var controller = CreateController(jobEnrichmentService: new FailingJobEnrichmentService());

        var result = await controller.EnrichIncompleteJobs(5, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.Equal("LinkedIn enrichment diagnostics failed", details.Title);
        Assert.Equal("Enrichment failed.", details.Detail);
    }

    [Fact]
    public async Task ScoreReadyJobsReturnsProblemDetailsWhenScoringFails()
    {
        var controller = CreateController(jobBatchScoringService: new FailingJobBatchScoringService());

        var result = await controller.ScoreReadyJobs(3, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("AI scoring diagnostics failed", details.Title);
        Assert.Equal("Scoring failed.", details.Detail);
    }

    private static DiagnosticsController CreateController(
        ILinkedInSessionVerificationService? linkedInSessionVerificationService = null,
        IJobImportService? jobImportService = null,
        IJobEnrichmentService? jobEnrichmentService = null,
        IJobBatchScoringService? jobBatchScoringService = null)
    {
        return new DiagnosticsController(
            new LinkedInFeasibilityProbe(
                new FakeLinkedInApiClient(),
                linkedInSessionVerificationService ?? new FakeLinkedInSessionVerificationService(),
                NullLogger<LinkedInFeasibilityProbe>.Instance),
            jobImportService ?? new FakeJobImportService(),
            jobEnrichmentService ?? new FakeJobEnrichmentService(),
            jobBatchScoringService ?? new FakeJobBatchScoringService(),
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

    private sealed class FailingLinkedInSessionVerificationService : ILinkedInSessionVerificationService
    {
        public Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LinkedInSessionVerificationResult.Failed(
                    "Stored session is unavailable.",
                    StatusCodes.Status503ServiceUnavailable));
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

    private sealed class FailingJobImportService : IJobImportService
    {
        public Task<JobImportResult> ImportCurrentSearchAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(JobImportResult.Failed("Import failed.", StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class FakeJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobEnrichmentResult.Failed("Enrichment failed.", StatusCodes.Status502BadGateway));
        }
    }

    private sealed class FakeJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobBatchScoringResult.Failed("Scoring failed.", StatusCodes.Status503ServiceUnavailable));
        }
    }
}
