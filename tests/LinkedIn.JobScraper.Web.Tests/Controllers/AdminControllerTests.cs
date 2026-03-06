using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AdminControllerTests
{
    [Fact]
    public async Task IndexRedirectsToCanonicalUsersTabWhenTabIsMissing()
    {
        var controller = CreateController(new FakeAdminUserManagementService());

        var result = await controller.Index(null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
    }

    [Fact]
    public async Task IndexRedirectsToCanonicalUsersTabWhenTabIsUnknown()
    {
        var controller = CreateController(new FakeAdminUserManagementService());

        var result = await controller.Index("unknown", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal(AdminController.UsersTab, redirect.RouteValues?["tab"]);
    }

    [Fact]
    public async Task IndexReturnsViewForUsersTab()
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

        var result = await controller.Index(AdminController.UsersTab, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/AdminUsers/Index.cshtml", view.ViewName);
        var model = Assert.IsType<AdminUsersPageViewModel>(view.Model);
        Assert.Single(model.Users);
        Assert.Equal("Saved.", model.StatusMessage);
        Assert.True(model.StatusSucceeded);
    }

    private static AdminController CreateController(IAdminUserManagementService userManagementService)
    {
        var httpContext = new DefaultHttpContext();
        return new AdminController(userManagementService)
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

        public Task<IReadOnlyList<AdminUserListItem>> GetUsersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Users);
        }

        public Task<AdminUserCreateResult> CreateUserAsync(AdminUserCreateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserCreateResult(false, "Not configured.", null));
        }

        public Task<AdminUserUpdateResult> UpdateUserProfileAsync(AdminUserUpdateProfileRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserUpdateResult(false, "Not configured.", null));
        }

        public Task<AdminUserUpdateResult> SetUserActiveStateAsync(AdminUserSetActiveStateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserUpdateResult(false, "Not configured.", null));
        }

        public Task<AdminUserDeleteResult> SoftDeleteUserAsync(AdminUserSoftDeleteRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminUserDeleteResult(false, "Not configured.", null, null));
        }
    }
}
