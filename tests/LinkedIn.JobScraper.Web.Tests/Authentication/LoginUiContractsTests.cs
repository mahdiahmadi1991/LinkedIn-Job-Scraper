namespace LinkedIn.JobScraper.Web.Tests.Authentication;

public sealed class LoginUiContractsTests
{
    [Fact]
    public void LoginViewContainsStandardLoadingButtonAndPasswordPeekWiring()
    {
        var viewContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/Views/Account/Login.cshtml");

        Assert.Contains("data-login-form", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-login-submit-button", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-loading-text=\"Signing in...\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-login-password-input", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-login-password-peek", viewContent, StringComparison.Ordinal);
        Assert.Contains("auth-password-field", viewContent, StringComparison.Ordinal);
        Assert.Contains("auth-brand-logo", viewContent, StringComparison.Ordinal);
        Assert.Contains("~/favicon.svg", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-app-version", viewContent, StringComparison.Ordinal);
        Assert.Contains("Version @AppVersionProvider.CurrentVersion", viewContent, StringComparison.Ordinal);
        Assert.Contains("~/js/login-page.js", viewContent, StringComparison.Ordinal);
        Assert.Contains("~/js/site.js", viewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginScriptUsesSharedLoadingHelperAndPressHoldPasswordReveal()
    {
        var scriptContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/wwwroot/js/login-page.js");

        Assert.Contains("window.appButtons?.setLoading", scriptContent, StringComparison.Ordinal);
        Assert.Contains("setButtonLoading(submitButton, true, \"Signing in...\")", scriptContent, StringComparison.Ordinal);
        Assert.Contains("pointerdown", scriptContent, StringComparison.Ordinal);
        Assert.Contains("pointerup", scriptContent, StringComparison.Ordinal);
        Assert.Contains("pointercancel", scriptContent, StringComparison.Ordinal);
        Assert.Contains("pointerleave", scriptContent, StringComparison.Ordinal);
        Assert.Contains("setPasswordVisible(true)", scriptContent, StringComparison.Ordinal);
        Assert.Contains("setPasswordVisible(false)", scriptContent, StringComparison.Ordinal);
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
