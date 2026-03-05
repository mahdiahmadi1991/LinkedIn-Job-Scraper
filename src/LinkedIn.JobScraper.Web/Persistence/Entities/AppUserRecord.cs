namespace LinkedIn.JobScraper.Web.Persistence.Entities;

/// <summary>
/// Local application user account used for cookie-based app authentication.
/// </summary>
public sealed class AppUserRecord
{
    /// <summary>
    /// Internal surrogate primary key for the user row.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique login identifier entered by the user at sign-in.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Friendly display name shown in the UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Password hash payload (algorithm + parameters + hash material),
    /// never the raw password.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the account can currently authenticate.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True when the account originated from configuration-based seeding.
    /// </summary>
    public bool IsSeeded { get; set; }

    /// <summary>
    /// Optional UTC expiration instant for temporary access accounts.
    /// Null means no expiry limit.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the account was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent account update.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
