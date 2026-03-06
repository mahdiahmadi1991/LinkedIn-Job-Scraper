using System.ComponentModel.DataAnnotations;

namespace LinkedIn.JobScraper.Web.Models;

public sealed class AiSettingsPageViewModel
{
    public string? ConcurrencyToken { get; set; }

    [Required]
    [Display(Name = "Behavioral Instructions")]
    public string BehavioralInstructions { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Priority Signals")]
    public string PrioritySignals { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Exclusion Signals")]
    public string ExclusionSignals { get; set; } = string.Empty;

    [Required]
    [Display(Name = "AI Output Language")]
    public string OutputLanguageCode { get; set; } = "en";

    public string? StatusMessage { get; set; }

    public bool StatusSucceeded { get; set; }

    public bool OpenAiApiKeyConfigured { get; set; }

    public bool OpenAiConnectionReady { get; set; }

    public string OpenAiConnectionStatusMessage { get; set; } = string.Empty;

    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";

    public string? OpenAiModel { get; set; }
}
