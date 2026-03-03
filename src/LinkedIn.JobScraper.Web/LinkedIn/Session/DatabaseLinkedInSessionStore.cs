using System.Text.Json;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class DatabaseLinkedInSessionStore : ILinkedInSessionStore, IDisposable
{
    private const string PrimarySessionKey = "primary";

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _databaseEnsured;

    public DatabaseLinkedInSessionStore(IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sanitizedHeaders = LinkedInSessionHeaderSanitizer.SanitizeForStorage(sessionSnapshot.Headers);

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

        existingRecord.RequestHeadersJson = JsonSerializer.Serialize(sanitizedHeaders);
        existingRecord.CapturedAtUtc = sessionSnapshot.CapturedAtUtc;
        existingRecord.Source = sessionSnapshot.Source;
        existingRecord.IsActive = true;
        existingRecord.LastValidatedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateCurrentAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingRecord = await dbContext.LinkedInSessions.SingleOrDefaultAsync(
            static session => session.SessionKey == PrimarySessionKey && session.IsActive,
            cancellationToken);

        if (existingRecord is null)
        {
            return;
        }

        existingRecord.IsActive = false;
        existingRecord.LastValidatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCurrentValidatedAsync(DateTimeOffset validatedAtUtc, CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingRecord = await dbContext.LinkedInSessions.SingleOrDefaultAsync(
            static session => session.SessionKey == PrimarySessionKey && session.IsActive,
            cancellationToken);

        if (existingRecord is null)
        {
            return;
        }

        existingRecord.LastValidatedAtUtc = validatedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_databaseEnsured)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);

        try
        {
            if (_databaseEnsured)
            {
                return;
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
            _databaseEnsured = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public void Dispose()
    {
        _initializationGate.Dispose();
    }
}
