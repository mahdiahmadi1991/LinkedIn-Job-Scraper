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

        var validationErrors = ValidateRequest(request);
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
            ExpiresAtUtc = normalizedExpiry,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminUserCreateResult(
            true,
            "User created successfully.",
            new AdminUserListItem(
                user.Id,
                user.UserName,
                user.DisplayName,
                user.IsActive,
                user.IsSuperAdmin,
                user.ExpiresAtUtc,
                user.CreatedAtUtc,
                user.UpdatedAtUtc));
    }

    private async Task EnsureCurrentUserIsSuperAdminAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentAppUserContext.GetRequiredUserId();
        var isSuperAdmin = await dbContext.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == currentUserId)
            .Select(static user => user.IsSuperAdmin)
            .FirstOrDefaultAsync(cancellationToken);

        if (!isSuperAdmin)
        {
            throw new InvalidOperationException("Super-admin access is required for user management operations.");
        }
    }

    private static List<AdminUserValidationError> ValidateRequest(AdminUserCreateRequest request)
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

public sealed record AdminUserCreateResult(
    bool Success,
    string Message,
    AdminUserListItem? User,
    bool IsConflict = false,
    IReadOnlyList<AdminUserValidationError>? ValidationErrors = null)
    : OperationResult(Success, Message);
