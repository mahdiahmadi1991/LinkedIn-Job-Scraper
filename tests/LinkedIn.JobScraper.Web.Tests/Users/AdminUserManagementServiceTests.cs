using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Authentication;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using LinkedIn.JobScraper.Web.Users;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.Users;

public sealed class AdminUserManagementServiceTests
{
    [Fact]
    public async Task GetUsersAsyncReturnsOrderedUsersForSuperAdmin()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.AddRange(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = new AppUserPasswordHasher().HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "member",
                    DisplayName = "Member",
                    PasswordHash = new AppUserPasswordHasher().HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                new AppUserRecord
                {
                    Id = 3,
                    UserName = "deleted-member",
                    DisplayName = "Deleted Member",
                    PasswordHash = new AppUserPasswordHasher().HashPassword("Passw0rd!"),
                    IsActive = false,
                    IsDeleted = true,
                    DeletedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var users = await service.GetUsersAsync(CancellationToken.None);

        Assert.Equal(2, users.Count);
        Assert.Equal(1, users[0].Id);
        Assert.True(users[0].IsSuperAdmin);
        Assert.Equal(2, users[1].Id);
        Assert.False(users[1].IsSuperAdmin);
    }

    [Fact]
    public async Task CreateUserAsyncCreatesRegularUserAndHashesPassword()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.CreateUserAsync(
            new AdminUserCreateRequest(
                "new-user",
                "New User",
                "Passw0rd!",
                true,
                DateTimeOffset.UtcNow.AddDays(10)),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.False(result.User!.IsSuperAdmin);
        Assert.False(result.IsConflict);

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var created = await verificationContext.AppUsers.SingleAsync(user => user.UserName == "new-user");
        Assert.True(hasher.VerifyPassword("Passw0rd!", created.PasswordHash));
    }

    [Fact]
    public async Task CreateUserAsyncCreatedUserCanAuthenticate()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var managementService = CreateService(options, currentUserId: 1);
        var createResult = await managementService.CreateUserAsync(
            new AdminUserCreateRequest(
                "new-user",
                "New User",
                "Passw0rd!",
                true,
                null),
            CancellationToken.None);

        Assert.True(createResult.Success);

        var authenticationService = new AppUserAuthenticationService(
            new TestDbContextFactory(options),
            hasher);
        var authenticationResult = await authenticationService.AuthenticateAsync(
            "new-user",
            "Passw0rd!",
            CancellationToken.None);

        Assert.True(authenticationResult.Success);
        Assert.NotNull(authenticationResult.User);
        Assert.Equal("new-user", authenticationResult.User!.UserName);
        Assert.False(authenticationResult.User.IsSuperAdmin);
    }

