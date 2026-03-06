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
        Assert.Contains("data-open-session-modal", viewContent, StringComparison.Ordinal);
        Assert.Contains("Connect your LinkedIn session first", viewContent, StringComparison.Ordinal);
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
    }

    [Fact]
    public void SessionModalScriptPublishesSessionStateEvent()
    {
        var scriptContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/wwwroot/js/session-modal.js");

        Assert.Contains("linkedinsession:state", scriptContent, StringComparison.Ordinal);
        Assert.Contains("window.dispatchEvent", scriptContent, StringComparison.Ordinal);
        Assert.Contains("CustomEvent", scriptContent, StringComparison.Ordinal);
        Assert.Contains("state?.autoCaptureActive && state?.browserOpen", scriptContent, StringComparison.Ordinal);
        Assert.Contains("state.autoCaptureActive && !state.browserOpen", scriptContent, StringComparison.Ordinal);
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
