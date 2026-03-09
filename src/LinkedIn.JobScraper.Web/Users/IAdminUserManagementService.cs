using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Users;

public interface IAdminUserManagementService
{
    Task<IReadOnlyList<AdminUserListItem>> GetUsersAsync(CancellationToken cancellationToken);

    Task<AdminUserCreateResult> CreateUserAsync(AdminUserCreateRequest request, CancellationToken cancellationToken);

    Task<AdminUserUpdateResult> UpdateUserProfileAsync(AdminUserUpdateProfileRequest request, CancellationToken cancellationToken);

    Task<AdminUserUpdateResult> SetUserActiveStateAsync(AdminUserSetActiveStateRequest request, CancellationToken cancellationToken);

    Task<AdminUserDeleteResult> SoftDeleteUserAsync(AdminUserSoftDeleteRequest request, CancellationToken cancellationToken);
}

public sealed class AdminUserManagementService : IAdminUserManagementService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IAppUserPasswordHasher _passwordHasher;

    public AdminUserManagementService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ICurrentAppUserContext currentAppUserContext,
        IAppUserPasswordHasher passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _currentAppUserContext = currentAppUserContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<AdminUserListItem>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsSuperAdminAsync(dbContext, cancellationToken);

        return await dbContext.AppUsers
            .AsNoTracking()
            .Where(static user => !user.IsDeleted)
            .OrderBy(static user => user.Id)
            .Select(
                static user => new AdminUserListItem(
                    user.Id,
                    user.UserName,
                    user.DisplayName,
                    user.IsActive,
                    user.IsSuperAdmin,
                    user.ExpiresAtUtc,
                    user.CreatedAtUtc,
                    user.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AdminUserCreateResult> CreateUserAsync(
        AdminUserCreateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
        {
            return new AdminUserCreateResult(
                false,
                "User creation request is invalid.",
                null,
                IsConflict: false,
                ValidationErrors: validationErrors);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsSuperAdminAsync(dbContext, cancellationToken);

        var normalizedUserName = request.UserName.Trim();
        if (string.Equals(
                normalizedUserName,
                AppUserSeedingStartupService.SuperAdminUserName,
                StringComparison.OrdinalIgnoreCase))
        {
            return new AdminUserCreateResult(
                false,
                "Reserved super-admin username cannot be used for user creation.",
                null,
                IsConflict: true,
                ValidationErrors:
                [
                    new AdminUserValidationError("UserName", "This username is reserved for the seeded super-admin account.")
                ]);
        }

        var normalizedDisplayName = request.DisplayName.Trim();
        var normalizedExpiry = request.ExpiresAtUtc?.ToUniversalTime();
        var existingUserNames = await dbContext.AppUsers
            .AsNoTracking()
            .Select(static user => user.UserName)
            .ToListAsync(cancellationToken);

        var duplicateExists = existingUserNames.Any(
            existingUserName => string.Equals(existingUserName, normalizedUserName, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            return new AdminUserCreateResult(
                false,
                "A user with this username already exists.",
                null,
                IsConflict: true,
                ValidationErrors:
                [
                    new AdminUserValidationError("UserName", "Username is already in use.")
                ]);
        }

        var utcNow = DateTimeOffset.UtcNow;
        var user = new AppUserRecord
        {
            UserName = normalizedUserName,
            DisplayName = normalizedDisplayName,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = request.IsActive,
            IsSeeded = false,
            IsSuperAdmin = false,
            IsDeleted = false,
            DeletedAtUtc = null,
            ExpiresAtUtc = normalizedExpiry,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminUserCreateResult(
            true,
            "User created successfully.",
            ToListItem(user));
    }

    public async Task<AdminUserUpdateResult> UpdateUserProfileAsync(
        AdminUserUpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = ValidateProfileUpdateRequest(request);
        if (validationErrors.Count > 0)
        {
            return new AdminUserUpdateResult(
                false,
                "User update request is invalid.",
                null,
                validationErrors);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsSuperAdminAsync(dbContext, cancellationToken);

        var user = await dbContext.AppUsers
            .SingleOrDefaultAsync(existingUser => existingUser.Id == request.UserId, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            return new AdminUserUpdateResult(false, "User was not found.", null);
        }

        if (user.IsSuperAdmin)
        {
            return new AdminUserUpdateResult(
                false,
                "Super-admin user cannot be modified.",
                null,
                [new AdminUserValidationError("UserId", "Super-admin user cannot be modified.")]);
        }

        var normalizedDisplayName = request.DisplayName.Trim();
        var normalizedExpiry = request.ExpiresAtUtc?.ToUniversalTime();
        var hasChanges = false;

        if (!string.Equals(user.DisplayName, normalizedDisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = normalizedDisplayName;
            hasChanges = true;
        }

        if (user.ExpiresAtUtc != normalizedExpiry)
        {
            user.ExpiresAtUtc = normalizedExpiry;
            hasChanges = true;
        }

        if (hasChanges)
        {
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AdminUserUpdateResult(
            true,
            hasChanges ? "User profile updated successfully." : "No user profile changes were required.",
            ToListItem(user));
    }

    public async Task<AdminUserUpdateResult> SetUserActiveStateAsync(
        AdminUserSetActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = ValidateSetActiveStateRequest(request);
        if (validationErrors.Count > 0)
        {
            return new AdminUserUpdateResult(
                false,
                "User activation request is invalid.",
                null,
                validationErrors);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsSuperAdminAsync(dbContext, cancellationToken);

        var user = await dbContext.AppUsers
            .SingleOrDefaultAsync(existingUser => existingUser.Id == request.UserId, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            return new AdminUserUpdateResult(false, "User was not found.", null);
        }

        if (user.IsSuperAdmin)
        {
            return new AdminUserUpdateResult(
                false,
                "Super-admin user cannot be modified.",
                null,
                [new AdminUserValidationError("UserId", "Super-admin user cannot be modified.")]);
        }

        var hasChanges = user.IsActive != request.IsActive;
        if (hasChanges)
        {
            user.IsActive = request.IsActive;
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AdminUserUpdateResult(
            true,
            hasChanges ? "User activation state updated successfully." : "User activation state was already set.",
            ToListItem(user));
    }

    public async Task<AdminUserDeleteResult> SoftDeleteUserAsync(
        AdminUserSoftDeleteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = ValidateSoftDeleteRequest(request);
        if (validationErrors.Count > 0)
        {
            return new AdminUserDeleteResult(
                false,
                "User deletion request is invalid.",
                null,
                null,
                validationErrors);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsSuperAdminAsync(dbContext, cancellationToken);

        var user = await dbContext.AppUsers
            .SingleOrDefaultAsync(existingUser => existingUser.Id == request.UserId, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            return new AdminUserDeleteResult(false, "User was not found.", null, null);
        }

        if (user.IsSuperAdmin)
        {
            return new AdminUserDeleteResult(
                false,
                "Super-admin user cannot be modified.",
                null,
                null,
                [new AdminUserValidationError("UserId", "Super-admin user cannot be modified.")]);
        }

        var utcNow = DateTimeOffset.UtcNow;
        user.IsDeleted = true;
        user.IsActive = false;
        user.DeletedAtUtc = utcNow;
        user.UpdatedAtUtc = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminUserDeleteResult(
            true,
            "User soft-deleted successfully.",
            user.Id,
            user.UserName);
    }

    private async Task EnsureCurrentUserIsSuperAdminAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentAppUserContext.GetRequiredUserId();
        var isSuperAdmin = await dbContext.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == currentUserId && !user.IsDeleted)
            .Select(static user => user.IsSuperAdmin)
            .FirstOrDefaultAsync(cancellationToken);

        if (!isSuperAdmin)
        {
            throw new InvalidOperationException("Super-admin access is required for user management operations.");
        }
    }

    private static List<AdminUserValidationError> ValidateCreateRequest(AdminUserCreateRequest request)
    {
        var errors = new List<AdminUserValidationError>();

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            errors.Add(new AdminUserValidationError("UserName", "Username is required."));
        }
        else if (request.UserName.Trim().Length > 128)
        {
            errors.Add(new AdminUserValidationError("UserName", "Username must be 128 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add(new AdminUserValidationError("DisplayName", "Display name is required."));
        }
        else if (request.DisplayName.Trim().Length > 256)
        {
            errors.Add(new AdminUserValidationError("DisplayName", "Display name must be 256 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add(new AdminUserValidationError("Password", "Password is required."));
        }

        if (request.ExpiresAtUtc is DateTimeOffset expiresAtUtc &&
            expiresAtUtc.ToUniversalTime() <= DateTimeOffset.UtcNow)
        {
            errors.Add(new AdminUserValidationError("ExpiresAtUtc", "Expiry time must be in the future."));
        }

        return errors;
    }

    private static List<AdminUserValidationError> ValidateProfileUpdateRequest(AdminUserUpdateProfileRequest request)
    {
        var errors = new List<AdminUserValidationError>();

        if (request.UserId <= 0)
        {
            errors.Add(new AdminUserValidationError("UserId", "User id must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add(new AdminUserValidationError("DisplayName", "Display name is required."));
        }
        else if (request.DisplayName.Trim().Length > 256)
        {
            errors.Add(new AdminUserValidationError("DisplayName", "Display name must be 256 characters or fewer."));
        }

        if (request.ExpiresAtUtc is DateTimeOffset expiresAtUtc &&
            expiresAtUtc.ToUniversalTime() <= DateTimeOffset.UtcNow)
        {
            errors.Add(new AdminUserValidationError("ExpiresAtUtc", "Expiry time must be in the future."));
        }

        return errors;
    }

    private static List<AdminUserValidationError> ValidateSetActiveStateRequest(AdminUserSetActiveStateRequest request)
    {
        var errors = new List<AdminUserValidationError>();

        if (request.UserId <= 0)
        {
            errors.Add(new AdminUserValidationError("UserId", "User id must be greater than zero."));
        }

        return errors;
    }

    private static List<AdminUserValidationError> ValidateSoftDeleteRequest(AdminUserSoftDeleteRequest request)
    {
        var errors = new List<AdminUserValidationError>();

        if (request.UserId <= 0)
        {
            errors.Add(new AdminUserValidationError("UserId", "User id must be greater than zero."));
        }

        return errors;
    }

    private static AdminUserListItem ToListItem(AppUserRecord user)
    {
        return new AdminUserListItem(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.IsActive,
            user.IsSuperAdmin,
            user.ExpiresAtUtc,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }
}

public sealed record AdminUserCreateRequest(
    string UserName,
    string DisplayName,
    string Password,
    bool IsActive,
    DateTimeOffset? ExpiresAtUtc);

public sealed record AdminUserValidationError(
    string Field,
    string Message);

public sealed record AdminUserListItem(
    int Id,
    string UserName,
    string DisplayName,
    bool IsActive,
    bool IsSuperAdmin,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminUserUpdateProfileRequest(
    int UserId,
    string DisplayName,
    DateTimeOffset? ExpiresAtUtc);

public sealed record AdminUserSetActiveStateRequest(
    int UserId,
    bool IsActive);

public sealed record AdminUserSoftDeleteRequest(
    int UserId);

public sealed record AdminUserCreateResult(
    bool Success,
    string Message,
    AdminUserListItem? User,
    bool IsConflict = false,
    IReadOnlyList<AdminUserValidationError>? ValidationErrors = null)
    : OperationResult(Success, Message);

public sealed record AdminUserUpdateResult(
    bool Success,
    string Message,
    AdminUserListItem? User,
    IReadOnlyList<AdminUserValidationError>? ValidationErrors = null)
    : OperationResult(Success, Message);

public sealed record AdminUserDeleteResult(
    bool Success,
    string Message,
    int? UserId,
    string? UserName,
    IReadOnlyList<AdminUserValidationError>? ValidationErrors = null)
    : OperationResult(Success, Message);
