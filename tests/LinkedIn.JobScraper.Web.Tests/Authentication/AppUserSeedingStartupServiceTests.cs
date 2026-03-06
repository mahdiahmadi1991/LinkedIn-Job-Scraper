using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.JobScraper.Web.Tests.Authentication;

public sealed class AppUserSeedingStartupServiceTests
{
    [Fact]
    public async Task StartAsyncCreatesSuperAdminWhenMissing()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var dbContext = CreateDbContext(databaseName);

        var service = CreateService(databaseName, isSqlConfigured: true);

        await service.StartAsync(CancellationToken.None);

        var user = await dbContext.AppUsers.SingleAsync();
        Assert.Equal(AppUserSeedingStartupService.SuperAdminId, user.Id);
        Assert.Equal(AppUserSeedingStartupService.SuperAdminUserName, user.UserName);
        Assert.Equal(AppUserSeedingStartupService.SuperAdminDisplayName, user.DisplayName);
        Assert.True(user.IsSeeded);
        Assert.True(user.IsActive);
        Assert.True(user.IsSuperAdmin);
        Assert.Null(user.ExpiresAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    [Fact]
    public async Task StartAsyncDoesNothingWhenSqlConnectionIsNotConfigured()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var dbContext = CreateDbContext(databaseName);

        var service = CreateService(databaseName, isSqlConfigured: false);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(0, await dbContext.AppUsers.CountAsync());
    }

    [Fact]
    public async Task StartAsyncNormalizesLegacyIdOneUserAndResetsPassword()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var hasher = new AppUserPasswordHasher();
        var legacyPassword = "LegacyPassw0rd!";

        await using (var setupContext = CreateDbContext(databaseName))
        {
            setupContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = AppUserSeedingStartupService.SuperAdminId,
                    UserName = "legacy-owner",
                    DisplayName = "Legacy Owner",
                    PasswordHash = hasher.HashPassword(legacyPassword),
                    IsActive = false,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(3),
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await setupContext.SaveChangesAsync();
        }

        var service = CreateService(databaseName, isSqlConfigured: true);
        await service.StartAsync(CancellationToken.None);

        await using var verificationContext = CreateDbContext(databaseName);
        var user = await verificationContext.AppUsers.SingleAsync();

        Assert.Equal(AppUserSeedingStartupService.SuperAdminId, user.Id);
        Assert.Equal(AppUserSeedingStartupService.SuperAdminUserName, user.UserName);
        Assert.Equal(AppUserSeedingStartupService.SuperAdminDisplayName, user.DisplayName);
        Assert.True(user.IsActive);
        Assert.True(user.IsSeeded);
        Assert.True(user.IsSuperAdmin);
        Assert.Null(user.ExpiresAtUtc);
        Assert.False(hasher.VerifyPassword(legacyPassword, user.PasswordHash));
    }

    [Fact]
    public async Task StartAsyncThrowsWhenReservedUsernameBelongsToDifferentUserId()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using (var setupContext = CreateDbContext(databaseName))
        {
            setupContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 2,
                    UserName = AppUserSeedingStartupService.SuperAdminUserName,
                    DisplayName = "Wrong Admin",
                    PasswordHash = new AppUserPasswordHasher().HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await setupContext.SaveChangesAsync();
        }

        var service = CreateService(databaseName, isSqlConfigured: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("bootstrap conflict", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsyncThrowsWhenIdOneIsMissingButOtherUsersExist()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using (var setupContext = CreateDbContext(databaseName))
        {
            setupContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "member-1",
                    DisplayName = "Member One",
                    PasswordHash = new AppUserPasswordHasher().HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await setupContext.SaveChangesAsync();
        }

        var service = CreateService(databaseName, isSqlConfigured: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("Id=1 does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LinkedInJobScraperDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new LinkedInJobScraperDbContext(options);
    }

    private static AppUserSeedingStartupService CreateService(string databaseName, bool isSqlConfigured)
    {
        var dbContextOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);

        return new AppUserSeedingStartupService(
            factory,
            new AppUserPasswordHasher(),
            new TestSqlServerConnectionStringProvider(isSqlConfigured),
            NullLogger<AppUserSeedingStartupService>.Instance);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<LinkedInJobScraperDbContext>
    {
        private readonly DbContextOptions<LinkedInJobScraperDbContext> _options;

        public TestDbContextFactory(DbContextOptions<LinkedInJobScraperDbContext> options)
        {
            _options = options;
        }

        public LinkedInJobScraperDbContext CreateDbContext()
        {
            return new LinkedInJobScraperDbContext(_options);
        }

        public ValueTask<LinkedInJobScraperDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new LinkedInJobScraperDbContext(_options));
        }
    }

    private sealed class TestSqlServerConnectionStringProvider : ISqlServerConnectionStringProvider
    {
        public TestSqlServerConnectionStringProvider(bool isConfigured)
        {
            IsConfigured = isConfigured;
        }

        public bool IsConfigured { get; }

        public string GetRequiredConnectionString()
        {
            return "Server=(local);Database=Test;Trusted_Connection=True;";
        }
    }
}
