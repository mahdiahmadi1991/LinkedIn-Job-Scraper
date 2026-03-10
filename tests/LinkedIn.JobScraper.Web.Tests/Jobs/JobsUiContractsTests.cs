namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobsUiContractsTests
{
    [Fact]
    public void JobsViewContainsSessionOnboardingModalContract()
    {
        var viewContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/Views/Jobs/Index.cshtml");

        Assert.Contains("data-session-onboarding-modal", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-onboarding-connect", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-onboarding-state-url", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-onboarding-verify-url", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-fetch-session-guard-note", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-fetch-session-guard-message", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-open-session-modal", viewContent, StringComparison.Ordinal);
        Assert.Contains("Import your LinkedIn session first", viewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void JobsPageScriptContainsSessionOnboardingTriggerLogic()
    {
        var scriptContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/wwwroot/js/jobs-page.js");

        Assert.Contains("sessionOnboardingModalElement", scriptContent, StringComparison.Ordinal);
        Assert.Contains("sessionOnboardingStorageKey", scriptContent, StringComparison.Ordinal);
        Assert.Contains("fetchSessionState", scriptContent, StringComparison.Ordinal);
        Assert.Contains("verifySessionState", scriptContent, StringComparison.Ordinal);
        Assert.Contains("maybeShowSessionOnboarding", scriptContent, StringComparison.Ordinal);
        Assert.Contains("setSessionOnboardingDismissed", scriptContent, StringComparison.Ordinal);
        Assert.Contains("applyFetchSessionGuard", scriptContent, StringComparison.Ordinal);
        Assert.Contains("runPeriodicSessionVerification", scriptContent, StringComparison.Ordinal);
        Assert.Contains("linkedinsession:state", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-session-onboarding-connect", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-linked-in-session-modal", scriptContent, StringComparison.Ordinal);
        Assert.Contains("resolveSessionGuardMessage", scriptContent, StringComparison.Ordinal);
        Assert.Contains("state?.resetRequirement?.required", scriptContent, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionModalScriptPublishesSessionStateEvent()
    {
        var scriptContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/wwwroot/js/session-modal.js");

        Assert.Contains("linkedinsession:state", scriptContent, StringComparison.Ordinal);
        Assert.Contains("window.dispatchEvent", scriptContent, StringComparison.Ordinal);
        Assert.Contains("CustomEvent", scriptContent, StringComparison.Ordinal);
        Assert.Contains("state?.storedSessionAvailable", scriptContent, StringComparison.Ordinal);
        Assert.Contains("state?.resetRequirement?.required", scriptContent, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionModalViewUsesCurlOnlyUiContract()
    {
        var viewContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/Views/Shared/_LinkedInSessionModal.cshtml");

        Assert.Contains("data-session-reset-note", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-modal-layout", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-curl-panel", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-details-panel", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-curl-browser-hint", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-curl-guide-card=\"chromium\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-curl-guide-card=\"firefox\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-expiration", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-connected-panel", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-start-replace", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-cancel-replace", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-import-curl", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-session-revoke", viewContent, StringComparison.Ordinal);
        Assert.Contains("Copy as cURL", viewContent, StringComparison.Ordinal);
        Assert.Contains("Copy as cURL (bash)", viewContent, StringComparison.Ordinal);
        Assert.Contains("Copy as cURL (cmd)", viewContent, StringComparison.Ordinal);
        Assert.Contains("Copy as cURL (POSIX)", viewContent, StringComparison.Ordinal);
        Assert.Contains("Reset Session", viewContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Revoke Session", viewContent, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary>Connection Details</summary>", viewContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Controlled Browser Connect", viewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionModalScriptContainsCurlOnlyFlowLogic()
    {
        var scriptContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/wwwroot/js/session-modal.js");

        Assert.Contains("importCurlUrl", scriptContent, StringComparison.Ordinal);
        Assert.Contains("revokeUrl", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-session-curl-guide-card", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-session-import-curl", scriptContent, StringComparison.Ordinal);
        Assert.Contains("detectCurlGuideBrowserFamily", scriptContent, StringComparison.Ordinal);
        Assert.Contains("applyCurlGuideRecommendation", scriptContent, StringComparison.Ordinal);
        Assert.Contains("updatePanelVisibility", scriptContent, StringComparison.Ordinal);
        Assert.Contains("replacingSession", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-session-start-replace", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-session-cancel-replace", scriptContent, StringComparison.Ordinal);
        Assert.Contains("state.storedSessionEstimatedExpiresAtUtc", scriptContent, StringComparison.Ordinal);
        Assert.Contains("Reset Required", scriptContent, StringComparison.Ordinal);
        Assert.Contains("isResetRequiredState", scriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain("setWizardStep", scriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain("launchUrl", scriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain("captureUrl", scriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain("state?.capabilities ?? {}", scriptContent, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(baseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "LinkedIn.JobScraper.sln");
            if (File.Exists(solutionPath))
            {
                var targetFilePath = Path.Combine(directory.FullName, relativePath);
                return File.ReadAllText(targetFilePath);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Repository root was not found while locating '{relativePath}'.");
    }
}
