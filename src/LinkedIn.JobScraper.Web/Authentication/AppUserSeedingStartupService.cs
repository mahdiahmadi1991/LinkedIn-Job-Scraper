using System.Security.Cryptography;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Authentication;

public sealed class AppUserSeedingStartupService : IHostedService
{
    public const int SuperAdminId = 1;
    public const string SuperAdminUserName = "admin@mahdiahmadi.dev";
    public const string SuperAdminDisplayName = "Super Admin";

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IAppUserPasswordHasher _passwordHasher;
    private readonly ISqlServerConnectionStringProvider _sqlServerConnectionStringProvider;
    private readonly ILogger<AppUserSeedingStartupService> _logger;

    public AppUserSeedingStartupService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IAppUserPasswordHasher passwordHasher,
        ISqlServerConnectionStringProvider sqlServerConnectionStringProvider,
        ILogger<AppUserSeedingStartupService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _passwordHasher = passwordHasher;
        _sqlServerConnectionStringProvider = sqlServerConnectionStringProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_sqlServerConnectionStringProvider.IsConfigured)
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var users = await dbContext.AppUsers.ToListAsync(cancellationToken);
        var superAdminById = users.SingleOrDefault(static user => user.Id == SuperAdminId);
        var superAdminByUserName = users.SingleOrDefault(
            user => string.Equals(user.UserName, SuperAdminUserName, StringComparison.OrdinalIgnoreCase));

        if (superAdminByUserName is not null && superAdminByUserName.Id != SuperAdminId)
        {
            throw new InvalidOperationException(
                $"Super-admin bootstrap conflict: '{SuperAdminUserName}' already exists as AppUsers.Id={superAdminByUserName.Id}, but reserved super-admin id is {SuperAdminId}.");
        }

        var utcNow = DateTimeOffset.UtcNow;

        if (superAdminById is null)
        {
            if (users.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Super-admin bootstrap conflict: AppUsers.Id={SuperAdminId} does not exist but {users.Count} user record(s) already exist. Manual remediation is required before startup can continue.");
            }

            var generatedPassword = GenerateRandomPassword();
            var user = new AppUserRecord
            {
                UserName = SuperAdminUserName,
                DisplayName = SuperAdminDisplayName,
                PasswordHash = _passwordHasher.HashPassword(generatedPassword),
                IsActive = true,
                IsSeeded = true,
                IsSuperAdmin = true,
                ExpiresAtUtc = null,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };

            dbContext.AppUsers.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (user.Id != SuperAdminId)
            {
                throw new InvalidOperationException(
                    $"Super-admin bootstrap invariant failed: expected created user id {SuperAdminId}, but received {user.Id}.");
            }

            Log.SuperAdminCreatedWithGeneratedPassword(_logger, SuperAdminUserName, generatedPassword);
            return;
        }

        var hasChanges = false;
        var generatedPasswordForNormalization = default(string);

        if (!string.Equals(superAdminById.UserName, SuperAdminUserName, StringComparison.Ordinal))
        {
            superAdminById.UserName = SuperAdminUserName;
            generatedPasswordForNormalization = GenerateRandomPassword();
            superAdminById.PasswordHash = _passwordHasher.HashPassword(generatedPasswordForNormalization);
            hasChanges = true;
        }

        if (!string.Equals(superAdminById.DisplayName, SuperAdminDisplayName, StringComparison.Ordinal))
        {
            superAdminById.DisplayName = SuperAdminDisplayName;
            hasChanges = true;
        }

        if (!superAdminById.IsActive)
        {
            superAdminById.IsActive = true;
            hasChanges = true;
        }

        if (!superAdminById.IsSeeded)
        {
            superAdminById.IsSeeded = true;
            hasChanges = true;
        }

        if (!superAdminById.IsSuperAdmin)
        {
            superAdminById.IsSuperAdmin = true;
            hasChanges = true;
        }

        if (superAdminById.ExpiresAtUtc is not null)
        {
            superAdminById.ExpiresAtUtc = null;
            hasChanges = true;
        }

        if (string.IsNullOrWhiteSpace(superAdminById.PasswordHash))
        {
            generatedPasswordForNormalization = GenerateRandomPassword();
            superAdminById.PasswordHash = _passwordHasher.HashPassword(generatedPasswordForNormalization);
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return;
        }

        superAdminById.UpdatedAtUtc = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(generatedPasswordForNormalization))
        {
            Log.SuperAdminPasswordResetWithGeneratedPassword(_logger, SuperAdminUserName, generatedPasswordForNormalization);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static string GenerateRandomPassword(int length = 20)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@$%*-_";
        const string all = upper + lower + digits + special;

        var chars = new char[length];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (var index = 4; index < chars.Length; index++)
        {
            chars[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        for (var index = chars.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (chars[index], chars[swapIndex]) = (chars[swapIndex], chars[index]);
        }

        return new string(chars);
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Super-admin account created. Username={UserName}. Generated password={GeneratedPassword}. Store this password securely and rotate it after first sign-in.")]
    public static partial void SuperAdminCreatedWithGeneratedPassword(
        ILogger logger,
        string userName,
        string generatedPassword);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Super-admin account normalized with password reset. Username={UserName}. Generated password={GeneratedPassword}. Store this password securely and rotate it after sign-in.")]
    public static partial void SuperAdminPasswordResetWithGeneratedPassword(
        ILogger logger,
        string userName,
        string generatedPassword);
}
