using System.Text.Json;
using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class JobsControllerTests
{
    [Fact]
    public async Task FetchAndScoreReturnsJsonPayloadForAjaxRequests()
    {
        var service = new FakeJobsDashboardService();
        var controller = new JobsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()),
            Url = new TestUrlHelper("/Jobs?sortBy=last-seen")
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.FetchAndScore(new JobsDashboardQuery(), "connection-1", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("success", document.RootElement.GetProperty("severity").GetString());
        Assert.Equal("Workflow complete.", document.RootElement.GetProperty("message").GetString());
        Assert.Equal("/Jobs?sortBy=last-seen", document.RootElement.GetProperty("redirectUrl").GetString());
        Assert.Equal("Workflow complete.", controller.TempData["JobsAlertMessage"]);
        Assert.Equal("success", controller.TempData["JobsAlertSeverity"]);
        Assert.Equal(25, controller.TempData["JobsWorkflowImportFetchedCount"]);
        Assert.Equal(100, controller.TempData["JobsWorkflowImportTotalCount"]);
        Assert.Equal(3, controller.TempData["JobsWorkflowEnrichmentEnrichedCount"]);
        Assert.Equal(3, controller.TempData["JobsWorkflowScoringScoredCount"]);
        Assert.Equal("connection-1", service.LastConnectionId);
    }

    private sealed class FakeJobsDashboardService : IJobsDashboardService
    {
        public string? LastConnectionId { get; private set; }

        public Task<JobsDashboardSnapshot> GetSnapshotAsync(JobsDashboardQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobDetailsSnapshot?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<JobsRowsChunk> GetRowsAsync(JobsDashboardQuery query, int offset, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<FetchAndScoreWorkflowResult> RunFetchAndScoreAsync(string? progressConnectionId, CancellationToken cancellationToken)
        {
            LastConnectionId = progressConnectionId;

            var import = JobImportResult.Succeeded(
                pagesFetched: 1,
                fetchedCount: 25,
                totalAvailableCount: 100,
                importedCount: 3,
                updatedExistingCount: 22,
                skippedCount: 22,
                message: "Import ok");

            var enrichment = JobEnrichmentResult.Succeeded(
                requestedCount: 3,
                processedCount: 3,
                enrichedCount: 3,
                failedCount: 0,
                warningCount: 0);

            var scoring = JobBatchScoringResult.Succeeded(
                requestedCount: 3,
                processedCount: 3,
                scoredCount: 3,
                failedCount: 0);

            return Task.FromResult(
                new FetchAndScoreWorkflowResult(
                    true,
                    "Workflow complete.",
                    "success",
                    import,
                    enrichment,
                    scoring));
        }

        public Task<JobStatusChangeResult> UpdateStatusAsync(Guid jobId, JobWorkflowStatus status, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
