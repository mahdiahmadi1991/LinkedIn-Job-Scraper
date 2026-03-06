using System.Globalization;
using System.Security.Claims;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Authentication;

public sealed class AppUserAuthenticationService : IAppUserAuthenticationService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IAppUserPasswordHasher _passwordHasher;

    public AppUserAuthenticationService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IAppUserPasswordHasher passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task<AppUserAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = (userName ?? string.Empty).Trim();
        var normalizedPassword = password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(normalizedPassword))
        {
            return new AppUserAuthenticationResult(false, "Enter your username and password.", null);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activeUsers = await dbContext.AppUsers
            .AsNoTracking()
            .Where(static appUser => appUser.IsActive)
            .ToListAsync(cancellationToken);

        var user = activeUsers.FirstOrDefault(
            appUser => string.Equals(appUser.UserName, normalizedUserName, StringComparison.OrdinalIgnoreCase));

        if (user is null || !_passwordHasher.VerifyPassword(normalizedPassword, user.PasswordHash))
        {
            return new AppUserAuthenticationResult(false, "The username or password is incorrect.", null);
        }

        if (user.ExpiresAtUtc is DateTimeOffset expiresAtUtc && expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new AppUserAuthenticationResult(
                false,
                "This local account has expired. Ask the app owner to extend or reseed the account.",
                null);
        }

        return new AppUserAuthenticationResult(
            true,
            "Authentication succeeded.",
            new AppUserIdentity(user.Id, user.UserName, user.DisplayName, user.IsSuperAdmin));
    }

    public ClaimsPrincipal CreatePrincipal(AppUserIdentity user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.UserName),
            new(AppUserClaimTypes.DisplayName, user.DisplayName),
            new(AppUserClaimTypes.IsSuperAdmin, user.IsSuperAdmin ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, AppAuthenticationDefaults.CookieScheme);
        return new ClaimsPrincipal(identity);
    }
}
