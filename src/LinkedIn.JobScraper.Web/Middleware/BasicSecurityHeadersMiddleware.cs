using Microsoft.AspNetCore.Http;

namespace LinkedIn.JobScraper.Web.Middleware;

public sealed class BasicSecurityHeadersMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        TryAddIfMissing(headers, "X-Content-Type-Options", "nosniff");
        TryAddIfMissing(headers, "X-Frame-Options", "DENY");
        TryAddIfMissing(headers, "Referrer-Policy", "no-referrer");

        await _next(context);
    }

    private static void TryAddIfMissing(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}

public static class BasicSecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseBasicSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<BasicSecurityHeadersMiddleware>();
    }
}
