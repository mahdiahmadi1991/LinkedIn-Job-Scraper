namespace LinkedIn.JobScraper.Web.Tests.Users;

public sealed class AdminUsersUiContractsTests
{
    [Fact]
    public void AdminUsersViewContainsLocalTimeAndRandomPasswordWiring()
    {
        var viewContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/Views/AdminUsers/Index.cshtml");

        Assert.Contains("data-admin-users-page", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-user-create-form", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-user-create-submit", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-loading-text=\"Creating user...\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-local-datetime-form", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-local-expiry-date-input", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-utc-hidden", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-utc-display", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-local-timezone-label", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-random-password-target", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-generate-random-password", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-password-input", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-toggle-password-visibility", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-copy-password", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-page-status", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-total", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-update-user-url", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-toggle-user-url", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-soft-delete-user-url", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-row-edit-action", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-row-edit-is-active", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-row-delete-action", viewContent, StringComparison.Ordinal);
        Assert.Contains("type=\"date\" class=\"form-control form-control-sm d-none\" data-row-edit-expires-local", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-empty-state", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-table-wrapper", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-soft-delete-modal", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-soft-delete-user-name", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-soft-delete-confirm", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-soft-delete-cancel", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-pagination", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-page-info", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-prev-page", viewContent, StringComparison.Ordinal);
        Assert.Contains("data-admin-users-next-page", viewContent, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"off\"", viewContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Reset form", viewContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Expires At (UTC)", viewContent, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"datetime-local\" class=\"form-control form-control-sm d-none\" data-row-edit-expires-local", viewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminUsersScriptContainsUtcLocalMappingAndPasswordGeneration()
    {
        var scriptContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/wwwroot/js/admin-users-page.js");

        Assert.Contains("toLocalDateTimeInputValue", scriptContent, StringComparison.Ordinal);
        Assert.Contains("toLocalDateInputValue", scriptContent, StringComparison.Ordinal);
        Assert.Contains("toUtcIsoValue", scriptContent, StringComparison.Ordinal);
        Assert.Contains("localDateToUtcIsoAtEndOfDay", scriptContent, StringComparison.Ordinal);
        Assert.Contains("Intl.DateTimeFormat().resolvedOptions().timeZone", scriptContent, StringComparison.Ordinal);
        Assert.Contains("generateRandomPassword", scriptContent, StringComparison.Ordinal);
        Assert.Contains("window.crypto?.getRandomValues", scriptContent, StringComparison.Ordinal);
        Assert.Contains("bindPasswordVisibilityToggle", scriptContent, StringComparison.Ordinal);
        Assert.Contains("bindPasswordCopyAction", scriptContent, StringComparison.Ordinal);
        Assert.Contains("bindCreateFormSubmission", scriptContent, StringComparison.Ordinal);
        Assert.Contains("window.appButtons?.setLoading", scriptContent, StringComparison.Ordinal);
        Assert.Contains("initializeEditableUserRows", scriptContent, StringComparison.Ordinal);
        Assert.Contains("saveEditableRow", scriptContent, StringComparison.Ordinal);
        Assert.Contains("deleteUserRow", scriptContent, StringComparison.Ordinal);
        Assert.Contains("requestSoftDeleteConfirmation", scriptContent, StringComparison.Ordinal);
        Assert.Contains("initializeSoftDeleteModal", scriptContent, StringComparison.Ordinal);
        Assert.Contains("window.bootstrap?.Modal", scriptContent, StringComparison.Ordinal);
        Assert.DoesNotContain("window.confirm", scriptContent, StringComparison.Ordinal);
        Assert.Contains("softDeleteUserUrl", scriptContent, StringComparison.Ordinal);
        Assert.Contains("usersPaginationState", scriptContent, StringComparison.Ordinal);
        Assert.Contains("pageSize: 20", scriptContent, StringComparison.Ordinal);
        Assert.Contains("bindUsersPaginationControls", scriptContent, StringComparison.Ordinal);
        Assert.Contains("applyUsersPagination", scriptContent, StringComparison.Ordinal);
        Assert.Contains("X-Requested-With", scriptContent, StringComparison.Ordinal);
        Assert.Contains("fetch(createForm.action", scriptContent, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard.writeText", scriptContent, StringComparison.Ordinal);
        Assert.Contains("data-generate-random-password", scriptContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminUsersViewContainsAdministrationTabShell()
    {
        var viewContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/Views/AdminUsers/Index.cshtml");

        Assert.Contains("aria-label=\"Administration tabs\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("id=\"admin-tab-link-@tab.Key\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("id=\"admin-tab-panel-users\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Admin\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("asp-route-tab=\"@tab.Key\"", viewContent, StringComparison.Ordinal);
        Assert.Contains("aria-labelledby=\"admin-tab-link-users\"", viewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void LayoutContainsAdministrationHubMenuLinkForSuperAdmin()
    {
        var layoutContent = ReadRepositoryFile("src/LinkedIn.JobScraper.Web/Views/Shared/_Layout.cshtml");

        Assert.Contains("asp-controller=\"Admin\"", layoutContent, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Index\"", layoutContent, StringComparison.Ordinal);
        Assert.Contains("asp-route-tab=\"users\"", layoutContent, StringComparison.Ordinal);
        Assert.Contains(">Administration</a>", layoutContent, StringComparison.Ordinal);
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
