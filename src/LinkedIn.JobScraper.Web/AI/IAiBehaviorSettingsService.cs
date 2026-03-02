namespace LinkedIn.JobScraper.Web.AI;

public interface IAiBehaviorSettingsService
{
    Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken);
}

public sealed record AiBehaviorProfile(
    string ProfileName,
    string BehavioralInstructions,
    string PrioritySignals,
    string ExclusionSignals);
