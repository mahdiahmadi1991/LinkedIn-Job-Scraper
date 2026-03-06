using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class AiBehaviorSettingsService : IAiBehaviorSettingsService
{
    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;

    public AiBehaviorSettingsService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ICurrentAppUserContext currentAppUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _currentAppUserContext = currentAppUserContext;
    }

    public async Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await GetOrCreateActiveRecordAsync(dbContext, userId, cancellationToken);

        return Map(record);
    }

    public async Task<AiBehaviorProfile> SaveAsync(AiBehaviorProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var userId = _currentAppUserContext.GetRequiredUserId();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await GetOrCreateActiveRecordAsync(dbContext, userId, cancellationToken);
        var entry = dbContext.Entry(record);
        var originalRowVersion = ConcurrencyTokenCodec.Decode(profile.ConcurrencyToken);

        if (originalRowVersion is not null)
        {
            entry.Property(static settings => settings.RowVersion).OriginalValue = originalRowVersion;
        }

        record.BehavioralInstructions = profile.BehavioralInstructions.Trim();
        record.PrioritySignals = profile.PrioritySignals.Trim();
        record.ExclusionSignals = profile.ExclusionSignals.Trim();
        record.OutputLanguageCode = AiOutputLanguage.Normalize(profile.OutputLanguageCode);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "AI behavior settings were updated by another operation. Reload the page and try again.",
                exception);
        }

        return Map(record);
    }

    private static async Task<AiBehaviorSettingsRecord> GetOrCreateActiveRecordAsync(
        LinkedInJobScraperDbContext dbContext,
        int userId,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.AiBehaviorSettings
            .Where(settings => settings.AppUserId == userId)
            .OrderBy(static settings => settings.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            record = new AiBehaviorSettingsRecord
            {
                AppUserId = userId,
                BehavioralInstructions = string.Empty,
                PrioritySignals = string.Empty,
                ExclusionSignals = string.Empty,
                OutputLanguageCode = AiOutputLanguage.English,
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
            record.BehavioralInstructions,
            record.PrioritySignals,
            record.ExclusionSignals,
            AiOutputLanguage.Normalize(record.OutputLanguageCode),
            ConcurrencyTokenCodec.Encode(record.RowVersion));
    }
}
