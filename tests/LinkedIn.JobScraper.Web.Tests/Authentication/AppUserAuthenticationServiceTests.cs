using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.Authentication;

public sealed class AppUserAuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsyncReturnsAuthenticatedUserForValidCredentials()
    {
        var dbContextOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using (var seedContext = new LinkedInJobScraperDbContext(dbContextOptions))
        {
            var passwordHasher = new AppUserPasswordHasher();
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    UserName = "owner",
                    DisplayName = "Local Owner",
                    PasswordHash = passwordHasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            await seedContext.SaveChangesAsync();
        }

        var service = new AppUserAuthenticationService(
            new TestDbContextFactory(dbContextOptions),
            new AppUserPasswordHasher());

        var result = await service.AuthenticateAsync("owner", "Passw0rd!", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("owner", result.User!.UserName);
        Assert.Equal("Local Owner", result.User.DisplayName);
        Assert.False(result.User.IsSuperAdmin);
    }

    [Fact]
    public async Task AuthenticateAsyncReturnsFailureForWrongPassword()
    {
        var dbContextOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using (var seedContext = new LinkedInJobScraperDbContext(dbContextOptions))
        {
            var passwordHasher = new AppUserPasswordHasher();
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    UserName = "owner",
                    DisplayName = "Local Owner",
                    PasswordHash = passwordHasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            await seedContext.SaveChangesAsync();
        }

        var service = new AppUserAuthenticationService(
            new TestDbContextFactory(dbContextOptions),
            new AppUserPasswordHasher());

        var result = await service.AuthenticateAsync("owner", "bad-password", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task AuthenticateAsyncReturnsFailureForExpiredUser()
    {
        var dbContextOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using (var seedContext = new LinkedInJobScraperDbContext(dbContextOptions))
        {
            var passwordHasher = new AppUserPasswordHasher();
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    UserName = "owner",
                    DisplayName = "Local Owner",
                    PasswordHash = passwordHasher.HashPassword("Passw0rd!"),
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            await seedContext.SaveChangesAsync();
        }

        var service = new AppUserAuthenticationService(
            new TestDbContextFactory(dbContextOptions),
            new AppUserPasswordHasher());

        var result = await service.AuthenticateAsync("owner", "Passw0rd!", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.User);
        Assert.Equal("This local account has expired. Ask the app owner to extend or reseed the account.", result.Message);
    }

    [Fact]
    public void CreatePrincipalAddsExpectedIdentityClaims()
    {
        var service = new AppUserAuthenticationService(
            new TestDbContextFactory(
                new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .Options),
            new AppUserPasswordHasher());

        var principal = service.CreatePrincipal(new AppUserIdentity(42, "owner", "Local Owner", true));

        Assert.Equal("owner", principal.Identity?.Name);
        Assert.Equal("42", principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Local Owner", principal.FindFirst(AppUserClaimTypes.DisplayName)?.Value);
        Assert.Equal("true", principal.FindFirst(AppUserClaimTypes.IsSuperAdmin)?.Value);
        Assert.Equal(AppAuthenticationDefaults.CookieScheme, principal.Identity?.AuthenticationType);
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
}
