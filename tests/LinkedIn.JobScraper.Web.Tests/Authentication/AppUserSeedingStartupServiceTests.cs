using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Authentication;

public sealed class AppUserSeedingStartupServiceTests
{
    [Fact]
    public async Task StartAsyncAddsConfiguredSeedUserWhenMissing()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var dbContext = CreateDbContext(databaseName);

        var service = CreateService(
            databaseName,
            new AppAuthenticationOptions
            {
                SeedUsers =
                [
                    new AppAuthenticationSeedUserOptions
                    {
                        UserName = "owner",
                        DisplayName = "Local Owner",
                        Password = "Passw0rd!"
                    }
                ]
            },
            isSqlConfigured: true);

        await service.StartAsync(CancellationToken.None);

        var user = await dbContext.AppUsers.SingleAsync();
        Assert.Equal("owner", user.UserName);
        Assert.Equal("Local Owner", user.DisplayName);
        Assert.True(user.IsSeeded);
        Assert.True(user.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    [Fact]
    public async Task StartAsyncDoesNothingWhenSqlConnectionIsNotConfigured()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var dbContext = CreateDbContext(databaseName);

        var service = CreateService(
            databaseName,
            new AppAuthenticationOptions
            {
                SeedUsers =
                [
                    new AppAuthenticationSeedUserOptions
                    {
                        UserName = "owner",
                        Password = "Passw0rd!"
                    }
                ]
            },
            isSqlConfigured: false);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(0, await dbContext.AppUsers.CountAsync());
    }

    [Fact]
    public async Task StartAsyncUpdatesExistingSeedUserPasswordWhenPasswordChanges()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var hasher = new AppUserPasswordHasher();

        await using (var setupContext = CreateDbContext(databaseName))
        {
            setupContext.AppUsers.Add(
                new AppUserRecord
                {
                    UserName = "owner",
                    DisplayName = "Local Owner",
                    PasswordHash = hasher.HashPassword("OldPassw0rd!"),
                    IsActive = true,
                    IsSeeded = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await setupContext.SaveChangesAsync();
        }

        var service = CreateService(
            databaseName,
            new AppAuthenticationOptions
            {
                SeedUsers =
                [
                    new AppAuthenticationSeedUserOptions
                    {
                        UserName = "owner",
                        DisplayName = "Local Owner",
                        Password = "NewPassw0rd!"
                    }
                ]
            },
            isSqlConfigured: true);

        await service.StartAsync(CancellationToken.None);

        await using var verificationContext = CreateDbContext(databaseName);
        var user = await verificationContext.AppUsers.SingleAsync();

        Assert.True(hasher.VerifyPassword("NewPassw0rd!", user.PasswordHash));
        Assert.False(hasher.VerifyPassword("OldPassw0rd!", user.PasswordHash));
    }

    private static LinkedInJobScraperDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new LinkedInJobScraperDbContext(options);
    }

    private static AppUserSeedingStartupService CreateService(
        string databaseName,
        AppAuthenticationOptions options,
        bool isSqlConfigured)
    {
        var dbContextOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);

        return new AppUserSeedingStartupService(
            factory,
            new AppUserPasswordHasher(),
            Options.Create(options),
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
