using LinkedIn.JobScraper.Web.AI;
using System.ComponentModel.DataAnnotations;

namespace LinkedIn.JobScraper.Web.Models;

public sealed class AdminUsersPageViewModel
{
    public string ActiveTab { get; set; } = "users";

    public AdminOpenAiSetupFormViewModel OpenAiSetupForm { get; set; } = new();

    public bool OpenAiApiKeyConfigured { get; set; }

    public bool OpenAiConnectionReady { get; set; }

    public string OpenAiConnectionStatusMessage { get; set; } = string.Empty;

    public string? OpenAiStatusMessage { get; set; }

    public bool OpenAiStatusSucceeded { get; set; }

    public AdminUserCreateFormViewModel CreateForm { get; set; } = new();

    public AdminUserUpdateFormViewModel UpdateForm { get; set; } = new();

    public AdminUserSetActiveStateFormViewModel ToggleActiveForm { get; set; } = new();

    public AdminUserSoftDeleteFormViewModel SoftDeleteForm { get; set; } = new();

    public IReadOnlyList<AdminUserListItemViewModel> Users { get; set; } = [];

    public string? StatusMessage { get; set; }

    public bool StatusSucceeded { get; set; }
}

public sealed class AdminUserCreateFormViewModel
{
    [Required]
    [StringLength(128)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Password")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Expires At")]
    [DataType(DataType.DateTime)]
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

public sealed class AdminUserUpdateFormViewModel
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [StringLength(256)]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "Expires At")]
    [DataType(DataType.DateTime)]
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

public sealed class AdminUserSetActiveStateFormViewModel
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; }
}

public sealed class AdminUserSoftDeleteFormViewModel
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
}

public sealed record AdminUserListItemViewModel(
    int Id,
    string UserName,
    string DisplayName,
    bool IsActive,
    bool IsSuperAdmin,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed class AdminOpenAiSetupFormViewModel : IValidatableObject
{
    public string? ConcurrencyToken { get; set; }

    [Required]
    [StringLength(512)]
    [Display(Name = "API Key")]
    [DataType(DataType.Password)]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    [Display(Name = "Model")]
    public string Model { get; set; } = string.Empty;

    [Required]
    [StringLength(512)]
    [Display(Name = "Base URL")]
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    [Range(1, int.MaxValue)]
    [Display(Name = "Request Timeout (seconds)")]
    public int RequestTimeoutSeconds { get; set; } = 45;

    [Display(Name = "Use Background Mode")]
    public bool UseBackgroundMode { get; set; } = true;

    [Range(1, int.MaxValue)]
    [Display(Name = "Background Polling Interval (milliseconds)")]
    public int BackgroundPollingIntervalMilliseconds { get; set; } = 1500;

    [Range(1, int.MaxValue)]
    [Display(Name = "Background Polling Timeout (seconds)")]
    public int BackgroundPollingTimeoutSeconds { get; set; } = 120;

    [Range(1, int.MaxValue)]
    [Display(Name = "Max Concurrent Scoring Requests")]
    public int MaxConcurrentScoringRequests { get; set; } = 2;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!OpenAiModelCatalog.IsSupported(Model))
        {
            yield return new ValidationResult(
                "Selected model is not supported for this setup profile.",
                [nameof(Model)]);
        }
    }
}
