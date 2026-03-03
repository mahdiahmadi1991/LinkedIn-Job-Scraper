using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
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
        var controller = CreateController();

        var result = await controller.Summary(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<DiagnosticsSummaryResponse>(json.Value);

        Assert.True(payload.Config.SqlServerConfigured);
        Assert.True(payload.Config.OpenAiApiKeyConfigured);
        Assert.True(payload.Config.OpenAiModelConfigured);
        Assert.True(payload.Session.StoredSessionAvailable);
        Assert.Equal("PlaywrightManualLogin", payload.Session.Source);
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
    public async Task LinkedInFeasibilityReturnsTypedSuccessPayload()
    {
        var controller = CreateController();

        var result = await controller.LinkedInFeasibility(false, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<LinkedInFeasibilityResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal(StatusCodes.Status200OK, payload.StatusCode);
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
    public async Task ImportCurrentSearchReturnsTypedSuccessPayload()
    {
        var controller = CreateController(jobImportService: new SuccessfulJobImportService());

        var result = await controller.ImportCurrentSearch(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<DiagnosticsImportResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal(1, payload.PagesFetched);
        Assert.Equal(25, payload.FetchedCount);
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
    public async Task EnrichIncompleteJobsReturnsTypedSuccessPayload()
    {
        var controller = CreateController(jobEnrichmentService: new SuccessfulJobEnrichmentService());

        var result = await controller.EnrichIncompleteJobs(5, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<DiagnosticsEnrichmentResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal(5, payload.RequestedCount);
        Assert.Equal(4, payload.EnrichedCount);
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

    [Fact]
    public async Task ScoreReadyJobsReturnsTypedSuccessPayload()
    {
        var controller = CreateController(jobBatchScoringService: new SuccessfulJobBatchScoringService());

        var result = await controller.ScoreReadyJobs(3, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<DiagnosticsScoringResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal(3, payload.RequestedCount);
        Assert.Equal(3, payload.ScoredCount);
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
        public Task<JobImportResult> ImportCurrentSearchAsync(
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SuccessfulJobImportService : IJobImportService
    {
        public Task<JobImportResult> ImportCurrentSearchAsync(
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(
                JobImportResult.Succeeded(
                    pagesFetched: 1,
                    fetchedCount: 25,
                    totalAvailableCount: 100,
                    importedCount: 5,
                    updatedExistingCount: 20,
                    skippedCount: 0));
        }
    }

    private sealed class FailingJobImportService : IJobImportService
    {
        public Task<JobImportResult> ImportCurrentSearchAsync(
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(JobImportResult.Failed("Import failed.", StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class FakeJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
            int count,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null,
            IReadOnlySet<Guid>? excludedJobIds = null)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SuccessfulJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
            int count,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null,
            IReadOnlySet<Guid>? excludedJobIds = null)
        {
            return Task.FromResult(
                JobEnrichmentResult.Succeeded(
                    requestedCount: count,
                    processedCount: count,
                    enrichedCount: count - 1,
                    failedCount: 0,
                    warningCount: 1));
        }
    }

    private sealed class FailingJobEnrichmentService : IJobEnrichmentService
    {
        public Task<JobEnrichmentResult> EnrichIncompleteJobsAsync(
            int count,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null,
            IReadOnlySet<Guid>? excludedJobIds = null)
        {
            return Task.FromResult(JobEnrichmentResult.Failed("Enrichment failed.", StatusCodes.Status502BadGateway));
        }
    }

    private sealed class FakeJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(
            int count,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            throw new NotSupportedException();
        }

        public Task<SingleJobScoringResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SuccessfulJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(
            int count,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(
                JobBatchScoringResult.Succeeded(
                    requestedCount: count,
                    processedCount: count,
                    scoredCount: count,
                    failedCount: 0));
        }

        public Task<SingleJobScoringResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingJobBatchScoringService : IJobBatchScoringService
    {
        public Task<JobBatchScoringResult> ScoreReadyJobsAsync(
            int count,
            CancellationToken cancellationToken,
            JobStageProgressCallback? progressCallback = null)
        {
            return Task.FromResult(JobBatchScoringResult.Failed("Scoring failed.", StatusCodes.Status503ServiceUnavailable));
        }

        public Task<SingleJobScoringResult> ScoreJobAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
