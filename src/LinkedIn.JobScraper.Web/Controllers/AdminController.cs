using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(
    AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme,
    Policy = AppAuthorizationPolicies.SuperAdminOnly)]
[Route("admin")]
public sealed class AdminController : Controller
{
    public const string UsersTab = "users";

    private readonly IAdminUserManagementService _adminUserManagementService;

    public AdminController(IAdminUserManagementService adminUserManagementService)
    {
        _adminUserManagementService = adminUserManagementService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        var normalizedTab = NormalizeTab(tab);
        if (!string.Equals(normalizedTab, tab, StringComparison.Ordinal))
        {
            return RedirectToAction(nameof(Index), new { tab = normalizedTab });
        }

        var users = await _adminUserManagementService.GetUsersAsync(cancellationToken);
        var viewModel = new AdminUsersPageViewModel
        {
            CreateForm = new AdminUserCreateFormViewModel
            {
                IsActive = true
            },
            UpdateForm = new AdminUserUpdateFormViewModel(),
            ToggleActiveForm = new AdminUserSetActiveStateFormViewModel(),
            SoftDeleteForm = new AdminUserSoftDeleteFormViewModel(),
            Users = users.Select(
                    static user => new AdminUserListItemViewModel(
                        user.Id,
                        user.UserName,
                        user.DisplayName,
                        user.IsActive,
                        user.IsSuperAdmin,
                        user.ExpiresAtUtc,
                        user.CreatedAtUtc,
                        user.UpdatedAtUtc))
                .ToArray(),
            StatusMessage = TempData["AdminUsersStatusMessage"] as string,
            StatusSucceeded = string.Equals(
                TempData["AdminUsersStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase)
        };

        return View("~/Views/AdminUsers/Index.cshtml", viewModel);
    }

    private static string NormalizeTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
        {
            return UsersTab;
        }

        var normalizedTab = tab.Trim().ToLowerInvariant();
        return string.Equals(normalizedTab, UsersTab, StringComparison.Ordinal)
            ? UsersTab
            : UsersTab;
    }
}
