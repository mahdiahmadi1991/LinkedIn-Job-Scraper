using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class AiBehaviorSettingsService : IAiBehaviorSettingsService
{
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;

    public AiBehaviorSettingsService(IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await GetOrCreateActiveRecordAsync(dbContext, cancellationToken);

        return Map(record);
    }

    public async Task<AiBehaviorProfile> SaveAsync(AiBehaviorProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await GetOrCreateActiveRecordAsync(dbContext, cancellationToken);
        record.ProfileName = profile.ProfileName.Trim();
        record.BehavioralInstructions = profile.BehavioralInstructions.Trim();
        record.PrioritySignals = profile.PrioritySignals.Trim();
        record.ExclusionSignals = profile.ExclusionSignals.Trim();
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(record);
    }

    private static async Task<AiBehaviorSettingsRecord> GetOrCreateActiveRecordAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.AiBehaviorSettings
            .OrderBy(static settings => settings.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            record = new AiBehaviorSettingsRecord
            {
                ProfileName = "Default",
                BehavioralInstructions =
                    "Prefer practical fit for the user's current C# and .NET career path. Favor roles that are realistically actionable now.",
                PrioritySignals =
                    "C#, .NET, ASP.NET Core, backend engineering, clear scope, remote-friendly roles, direct apply path, concrete requirements.",
                ExclusionSignals =
                    "Strong stack mismatch, unclear responsibilities, vague descriptions, low evidence of fit, or roles far outside established experience.",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            dbContext.AiBehaviorSettings.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return record;
    }

    private static AiBehaviorProfile Map(AiBehaviorSettingsRecord record)
    {
        return new AiBehaviorProfile(
            record.ProfileName,
            record.BehavioralInstructions,
            record.PrioritySignals,
            record.ExclusionSignals);
    }
}
