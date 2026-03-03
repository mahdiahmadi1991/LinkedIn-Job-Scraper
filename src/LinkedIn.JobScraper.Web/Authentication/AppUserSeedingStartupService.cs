using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Authentication;

public sealed class AppUserSeedingStartupService : IHostedService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IAppUserPasswordHasher _passwordHasher;
    private readonly IOptions<AppAuthenticationOptions> _options;
    private readonly ISqlServerConnectionStringProvider _sqlServerConnectionStringProvider;
    private readonly ILogger<AppUserSeedingStartupService> _logger;

    public AppUserSeedingStartupService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IAppUserPasswordHasher passwordHasher,
        IOptions<AppAuthenticationOptions> options,
        ISqlServerConnectionStringProvider sqlServerConnectionStringProvider,
        ILogger<AppUserSeedingStartupService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
        _options = options;
        _sqlServerConnectionStringProvider = sqlServerConnectionStringProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuredSeedUsers = _options.Value.SeedUsers
            .Where(static user => !string.IsNullOrWhiteSpace(user.UserName))
            .ToArray();

        if (configuredSeedUsers.Length == 0 || !_sqlServerConnectionStringProvider.IsConfigured)
        {
            return;
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            if (dbContext.Database.IsRelational())
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            var existingUsers = await dbContext.AppUsers
                .ToDictionaryAsync(user => user.UserName, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var utcNow = DateTimeOffset.UtcNow;
            var hasChanges = false;

            foreach (var seedUser in configuredSeedUsers)
            {
                var normalizedUserName = seedUser.UserName.Trim();

                if (existingUsers.TryGetValue(normalizedUserName, out var existingUser))
                {
                    var normalizedDisplayName = string.IsNullOrWhiteSpace(seedUser.DisplayName)
                        ? normalizedUserName
                        : seedUser.DisplayName.Trim();
                    var normalizedExpiry = NormalizeExpiry(seedUser.ExpiresAtUtc);
                    var hasWritablePassword = !string.IsNullOrWhiteSpace(seedUser.Password);
                    var passwordNeedsUpdate = hasWritablePassword &&
                        !_passwordHasher.VerifyPassword(seedUser.Password.Trim(), existingUser.PasswordHash);

                    if (!string.Equals(existingUser.DisplayName, normalizedDisplayName, StringComparison.Ordinal) ||
                        !existingUser.IsSeeded ||
                        !existingUser.IsActive ||
                        existingUser.ExpiresAtUtc != normalizedExpiry ||
                        passwordNeedsUpdate)
                    {
                        existingUser.DisplayName = normalizedDisplayName;
                        existingUser.IsSeeded = true;
                        existingUser.IsActive = true;
                        existingUser.ExpiresAtUtc = normalizedExpiry;

                        if (passwordNeedsUpdate)
                        {
                            existingUser.PasswordHash = _passwordHasher.HashPassword(seedUser.Password.Trim());
                        }

                        existingUser.UpdatedAtUtc = utcNow;
                        hasChanges = true;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(seedUser.Password))
                {
                    continue;
                }

                dbContext.AppUsers.Add(
                    new AppUserRecord
                    {
                        UserName = seedUser.UserName.Trim(),
                        DisplayName = string.IsNullOrWhiteSpace(seedUser.DisplayName)
                            ? normalizedUserName
                            : seedUser.DisplayName.Trim(),
                        PasswordHash = _passwordHasher.HashPassword(seedUser.Password.Trim()),
                        IsActive = true,
                        IsSeeded = true,
                        ExpiresAtUtc = NormalizeExpiry(seedUser.ExpiresAtUtc),
                        CreatedAtUtc = utcNow,
                        UpdatedAtUtc = utcNow
                    });

                hasChanges = true;
            }

            if (!hasChanges)
            {
                return;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Log.FailedToSynchronizeSeededAppUsers(_logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static DateTimeOffset? NormalizeExpiry(DateTimeOffset? expiresAtUtc)
    {
        return expiresAtUtc?.ToUniversalTime();
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Seeded app users could not be synchronized during startup. The application will continue without failing startup.")]
    public static partial void FailedToSynchronizeSeededAppUsers(ILogger logger, Exception exception);
}
