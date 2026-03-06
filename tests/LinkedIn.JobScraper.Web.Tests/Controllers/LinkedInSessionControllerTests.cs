using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Contracts;
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
            new FakeLinkedInSessionCurlImportService(),
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
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.False(payload.Success);
        Assert.True(payload.State.StoredSessionAvailable);
        Assert.Equal("Refresh Session", payload.State.PrimaryActionLabel);
        Assert.Equal("Connected", payload.State.SessionIndicatorLabel);
        Assert.Equal("session-state-connected", payload.State.SessionIndicatorClass);
    }

    [Fact]
    public async Task StateReturnsMissingSessionShapeWhenNoStoredSessionExists()
    {
        var controller = new LinkedInSessionController(
            new MissingSessionLinkedInBrowserLoginService(),
            new FakeLinkedInSessionCurlImportService(),
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
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.False(payload.Success);
        Assert.False(payload.State.StoredSessionAvailable);
        Assert.Equal("Connect Session", payload.State.PrimaryActionLabel);
        Assert.Equal("Missing", payload.State.SessionIndicatorLabel);
        Assert.Equal("session-state-missing", payload.State.SessionIndicatorClass);
    }

    [Fact]
    public async Task StateTreatsAutoCaptureAsStoppedWhenBrowserIsClosed()
    {
        var controller = new LinkedInSessionController(
            new AutoCaptureStaleLinkedInBrowserLoginService(),
            new FakeLinkedInSessionCurlImportService(),
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
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.False(payload.State.AutoCaptureActive);
        Assert.Equal("Missing", payload.State.SessionIndicatorLabel);
        Assert.Equal("session-state-missing", payload.State.SessionIndicatorClass);
        Assert.True(payload.State.ShowManualCaptureAction);
    }

    [Fact]
    public async Task VerifyReturnsProblemDetailsForAjaxFailures()
    {
        var controller = new LinkedInSessionController(
            new FakeLinkedInBrowserLoginService(),
            new FakeLinkedInSessionCurlImportService(),
            new FakeLinkedInSessionStore(),
            new FailingLinkedInSessionVerificationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Verify(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("LinkedIn session action failed", details.Title);
        Assert.Equal("Stored session is no longer valid.", details.Detail);
        Assert.Equal("Stored session is no longer valid.", controller.TempData["LinkedInSessionStatusMessage"]);
        Assert.Equal(bool.FalseString, controller.TempData["LinkedInSessionStatusSucceeded"]);
    }

    [Fact]
    public async Task LaunchReturnsProblemDetailsForAjaxFailures()
    {
        var controller = new LinkedInSessionController(
            new FailingLinkedInBrowserLoginService(),
            new FakeLinkedInSessionCurlImportService(),
            new FakeLinkedInSessionStore(),
            new FakeLinkedInSessionVerificationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Launch(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("LinkedIn session action failed", details.Title);
        Assert.Equal("Browser launch failed.", details.Detail);
    }

    [Fact]
    public async Task VerifyNormalizesNonErrorFailureStatusCodesToConflict()
    {
        var controller = new LinkedInSessionController(
            new FakeLinkedInBrowserLoginService(),
            new FakeLinkedInSessionCurlImportService(),
            new FakeLinkedInSessionStore(),
            new AmbiguousFailingLinkedInSessionVerificationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Verify(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("Payload shape was unexpected.", details.Detail);
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

    private sealed class FailingLinkedInBrowserLoginService : ILinkedInBrowserLoginService
    {
        public Task<LinkedInBrowserLoginActionResult> CaptureAndSaveAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInBrowserLoginState> GetStateAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInBrowserLoginActionResult> LaunchLoginAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new LinkedInBrowserLoginActionResult(false, "Browser launch failed."));
        }
    }

    private sealed class MissingSessionLinkedInBrowserLoginService : ILinkedInBrowserLoginService
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
                    StoredSessionAvailable: false,
                    StoredSessionCapturedAtUtc: null,
                    StoredSessionSource: null,
                    AutoCaptureActive: false,
                    AutoCaptureStatusMessage: null,
                    AutoCaptureCompletedSuccessfully: false));
        }

        public Task<LinkedInBrowserLoginActionResult> LaunchLoginAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class AutoCaptureStaleLinkedInBrowserLoginService : ILinkedInBrowserLoginService
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
                    StoredSessionAvailable: false,
                    StoredSessionCapturedAtUtc: null,
                    StoredSessionSource: null,
                    AutoCaptureActive: true,
                    AutoCaptureStatusMessage: "Waiting for LinkedIn login...",
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
        public Task<LinkedInSessionVerificationResult> VerifyAsync(
            LinkedInSessionSnapshot sessionSnapshot,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LinkedInSessionVerificationResult.Failed(
                "Stored session is no longer valid.",
                    StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class AmbiguousFailingLinkedInSessionVerificationService : ILinkedInSessionVerificationService
    {
        public Task<LinkedInSessionVerificationResult> VerifyAsync(
            LinkedInSessionSnapshot sessionSnapshot,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInSessionVerificationResult> VerifyCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LinkedInSessionVerificationResult.Failed(
                    "Payload shape was unexpected.",
                    StatusCodes.Status200OK));
        }
    }

    private sealed class FakeLinkedInSessionCurlImportService : ILinkedInSessionCurlImportService
    {
        public Task<LinkedInSessionCurlImportResult> ImportAsync(string? curlText, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
