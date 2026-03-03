namespace LinkedIn.JobScraper.Web.AI;

public interface IAiBehaviorSettingsService
{
    Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken);

    Task<AiBehaviorProfile> SaveAsync(AiBehaviorProfile profile, CancellationToken cancellationToken);
}

public sealed record AiBehaviorProfile(
    string ProfileName,
    string BehavioralInstructions,
    string PrioritySignals,
    string ExclusionSignals,
    string OutputLanguageCode,
    string? ConcurrencyToken = null)
{
    public string OutputDirection => AiOutputLanguage.GetDirection(OutputLanguageCode);
}
