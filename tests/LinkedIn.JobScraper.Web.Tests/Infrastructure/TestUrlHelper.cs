using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LinkedIn.JobScraper.Web.Tests.Infrastructure;

internal sealed class TestUrlHelper : IUrlHelper
{
    private readonly string _url;

    public TestUrlHelper(string url)
    {
        _url = url;
    }

    public ActionContext ActionContext => new();

    public string? Action(UrlActionContext actionContext)
    {
        return _url;
    }

    public string? Content(string? contentPath)
    {
        return contentPath;
    }

    public bool IsLocalUrl(string? url)
    {
        return true;
    }

    public string? Link(string? routeName, object? values)
    {
        return _url;
    }

    public string? RouteUrl(UrlRouteContext routeContext)
    {
        return _url;
    }
}
