using System.ComponentModel.DataAnnotations;

namespace LinkedIn.JobScraper.Web.Models;

public sealed class LinkedInSearchSettingsPageViewModel
{
    public string? ConcurrencyToken { get; set; }

    [Required]
    [Display(Name = "Search By Title, Skill, Or Company")]
    public string Keywords { get; set; } = string.Empty;

    [Display(Name = "City, State, Or Zip Code")]
    public string? LocationInput { get; set; }

    public string? LocationDisplayName { get; set; }

    public string? LocationGeoId { get; set; }

    [Display(Name = "Easy Apply Only")]
    public bool EasyApply { get; set; }

    public List<string> WorkplaceTypeCodes { get; set; } = [];

    public List<string> JobTypeCodes { get; set; } = [];

    public List<LinkedInLocationSuggestionViewModel> LocationSuggestions { get; set; } = [];

    public string? StatusMessage { get; set; }

    public bool StatusSucceeded { get; set; }

    public static IReadOnlyList<SearchFilterOptionViewModel> WorkplaceTypeOptions { get; } =
    [
        new("1", "On-site"),
        new("3", "Hybrid"),
        new("2", "Remote")
    ];

    public static IReadOnlyList<SearchFilterOptionViewModel> JobTypeOptions { get; } =
    [
        new("F", "Full-time"),
        new("P", "Part-time"),
        new("C", "Contract"),
        new("T", "Temporary"),
        new("I", "Internship"),
        new("O", "Volunteer / Other")
    ];
}

public sealed record SearchFilterOptionViewModel(
    string Code,
    string Label);

public sealed record LinkedInLocationSuggestionViewModel(
    string GeoId,
    string DisplayName);
