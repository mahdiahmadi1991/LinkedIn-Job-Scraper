using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(
    AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme,
    Policy = AppAuthorizationPolicies.SuperAdminOnly)]
[Route("admin/users")]
public sealed class AdminUsersController : Controller
{
    private readonly IAdminUserManagementService _adminUserManagementService;

    public AdminUsersController(IAdminUserManagementService adminUserManagementService)
    {
        _adminUserManagementService = adminUserManagementService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _adminUserManagementService.GetUsersAsync(cancellationToken);
        var viewModel = new AdminUsersPageViewModel
        {
            CreateForm = new AdminUserCreateFormViewModel
            {
                IsActive = true
            },
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

        return View(viewModel);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AdminUsersPageViewModel viewModel,
        CancellationToken cancellationToken)
    {
        viewModel.CreateForm ??= new AdminUserCreateFormViewModel();
        viewModel.Users = await GetUsersForViewAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            viewModel.StatusMessage = "Review the highlighted user fields and try again.";
            viewModel.StatusSucceeded = false;
            viewModel.CreateForm.Password = string.Empty;
            return View("Index", viewModel);
        }

        var result = await _adminUserManagementService.CreateUserAsync(
            new AdminUserCreateRequest(
                viewModel.CreateForm.UserName,
                viewModel.CreateForm.DisplayName,
                viewModel.CreateForm.Password,
                viewModel.CreateForm.IsActive,
                viewModel.CreateForm.ExpiresAtUtc),
            cancellationToken);

        if (!result.Success)
        {
            foreach (var validationError in result.ValidationErrors ?? [])
            {
                ModelState.AddModelError(
                    $"CreateForm.{validationError.Field}",
                    validationError.Message);
            }

            viewModel.StatusMessage = result.Message;
            viewModel.StatusSucceeded = false;
            viewModel.CreateForm.Password = string.Empty;
            return View("Index", viewModel);
        }

        TempData["AdminUsersStatusMessage"] = $"Created user '{result.User?.UserName}'.";
        TempData["AdminUsersStatusSucceeded"] = bool.TrueString;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<AdminUserListItemViewModel>> GetUsersForViewAsync(CancellationToken cancellationToken)
    {
        var users = await _adminUserManagementService.GetUsersAsync(cancellationToken);

        return users.Select(
                static user => new AdminUserListItemViewModel(
                    user.Id,
                    user.UserName,
                    user.DisplayName,
                    user.IsActive,
                    user.IsSuperAdmin,
                    user.ExpiresAtUtc,
                    user.CreatedAtUtc,
                    user.UpdatedAtUtc))
            .ToArray();
    }
}
