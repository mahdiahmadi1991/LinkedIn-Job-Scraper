namespace LinkedIn.JobScraper.Web.Persistence.Entities;

public sealed class AiBehaviorSettingsRecord
{
    public int Id { get; set; }

    public string ProfileName { get; set; } = "Default";

    public string BehavioralInstructions { get; set; } = string.Empty;

    public string PrioritySignals { get; set; } = string.Empty;

    public string ExclusionSignals { get; set; } = string.Empty;

    public string OutputLanguageCode { get; set; } = "en";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
