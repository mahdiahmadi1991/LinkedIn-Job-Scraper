using System.ComponentModel.DataAnnotations;

namespace LinkedIn.JobScraper.Web.Models;

public sealed class AdminUsersPageViewModel
{
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
