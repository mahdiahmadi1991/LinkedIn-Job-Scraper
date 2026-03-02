using System.Text.Json;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class DatabaseLinkedInSessionStore : ILinkedInSessionStore
{
    private const string PrimarySessionKey = "primary";

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;

    public DatabaseLinkedInSessionStore(IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        var record = await dbContext.LinkedInSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                static session => session.SessionKey == PrimarySessionKey && session.IsActive,
                cancellationToken);

        if (record is null)
        {
            return null;
        }

        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(record.RequestHeadersJson) ??
                      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new LinkedInSessionSnapshot(headers, record.CapturedAtUtc, record.Source);
    }

    public async Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        var existingRecord = await dbContext.LinkedInSessions.SingleOrDefaultAsync(
            static session => session.SessionKey == PrimarySessionKey,
            cancellationToken);

        if (existingRecord is null)
        {
            existingRecord = new LinkedInSessionRecord
            {
                SessionKey = PrimarySessionKey
            };

            dbContext.LinkedInSessions.Add(existingRecord);
        }

        existingRecord.RequestHeadersJson = JsonSerializer.Serialize(sessionSnapshot.Headers);
        existingRecord.CapturedAtUtc = sessionSnapshot.CapturedAtUtc;
        existingRecord.Source = sessionSnapshot.Source;
        existingRecord.IsActive = true;
        existingRecord.LastValidatedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
