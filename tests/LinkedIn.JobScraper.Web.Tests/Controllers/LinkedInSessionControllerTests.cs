using System.Text.Json;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class LinkedInSessionControllerTests
{
    [Fact]
    public async Task StateReturnsCurrentSessionShape()
    {
        var browserLoginService = new FakeLinkedInBrowserLoginService();
        var controller = new LinkedInSessionController(
            browserLoginService,
            new FakeLinkedInSessionStore(),
            new FakeLinkedInSessionVerificationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var result = await controller.State(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));

        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("state").GetProperty("storedSessionAvailable").GetBoolean());
        Assert.Equal("Refresh Session", document.RootElement.GetProperty("state").GetProperty("primaryActionLabel").GetString());
        Assert.Equal("Connected", document.RootElement.GetProperty("state").GetProperty("sessionIndicatorLabel").GetString());
        Assert.Equal("session-state-connected", document.RootElement.GetProperty("state").GetProperty("sessionIndicatorClass").GetString());
    }

    private sealed class FakeLinkedInBrowserLoginService : ILinkedInBrowserLoginService
    {
        public Task<LinkedInBrowserLoginActionResult> CaptureAndSaveAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInBrowserLoginState> GetStateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new LinkedInBrowserLoginState(
                    BrowserOpen: false,
                    CurrentPageUrl: null,
                    StoredSessionAvailable: true,
                    StoredSessionCapturedAtUtc: DateTimeOffset.UtcNow,
                    StoredSessionSource: "Test",
                    AutoCaptureActive: false,
                    AutoCaptureStatusMessage: null,
                    AutoCaptureCompletedSuccessfully: false));
        }

        public Task<LinkedInBrowserLoginActionResult> LaunchLoginAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeLinkedInSessionStore : ILinkedInSessionStore
    {
        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }
    }
}
