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
    public IActionResult Index()
    {
        return RedirectToAction(nameof(AdminController.Index), "Admin", new { tab = AdminController.UsersTab });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "CreateForm")] AdminUserCreateFormViewModel form,
        CancellationToken cancellationToken)
    {
        form ??= new AdminUserCreateFormViewModel();
        var isAjaxRequest = IsAjaxRequest();

        if (!ModelState.IsValid)
        {
            var validationMessage = BuildValidationErrorSummary("Review the highlighted user fields and try again.");
            if (isAjaxRequest)
            {
                return BadRequest(BuildCreateAjaxResponse(false, validationMessage, null));
            }

            form.Password = string.Empty;
            var invalidModelView = await BuildPageViewModelAsync(
                cancellationToken,
                createForm: form,
                statusMessage: validationMessage,
                statusSucceeded: false);
            return View("Index", invalidModelView);
        }

        var result = await _adminUserManagementService.CreateUserAsync(
            new AdminUserCreateRequest(
                form.UserName,
                form.DisplayName,
                form.Password,
                form.IsActive,
                form.ExpiresAtUtc),
            cancellationToken);

        if (!result.Success)
        {
            foreach (var validationError in result.ValidationErrors ?? [])
            {
                ModelState.AddModelError(
                    $"CreateForm.{validationError.Field}",
                    validationError.Message);
            }

            if (isAjaxRequest)
            {
                return BadRequest(BuildCreateAjaxResponse(false, result.Message, null));
            }

            form.Password = string.Empty;
            var failedView = await BuildPageViewModelAsync(
                cancellationToken,
                createForm: form,
                statusMessage: result.Message,
                statusSucceeded: false);
            return View("Index", failedView);
        }

        var successMessage = $"Created user '{result.User?.UserName}'.";
        if (isAjaxRequest)
        {
            return Ok(BuildCreateAjaxResponse(true, successMessage, result.User));
        }

        TempData["AdminUsersStatusMessage"] = successMessage;
        TempData["AdminUsersStatusSucceeded"] = bool.TrueString;
        return RedirectToAction(nameof(AdminController.Index), "Admin", new { tab = AdminController.UsersTab });
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        [Bind(Prefix = "UpdateForm")] AdminUserUpdateFormViewModel form,
        CancellationToken cancellationToken)
    {
        var isAjaxRequest = IsAjaxRequest();

        if (!ModelState.IsValid)
        {
            var validationMessage = BuildValidationErrorSummary("Review the highlighted user fields and try again.");
            if (isAjaxRequest)
            {
                return BadRequest(BuildAjaxResponse(false, validationMessage, null, "UpdateForm."));
            }

            var invalidModelView = await BuildPageViewModelAsync(
                cancellationToken,
                updateForm: form,
                statusMessage: validationMessage,
                statusSucceeded: false);
            return View("Index", invalidModelView);
        }

        var result = await _adminUserManagementService.UpdateUserProfileAsync(
            new AdminUserUpdateProfileRequest(
                form.UserId,
                form.DisplayName,
                form.ExpiresAtUtc),
            cancellationToken);

        if (!result.Success)
        {
            foreach (var validationError in result.ValidationErrors ?? [])
            {
                ModelState.AddModelError(
                    $"UpdateForm.{validationError.Field}",
                    validationError.Message);
            }

            if (isAjaxRequest)
            {
                return BadRequest(BuildAjaxResponse(false, result.Message, null, "UpdateForm."));
            }

            var failedView = await BuildPageViewModelAsync(
                cancellationToken,
                updateForm: form,
                statusMessage: result.Message,
                statusSucceeded: false);
            return View("Index", failedView);
        }

        var successMessage = $"Updated user '{result.User?.UserName}'.";
        if (isAjaxRequest)
        {
            return Ok(BuildAjaxResponse(true, successMessage, result.User, "UpdateForm."));
        }

        TempData["AdminUsersStatusMessage"] = successMessage;
        TempData["AdminUsersStatusSucceeded"] = bool.TrueString;
        return RedirectToAction(nameof(AdminController.Index), "Admin", new { tab = AdminController.UsersTab });
    }

    [HttpPost("set-active-state")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActiveState(
        [Bind(Prefix = "ToggleActiveForm")] AdminUserSetActiveStateFormViewModel form,
        CancellationToken cancellationToken)
    {
        var isAjaxRequest = IsAjaxRequest();

        if (!ModelState.IsValid)
        {
            var validationMessage = BuildValidationErrorSummary("Review the highlighted user fields and try again.");
            if (isAjaxRequest)
            {
                return BadRequest(BuildAjaxResponse(false, validationMessage, null, "ToggleActiveForm."));
            }

            var invalidModelView = await BuildPageViewModelAsync(
                cancellationToken,
                toggleActiveForm: form,
                statusMessage: validationMessage,
                statusSucceeded: false);
            return View("Index", invalidModelView);
        }

        var result = await _adminUserManagementService.SetUserActiveStateAsync(
            new AdminUserSetActiveStateRequest(form.UserId, form.IsActive),
            cancellationToken);

        if (!result.Success)
        {
            foreach (var validationError in result.ValidationErrors ?? [])
            {
                ModelState.AddModelError(
                    $"ToggleActiveForm.{validationError.Field}",
                    validationError.Message);
            }

            if (isAjaxRequest)
            {
                return BadRequest(BuildAjaxResponse(false, result.Message, null, "ToggleActiveForm."));
            }

            var failedView = await BuildPageViewModelAsync(
                cancellationToken,
                toggleActiveForm: form,
                statusMessage: result.Message,
                statusSucceeded: false);
            return View("Index", failedView);
        }

        var successMessage = $"Updated activation state for user '{result.User?.UserName}'.";
        if (isAjaxRequest)
        {
            return Ok(BuildAjaxResponse(true, successMessage, result.User, "ToggleActiveForm."));
        }

        TempData["AdminUsersStatusMessage"] = successMessage;
        TempData["AdminUsersStatusSucceeded"] = bool.TrueString;
        return RedirectToAction(nameof(AdminController.Index), "Admin", new { tab = AdminController.UsersTab });
    }

    [HttpPost("soft-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(
        [Bind(Prefix = "SoftDeleteForm")] AdminUserSoftDeleteFormViewModel form,
        CancellationToken cancellationToken)
    {
        var isAjaxRequest = IsAjaxRequest();

        if (!ModelState.IsValid)
        {
            var validationMessage = BuildValidationErrorSummary("Review the highlighted user fields and try again.");
            if (isAjaxRequest)
            {
                return BadRequest(BuildDeleteAjaxResponse(false, validationMessage, null, null));
            }

            var invalidModelView = await BuildPageViewModelAsync(
                cancellationToken,
                softDeleteForm: form,
                statusMessage: validationMessage,
                statusSucceeded: false);
            return View("Index", invalidModelView);
        }

        var result = await _adminUserManagementService.SoftDeleteUserAsync(
            new AdminUserSoftDeleteRequest(form.UserId),
            cancellationToken);

        if (!result.Success)
        {
            foreach (var validationError in result.ValidationErrors ?? [])
            {
                ModelState.AddModelError(
                    $"SoftDeleteForm.{validationError.Field}",
                    validationError.Message);
            }

            if (isAjaxRequest)
            {
                return BadRequest(BuildDeleteAjaxResponse(false, result.Message, null, null));
            }

            var failedView = await BuildPageViewModelAsync(
                cancellationToken,
                softDeleteForm: form,
                statusMessage: result.Message,
                statusSucceeded: false);
            return View("Index", failedView);
        }

        var successMessage = $"Soft-deleted user '{result.UserName}'.";
        if (isAjaxRequest)
        {
            return Ok(BuildDeleteAjaxResponse(true, successMessage, result.UserId, result.UserName));
        }

        TempData["AdminUsersStatusMessage"] = successMessage;
        TempData["AdminUsersStatusSucceeded"] = bool.TrueString;
        return RedirectToAction(nameof(AdminController.Index), "Admin", new { tab = AdminController.UsersTab });
    }

    private async Task<AdminUsersPageViewModel> BuildPageViewModelAsync(
        CancellationToken cancellationToken,
        AdminUserCreateFormViewModel? createForm = null,
        AdminUserUpdateFormViewModel? updateForm = null,
        AdminUserSetActiveStateFormViewModel? toggleActiveForm = null,
        AdminUserSoftDeleteFormViewModel? softDeleteForm = null,
        string? statusMessage = null,
        bool? statusSucceeded = null)
    {
        var users = await _adminUserManagementService.GetUsersAsync(cancellationToken);

        return new AdminUsersPageViewModel
        {
            CreateForm = createForm ?? new AdminUserCreateFormViewModel
            {
                IsActive = true
            },
            UpdateForm = updateForm ?? new AdminUserUpdateFormViewModel(),
            ToggleActiveForm = toggleActiveForm ?? new AdminUserSetActiveStateFormViewModel(),
            SoftDeleteForm = softDeleteForm ?? new AdminUserSoftDeleteFormViewModel(),
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
            StatusMessage = statusMessage ?? TempData["AdminUsersStatusMessage"] as string,
            StatusSucceeded = statusSucceeded ?? string.Equals(
                TempData["AdminUsersStatusSucceeded"] as string,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase)
        };
    }

    private string BuildValidationErrorSummary(string fallbackMessage)
    {
        var validationMessages = ModelState.Values
            .SelectMany(static entry => entry.Errors)
            .Select(static error => error.ErrorMessage)
            .Where(static message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        if (validationMessages.Length == 0)
        {
            return fallbackMessage;
        }

        return $"{fallbackMessage} {string.Join(" ", validationMessages)}";
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private object BuildCreateAjaxResponse(bool success, string message, AdminUserListItem? user)
    {
        return BuildAjaxResponse(success, message, user, "CreateForm.");
    }

    private object BuildDeleteAjaxResponse(bool success, string message, int? userId, string? userName)
    {
        return new
        {
            success,
            message,
            userId,
            userName,
            errors = CollectFormErrors("SoftDeleteForm.")
        };
    }

    private object BuildAjaxResponse(bool success, string message, AdminUserListItem? user, string fieldPrefix)
    {
        return new
        {
            success,
            message,
            user,
            errors = CollectFormErrors(fieldPrefix)
        };
    }

    private Dictionary<string, string[]> CollectFormErrors(string prefix)
    {
        return ModelState
            .Where(
                item => item.Value is not null &&
                        item.Value.Errors.Count > 0 &&
                        item.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(
                item => item.Key[prefix.Length..],
                item => item.Value!.Errors
                    .Select(static error => error.ErrorMessage)
                    .Where(static message => !string.IsNullOrWhiteSpace(message))
                    .ToArray(),
                StringComparer.Ordinal);
    }
}
