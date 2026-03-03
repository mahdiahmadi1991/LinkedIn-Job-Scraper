using LinkedIn.JobScraper.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.Middleware;

public sealed class RequestCorrelationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsyncUsesIncomingCorrelationHeaderWhenPresent()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[RequestCorrelationMiddleware.CorrelationHeaderName] = "incoming-correlation-id";

        var middleware = new RequestCorrelationMiddleware(
            static async httpContext => { await httpContext.Response.WriteAsync("ok"); },
            NullLogger<RequestCorrelationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("incoming-correlation-id", context.TraceIdentifier);
        Assert.Equal(
            "incoming-correlation-id",
            context.Response.Headers[RequestCorrelationMiddleware.CorrelationHeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsyncUsesExistingTraceIdentifierWhenHeaderIsMissing()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "generated-trace-id"
        };

        var middleware = new RequestCorrelationMiddleware(
            static async httpContext => { await httpContext.Response.WriteAsync("ok"); },
            NullLogger<RequestCorrelationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("generated-trace-id", context.TraceIdentifier);
        Assert.Equal(
            "generated-trace-id",
            context.Response.Headers[RequestCorrelationMiddleware.CorrelationHeaderName].ToString());
    }
}
