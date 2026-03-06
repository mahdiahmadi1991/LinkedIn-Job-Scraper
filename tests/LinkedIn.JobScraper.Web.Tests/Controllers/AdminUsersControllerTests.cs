using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task IndexReturnsViewWithUsersAndStatusMessageFromTempData()
    {
        var service = new FakeAdminUserManagementService
        {
            Users =
            [
                new AdminUserListItem(
                    1,
                    "admin@mahdiahmadi.dev",
                    "Super Admin",
                    true,
                    true,
                    null,
                    DateTimeOffset.UtcNow.AddDays(-10),
                    DateTimeOffset.UtcNow.AddDays(-1))
            ]
        };

        var controller = CreateController(service);
        controller.TempData["AdminUsersStatusMessage"] = "Saved.";
        controller.TempData["AdminUsersStatusSucceeded"] = bool.TrueString;

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);

        Assert.Single(model.Users);
        Assert.Equal("admin@mahdiahmadi.dev", model.Users[0].UserName);
        Assert.Equal("Saved.", model.StatusMessage);
        Assert.True(model.StatusSucceeded);
    }

    [Fact]
    public async Task CreateReturnsIndexViewWhenModelStateIsInvalid()
    {
        var service = new FakeAdminUserManagementService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("CreateForm.UserName", "Required");

        var result = await controller.Create(
            new AdminUsersPageViewModel
            {
                CreateForm = new AdminUserCreateFormViewModel
                {
                    Password = "secret"
                }
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
            new AdminUsersPageViewModel
            {
                CreateForm = new AdminUserCreateFormViewModel
                {
                    UserName = "member",
                    DisplayName = "Member",
                    Password = "Passw0rd!",
                    IsActive = true
                }
            },
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminUsersController.Index), redirect.ActionName);
        Assert.Equal("Created user 'member'.", controller.TempData["AdminUsersStatusMessage"]);
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
            new AdminUsersPageViewModel
            {
                CreateForm = new AdminUserCreateFormViewModel
                {
                    UserName = "member",
                    DisplayName = "Member",
                    Password = "Passw0rd!",
                    IsActive = true
                }
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

        public int CreateUserCallCount { get; private set; }

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
    }
}
