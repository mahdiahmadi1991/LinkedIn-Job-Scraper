using LinkedIn.JobScraper.Web.Middleware;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace LinkedIn.JobScraper.Web.Tests.Middleware;

public sealed class BasicSecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsyncAddsExpectedSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new BasicSecurityHeadersMiddleware(static async httpContext =>
        {
            await httpContext.Response.WriteAsync("ok");
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsyncDoesNotOverrideExistingHeaderValues()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["Referrer-Policy"] = "same-origin";

        var middleware = new BasicSecurityHeadersMiddleware(static async httpContext =>
        {
            await httpContext.Response.WriteAsync("ok");
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("same-origin", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
    }
}
