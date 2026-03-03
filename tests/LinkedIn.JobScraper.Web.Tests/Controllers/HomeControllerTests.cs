using LinkedIn.JobScraper.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class HomeControllerTests
{
    [Fact]
    public void IndexRedirectsToJobsIndex()
    {
        var controller = new HomeController();

        var result = controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Jobs", redirect.ControllerName);
    }
}