    [Fact]
    public async Task CreateUserAsyncRejectsReservedSuperAdminUsername()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "legacy-admin",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.CreateUserAsync(
            new AdminUserCreateRequest(
                AppUserSeedingStartupService.SuperAdminUserName,
                "Another Admin",
                "Passw0rd!",
                true,
                null),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsConflict);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!, error => error.Field == "UserName");
    }

    [Fact]
    public async Task SetUserActiveStateAsyncUpdatesNonSuperAdminUser()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.AddRange(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "member",
                    DisplayName = "Member",
                    PasswordHash = hasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.SetUserActiveStateAsync(
            new AdminUserSetActiveStateRequest(2, false),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.False(result.User!.IsActive);

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var user = await verificationContext.AppUsers.SingleAsync(existingUser => existingUser.Id == 2);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task SetUserActiveStateAsyncRejectsSuperAdminTarget()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.SetUserActiveStateAsync(
            new AdminUserSetActiveStateRequest(1, false),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!, error => error.Field == "UserId");

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var superAdmin = await verificationContext.AppUsers.SingleAsync(existingUser => existingUser.Id == 1);
        Assert.True(superAdmin.IsActive);
    }

    [Fact]
    public async Task SoftDeleteUserAsyncMarksUserAsDeletedAndInactive()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.AddRange(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "member",
                    DisplayName = "Member",
                    PasswordHash = hasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.SoftDeleteUserAsync(
            new AdminUserSoftDeleteRequest(2),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.UserId);
        Assert.Equal("member", result.UserName);

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var user = await verificationContext.AppUsers.SingleAsync(existingUser => existingUser.Id == 2);
        Assert.True(user.IsDeleted);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAtUtc);
    }

    [Fact]
    public async Task SoftDeleteUserAsyncRejectsSuperAdminTarget()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.SoftDeleteUserAsync(
            new AdminUserSoftDeleteRequest(1),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!, error => error.Field == "UserId");

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var superAdmin = await verificationContext.AppUsers.SingleAsync(existingUser => existingUser.Id == 1);
        Assert.False(superAdmin.IsDeleted);
    }

    [Fact]
    public async Task UpdateUserProfileAsyncUpdatesDisplayNameAndNormalizesExpiryToUtc()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.AddRange(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "member",
                    DisplayName = "Old Name",
                    PasswordHash = hasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    ExpiresAtUtc = null,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        var localExpiry = new DateTimeOffset(2030, 1, 2, 10, 30, 0, TimeSpan.FromHours(3));
        var expectedUtcExpiry = localExpiry.ToUniversalTime();

        var service = CreateService(options, currentUserId: 1);
        var result = await service.UpdateUserProfileAsync(
            new AdminUserUpdateProfileRequest(2, " Updated Member ", localExpiry),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("Updated Member", result.User!.DisplayName);
        Assert.Equal(expectedUtcExpiry, result.User.ExpiresAtUtc);

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var user = await verificationContext.AppUsers.SingleAsync(existingUser => existingUser.Id == 2);
        Assert.Equal("Updated Member", user.DisplayName);
        Assert.Equal(expectedUtcExpiry, user.ExpiresAtUtc);
    }

    [Fact]
    public async Task UpdateUserProfileAsyncRejectsSuperAdminTarget()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    ExpiresAtUtc = null,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.UpdateUserProfileAsync(
            new AdminUserUpdateProfileRequest(
                1,
                "Changed Admin",
                DateTimeOffset.UtcNow.AddDays(30)),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!, error => error.Field == "UserId");

        await using var verificationContext = new LinkedInJobScraperDbContext(options);
        var superAdmin = await verificationContext.AppUsers.SingleAsync(existingUser => existingUser.Id == 1);
        Assert.Equal("Super Admin", superAdmin.DisplayName);
    }

    [Fact]
    public async Task CreateUserAsyncReturnsConflictWhenUserNameAlreadyExistsIgnoringCase()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.AddRange(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "ExistingUser",
                    DisplayName = "Existing User",
                    PasswordHash = hasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.CreateUserAsync(
            new AdminUserCreateRequest(
                "existinguser",
                "Another User",
                "Passw0rd!",
                true,
                null),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.IsConflict);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors!, error => error.Field == "UserName");
    }

    [Fact]
    public async Task CreateUserAsyncReturnsValidationErrorsForInvalidInput()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.Add(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = new AppUserPasswordHasher().HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 1);
        var result = await service.CreateUserAsync(
            new AdminUserCreateRequest(
                "   ",
                "   ",
                "",
                true,
                DateTimeOffset.UtcNow.AddMinutes(-1)),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.IsConflict);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors!.Count >= 3);
    }

    [Fact]
    public async Task CreateUserAsyncThrowsWhenCurrentUserIsNotSuperAdmin()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var hasher = new AppUserPasswordHasher();

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            seedContext.AppUsers.AddRange(
                new AppUserRecord
                {
                    Id = 1,
                    UserName = "admin@mahdiahmadi.dev",
                    DisplayName = "Super Admin",
                    PasswordHash = hasher.HashPassword("AdminPass!"),
                    IsActive = true,
                    IsSeeded = true,
                    IsSuperAdmin = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new AppUserRecord
                {
                    Id = 2,
                    UserName = "member",
                    DisplayName = "Member",
                    PasswordHash = hasher.HashPassword("Passw0rd!"),
                    IsActive = true,
                    IsSeeded = false,
                    IsSuperAdmin = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(options, currentUserId: 2);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateUserAsync(
                new AdminUserCreateRequest(
                    "new-user",
                    "New User",
                    "Passw0rd!",
                    true,
                    null),
                CancellationToken.None));

        Assert.Contains("Super-admin access is required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminUserManagementService CreateService(
        DbContextOptions<LinkedInJobScraperDbContext> options,
        int currentUserId)
    {
        return new AdminUserManagementService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext(currentUserId),
            new AppUserPasswordHasher());
    }
}
