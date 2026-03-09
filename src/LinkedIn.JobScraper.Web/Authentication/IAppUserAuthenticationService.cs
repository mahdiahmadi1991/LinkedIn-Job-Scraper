using System.Security.Claims;
using LinkedIn.JobScraper.Web.Contracts;

namespace LinkedIn.JobScraper.Web.Authentication;

public interface IAppUserAuthenticationService
{
    Task<AppUserAuthenticationResult> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);

    ClaimsPrincipal CreatePrincipal(AppUserIdentity user);
}

public sealed record AppUserIdentity(
    int Id,
    string UserName,
    string DisplayName,
    bool IsSuperAdmin);

public sealed record AppUserAuthenticationResult(
    bool Success,
    string Message,
    AppUserIdentity? User) : OperationResult(Success, Message);
