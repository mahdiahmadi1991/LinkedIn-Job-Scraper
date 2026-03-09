using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace LinkedIn.JobScraper.Web.Authentication;

public interface ICurrentAppUserContext
{
    int GetRequiredUserId();

    bool TryGetUserId(out int userId);
}

public sealed class HttpContextCurrentAppUserContext : ICurrentAppUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentAppUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetRequiredUserId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("An authenticated app user context is required for this operation.");
        }

        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(rawUserId))
        {
            throw new InvalidOperationException("Authenticated app user id claim is missing.");
        }

        if (!int.TryParse(rawUserId, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            throw new InvalidOperationException("Authenticated app user id claim is invalid.");
        }

        return userId;
    }

    public bool TryGetUserId(out int userId)
    {
        userId = default;
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(rawUserId, NumberStyles.None, CultureInfo.InvariantCulture, out userId) && userId > 0;
    }
}
