using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public void IndexRedirectsToAdministrationHubUsersTab()
    {
        var controller = CreateController(new FakeAdminUserManagementService());

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
    }

    [Fact]
    public async Task CreateReturnsIndexViewWhenModelStateIsInvalid()
    {
        var service = new FakeAdminUserManagementService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("CreateForm.UserName", "Required");

        var result = await controller.Create(
            new AdminUserCreateFormViewModel
            {
                Password = "secret"
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Equal(string.Empty, model.CreateForm.Password);
        Assert.Equal(0, service.CreateUserCallCount);
    }

    [Fact]
    public async Task CreateRedirectsToIndexWhenServiceReturnsSuccess()
    {
        var service = new FakeAdminUserManagementService
        {
            CreateResult = new AdminUserCreateResult(
                true,
                "Created",
                new AdminUserListItem(
                    2,
                    "member",
                    "Member",
                    true,
                    false,
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
        var controller = CreateController(service);

        var result = await controller.Create(
            new AdminUserCreateFormViewModel
            {
                UserName = "member",
                DisplayName = "Member",
                Password = "Passw0rd!",
                IsActive = true
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
        Assert.Equal("Created user 'member'.", controller.TempData["AdminUsersStatusMessage"]);
    }

    [Fact]
    public async Task CreateReturnsOkJsonWhenAjaxRequestSucceeds()
    {
        var service = new FakeAdminUserManagementService
        {
            CreateResult = new AdminUserCreateResult(
                true,
                "Created",
                new AdminUserListItem(
                    3,
                    "new-member",
                    "New Member",
                    true,
                    false,
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.Create(
            new AdminUserCreateFormViewModel
            {
                UserName = "new-member",
                DisplayName = "New Member",
                Password = "Passw0rd!",
                IsActive = true
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"success\":true", payload, StringComparison.Ordinal);
        Assert.Contains("Created user", payload, StringComparison.Ordinal);
        Assert.Contains("new-member", payload, StringComparison.Ordinal);
        Assert.Equal(1, service.CreateUserCallCount);
    }

    [Fact]
    public async Task CreateReturnsIndexViewAndModelErrorsWhenServiceReturnsValidationFailure()
    {
        var service = new FakeAdminUserManagementService
        {
            CreateResult = new AdminUserCreateResult(
                false,
                "Invalid",
                null,
                false,
                [new AdminUserValidationError("UserName", "Username is already in use.")])
        };
        var controller = CreateController(service);

        var result = await controller.Create(
            new AdminUserCreateFormViewModel
            {
                UserName = "member",
                DisplayName = "Member",
                Password = "Passw0rd!",
                IsActive = true
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Equal("Invalid", model.StatusMessage);
        Assert.Equal(string.Empty, model.CreateForm.Password);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("CreateForm.UserName", controller.ModelState.Keys);
    }

    [Fact]
    public async Task CreateReturnsBadRequestJsonWhenAjaxRequestHasValidationErrors()
    {
        var service = new FakeAdminUserManagementService();
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        controller.ModelState.AddModelError("CreateForm.UserName", "Username is required.");

        var result = await controller.Create(
            new AdminUserCreateFormViewModel
            {
                Password = "secret"
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = JsonSerializer.Serialize(badRequest.Value);

        Assert.Contains("\"success\":false", payload, StringComparison.Ordinal);
        Assert.Contains("Username is required.", payload, StringComparison.Ordinal);
        Assert.Equal(0, service.CreateUserCallCount);
    }

    [Fact]
    public async Task UpdateRedirectsToIndexWhenServiceReturnsSuccess()
    {
        var service = new FakeAdminUserManagementService
        {
            UpdateResult = new AdminUserUpdateResult(
                true,
                "Updated",
                new AdminUserListItem(
                    2,
                    "member",
                    "Updated Member",
                    true,
                    false,
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
        var controller = CreateController(service);

        var result = await controller.Update(
            new AdminUserUpdateFormViewModel
            {
                UserId = 2,
                DisplayName = "Updated Member"
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
        Assert.Equal("Updated user 'member'.", controller.TempData["AdminUsersStatusMessage"]);
        Assert.Equal(1, service.UpdateUserCallCount);
    }

    [Fact]
    public async Task UpdateReturnsOkJsonWhenAjaxRequestSucceeds()
    {
        var service = new FakeAdminUserManagementService
        {
            UpdateResult = new AdminUserUpdateResult(
                true,
                "Updated",
                new AdminUserListItem(
                    2,
                    "member",
                    "Updated Member",
                    true,
                    false,
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.Update(
            new AdminUserUpdateFormViewModel
            {
                UserId = 2,
                DisplayName = "Updated Member"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"success\":true", payload, StringComparison.Ordinal);
        Assert.Contains("Updated user", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateReturnsIndexViewAndModelErrorsWhenServiceReturnsValidationFailure()
    {
        var service = new FakeAdminUserManagementService
        {
            UpdateResult = new AdminUserUpdateResult(
                false,
                "Invalid",
                null,
                [new AdminUserValidationError("ExpiresAtUtc", "Expiry time must be in the future.")])
        };
        var controller = CreateController(service);

        var result = await controller.Update(
            new AdminUserUpdateFormViewModel
            {
                UserId = 2,
                DisplayName = "Member"
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Equal("Invalid", model.StatusMessage);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("UpdateForm.ExpiresAtUtc", controller.ModelState.Keys);
    }

    [Fact]
    public async Task SetActiveStateRedirectsToIndexWhenServiceReturnsSuccess()
    {
        var service = new FakeAdminUserManagementService
        {
            SetActiveStateResult = new AdminUserUpdateResult(
                true,
                "Updated",
                new AdminUserListItem(
                    2,
                    "member",
                    "Member",
                    false,
                    false,
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
        var controller = CreateController(service);

        var result = await controller.SetActiveState(
            new AdminUserSetActiveStateFormViewModel
            {
                UserId = 2,
                IsActive = false
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
        Assert.Equal("Updated activation state for user 'member'.", controller.TempData["AdminUsersStatusMessage"]);
        Assert.Equal(1, service.SetActiveStateCallCount);
    }

    [Fact]
    public async Task SetActiveStateReturnsOkJsonWhenAjaxRequestSucceeds()
    {
        var service = new FakeAdminUserManagementService
        {
            SetActiveStateResult = new AdminUserUpdateResult(
                true,
                "Updated",
                new AdminUserListItem(
                    2,
                    "member",
                    "Member",
                    false,
                    false,
                    null,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow))
        };
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.SetActiveState(
            new AdminUserSetActiveStateFormViewModel
            {
                UserId = 2,
                IsActive = false
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"success\":true", payload, StringComparison.Ordinal);
        Assert.Contains("Updated activation state", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetActiveStateReturnsIndexViewAndModelErrorsWhenServiceReturnsValidationFailure()
    {
        var service = new FakeAdminUserManagementService
        {
            SetActiveStateResult = new AdminUserUpdateResult(
                false,
                "Super-admin user cannot be modified.",
                null,
                [new AdminUserValidationError("UserId", "Super-admin user cannot be modified.")])
        };
        var controller = CreateController(service);

        var result = await controller.SetActiveState(
            new AdminUserSetActiveStateFormViewModel
            {
                UserId = 1,
                IsActive = false
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Equal("Super-admin user cannot be modified.", model.StatusMessage);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains("ToggleActiveForm.UserId", controller.ModelState.Keys);
    }

    [Fact]
    public async Task SoftDeleteRedirectsToIndexWhenServiceReturnsSuccess()
    {
        var service = new FakeAdminUserManagementService
        {
            SoftDeleteResult = new AdminUserDeleteResult(
                true,
                "Deleted",
                2,
                "member")
        };
        var controller = CreateController(service);

        var result = await controller.SoftDelete(
            new AdminUserSoftDeleteFormViewModel
            {
                UserId = 2
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Equal("Admin", redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
        Assert.Equal("Soft-deleted user 'member'.", controller.TempData["AdminUsersStatusMessage"]);
        Assert.Equal(1, service.SoftDeleteCallCount);
    }

    [Fact]
    public async Task SoftDeleteReturnsOkJsonWhenAjaxRequestSucceeds()
    {
        var service = new FakeAdminUserManagementService
        {
            SoftDeleteResult = new AdminUserDeleteResult(
                true,
                "Deleted",
                3,
                "new-member")
        };
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await controller.SoftDelete(
            new AdminUserSoftDeleteFormViewModel
            {
                UserId = 3
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"success\":true", payload, StringComparison.Ordinal);
        Assert.Contains("Soft-deleted user", payload, StringComparison.Ordinal);
        Assert.Contains("\"userId\":3", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SoftDeleteReturnsBadRequestJsonWhenAjaxRequestHasValidationErrors()
    {
        var service = new FakeAdminUserManagementService();
        var controller = CreateController(service);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        controller.ModelState.AddModelError("SoftDeleteForm.UserId", "User id must be greater than zero.");

        var result = await controller.SoftDelete(
            new AdminUserSoftDeleteFormViewModel
            {
                UserId = 0
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = JsonSerializer.Serialize(badRequest.Value);

        Assert.Contains("\"success\":false", payload, StringComparison.Ordinal);
        Assert.Contains("User id must be greater than zero.", payload, StringComparison.Ordinal);
        Assert.Equal(0, service.SoftDeleteCallCount);
    }

    private static AdminUsersController CreateController(IAdminUserManagementService userManagementService)
    {
        var httpContext = new DefaultHttpContext();
        return new AdminUsersController(userManagementService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private sealed class FakeAdminUserManagementService : IAdminUserManagementService
    {
        public IReadOnlyList<AdminUserListItem> Users { get; set; } = [];

        public AdminUserCreateResult? CreateResult { get; set; }

        public AdminUserUpdateResult? UpdateResult { get; set; }

        public AdminUserUpdateResult? SetActiveStateResult { get; set; }

        public AdminUserDeleteResult? SoftDeleteResult { get; set; }

        public int CreateUserCallCount { get; private set; }

        public int UpdateUserCallCount { get; private set; }

        public int SetActiveStateCallCount { get; private set; }

        public int SoftDeleteCallCount { get; private set; }

        public Task<IReadOnlyList<AdminUserListItem>> GetUsersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Users);
        }

        public Task<AdminUserCreateResult> CreateUserAsync(AdminUserCreateRequest request, CancellationToken cancellationToken)
        {
            CreateUserCallCount++;
            return Task.FromResult(
                CreateResult ??
                new AdminUserCreateResult(
                    false,
                    "Create result was not configured for test.",
                    null));
        }

        public Task<AdminUserUpdateResult> UpdateUserProfileAsync(AdminUserUpdateProfileRequest request, CancellationToken cancellationToken)
        {
            UpdateUserCallCount++;
            return Task.FromResult(
                UpdateResult ??
                new AdminUserUpdateResult(false, "Update result was not configured for test.", null));
        }

        public Task<AdminUserUpdateResult> SetUserActiveStateAsync(AdminUserSetActiveStateRequest request, CancellationToken cancellationToken)
        {
            SetActiveStateCallCount++;
            return Task.FromResult(
                SetActiveStateResult ??
                new AdminUserUpdateResult(false, "Set-active-state result was not configured for test.", null));
        }

        public Task<AdminUserDeleteResult> SoftDeleteUserAsync(AdminUserSoftDeleteRequest request, CancellationToken cancellationToken)
        {
            SoftDeleteCallCount++;
            return Task.FromResult(
                SoftDeleteResult ??
                new AdminUserDeleteResult(false, "Soft-delete result was not configured for test.", null, null));
        }
    }
}
