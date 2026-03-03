using Microsoft.AspNetCore.Http;

namespace LinkedIn.JobScraper.Web.Middleware;

public sealed class RequestCorrelationMiddleware
{
    public const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeaderName] = correlationId;

        using (_logger.BeginScope(
                   new Dictionary<string, object?>(StringComparer.Ordinal)
                   {
                       ["CorrelationId"] = correlationId
                   }))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incomingValue = context.Request.Headers[CorrelationHeaderName].ToString().Trim();
        return string.IsNullOrWhiteSpace(incomingValue)
            ? context.TraceIdentifier
            : incomingValue;
    }
}

public static class RequestCorrelationMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RequestCorrelationMiddleware>();
    }
}
