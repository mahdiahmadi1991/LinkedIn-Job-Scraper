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
        var controller = CreateController(
            linkedInSessionStore: new SnapshotLinkedInSessionStore(
                new LinkedInSessionSnapshot(
                    new Dictionary<string, string>(),
                    new DateTimeOffset(2026, 3, 10, 10, 15, 0, TimeSpan.Zero),
                    "CurlImport")));

        var result = await controller.State(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.False(payload.Success);
        Assert.True(payload.State.StoredSessionAvailable);
        Assert.Equal("Connected", payload.State.SessionIndicatorLabel);
        Assert.Equal("session-state-connected", payload.State.SessionIndicatorClass);
        Assert.False(payload.State.ResetRequirement.Required);
    }

    [Fact]
    public async Task StateReturnsMissingSessionShapeWhenNoStoredSessionExists()
    {
        var controller = CreateController(linkedInSessionStore: new SnapshotLinkedInSessionStore(null));

        var result = await controller.State(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.False(payload.Success);
        Assert.False(payload.State.StoredSessionAvailable);
        Assert.Equal("Missing", payload.State.SessionIndicatorLabel);
        Assert.Equal("session-state-missing", payload.State.SessionIndicatorClass);
    }

    [Fact]
    public async Task StateReturnsStoredSessionExpiryMetadataWhenAvailable()
    {
        var controller = CreateController(
            linkedInSessionStore: new SnapshotLinkedInSessionStore(
                new LinkedInSessionSnapshot(
                    new Dictionary<string, string>(),
                    new DateTimeOffset(2026, 3, 10, 10, 15, 0, TimeSpan.Zero),
                    "CurlImport",
                    new DateTimeOffset(2026, 4, 10, 8, 30, 0, TimeSpan.Zero),
                    "li_at cookie")));

        var result = await controller.State(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.Equal(new DateTimeOffset(2026, 4, 10, 8, 30, 0, TimeSpan.Zero), payload.State.StoredSessionEstimatedExpiresAtUtc);
        Assert.Equal("li_at cookie", payload.State.StoredSessionExpirySource);
    }

    [Fact]
    public async Task VerifyReturnsProblemDetailsForAjaxFailures()
    {
        var controller = CreateController(linkedInSessionVerificationService: new FailingLinkedInSessionVerificationService());
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
    public async Task VerifyNormalizesNonErrorFailureStatusCodesToConflict()
    {
        var controller = CreateController(
            linkedInSessionVerificationService: new AmbiguousFailingLinkedInSessionVerificationService());
        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Verify(CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("Payload shape was unexpected.", details.Detail);
    }

    [Fact]
    public async Task VerifySetsResetRequiredStateWhenLinkedInReturnsForbidden()
    {
        var resetTracker = new FakeLinkedInSessionResetRequirementTracker();
        var controller = CreateController(
            linkedInSessionVerificationService: new ForbiddenLinkedInSessionVerificationService(),
            linkedInSessionResetRequirementTracker: resetTracker);

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var verifyResult = await controller.Verify(CancellationToken.None);
        var problem = Assert.IsType<ObjectResult>(verifyResult);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);

        var stateResult = await controller.State(CancellationToken.None);
        var json = Assert.IsType<JsonResult>(stateResult);
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.True(payload.State.ResetRequirement.Required);
        Assert.Equal("session_forbidden", payload.State.ResetRequirement.ReasonCode);
        Assert.Equal(StatusCodes.Status403Forbidden, payload.State.ResetRequirement.StatusCode);
        Assert.Equal("Reset Required", payload.State.SessionIndicatorLabel);
    }

    [Fact]
    public async Task RevokeClearsSessionAndResetRequirement()
    {
        var store = new MutableLinkedInSessionStore(
            new LinkedInSessionSnapshot(
                new Dictionary<string, string>(),
                new DateTimeOffset(2026, 3, 10, 10, 15, 0, TimeSpan.Zero),
                "CurlImport"));
        var resetTracker = new FakeLinkedInSessionResetRequirementTracker();
        resetTracker.MarkRequired("session_forbidden", "Reset required", StatusCodes.Status403Forbidden);

        var controller = CreateController(
            linkedInSessionStore: store,
            linkedInSessionResetRequirementTracker: resetTracker);
        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Revoke(CancellationToken.None);
        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<LinkedInSessionActionResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.False(payload.State.StoredSessionAvailable);
        Assert.False(payload.State.ResetRequirement.Required);
        Assert.Equal("Missing", payload.State.SessionIndicatorLabel);
    }

    private static LinkedInSessionController CreateController(
        ILinkedInSessionCurlImportService? linkedInSessionCurlImportService = null,
        ILinkedInSessionResetRequirementTracker? linkedInSessionResetRequirementTracker = null,
        ILinkedInSessionStore? linkedInSessionStore = null,
        ILinkedInSessionVerificationService? linkedInSessionVerificationService = null)
    {
        return new LinkedInSessionController(
            linkedInSessionCurlImportService ?? new FakeLinkedInSessionCurlImportService(),
            linkedInSessionResetRequirementTracker ?? new FakeLinkedInSessionResetRequirementTracker(),
            linkedInSessionStore ?? new SnapshotLinkedInSessionStore(null),
            linkedInSessionVerificationService ?? new FakeLinkedInSessionVerificationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
    }

    private sealed class FakeLinkedInSessionCurlImportService : ILinkedInSessionCurlImportService
    {
        public Task<LinkedInSessionCurlImportResult> ImportAsync(string? curlText, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SnapshotLinkedInSessionStore : ILinkedInSessionStore
    {
        private readonly LinkedInSessionSnapshot? _snapshot;

        public SnapshotLinkedInSessionStore(LinkedInSessionSnapshot? snapshot)
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

    private sealed class MutableLinkedInSessionStore : ILinkedInSessionStore
    {
        private LinkedInSessionSnapshot? _snapshot;

        public MutableLinkedInSessionStore(LinkedInSessionSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshot);
        }

        public Task InvalidateCurrentAsync(CancellationToken cancellationToken)
        {
            _snapshot = null;
            return Task.CompletedTask;
        }

        public Task MarkCurrentValidatedAsync(DateTimeOffset validatedAtUtc, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
        {
            _snapshot = sessionSnapshot;
            return Task.CompletedTask;
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

    private sealed class ForbiddenLinkedInSessionVerificationService : ILinkedInSessionVerificationService
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
                    "Stored session is forbidden.",
                    StatusCodes.Status403Forbidden));
        }
    }

    private sealed class FakeLinkedInSessionResetRequirementTracker : ILinkedInSessionResetRequirementTracker
    {
        public LinkedInSessionResetRequirementState Current { get; private set; } =
            LinkedInSessionResetRequirementState.NotRequired;

        public LinkedInSessionResetRequirementState GetCurrent()
        {
            return Current;
        }

        public void MarkRequired(string reasonCode, string message, int? statusCode = null)
        {
            Current = new LinkedInSessionResetRequirementState(
                true,
                reasonCode,
                message,
                statusCode,
                DateTimeOffset.UtcNow);
        }

        public void Clear()
        {
            Current = LinkedInSessionResetRequirementState.NotRequired;
        }
    }
}
