using System.Text.Json;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class DatabaseLinkedInSessionStore : ILinkedInSessionStore, IDisposable
{
    private const string PrimarySessionKey = "primary";

    private readonly ICurrentAppUserContext _currentAppUserContext;
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _databaseEnsured;

    public DatabaseLinkedInSessionStore(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        ICurrentAppUserContext currentAppUserContext)
    {
        _dbContextFactory = dbContextFactory;
        _currentAppUserContext = currentAppUserContext;
    }

    public async Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var record = await dbContext.LinkedInSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                session => session.AppUserId == userId && session.SessionKey == PrimarySessionKey && session.IsActive,
                cancellationToken);

        if (record is null)
        {
            return null;
        }

        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(record.RequestHeadersJson) ??
                      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new LinkedInSessionSnapshot(
            headers,
            record.CapturedAtUtc,
            record.Source,
            record.EstimatedExpiresAtUtc,
            record.ExpirySource);
    }

    public async Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sanitizedHeaders = LinkedInSessionHeaderSanitizer.SanitizeForStorage(sessionSnapshot.Headers);

        var existingRecord = await dbContext.LinkedInSessions.SingleOrDefaultAsync(
            session => session.AppUserId == userId && session.SessionKey == PrimarySessionKey,
            cancellationToken);

        if (existingRecord is null)
        {
            existingRecord = new LinkedInSessionRecord
            {
                AppUserId = userId,
                SessionKey = PrimarySessionKey
            };

            dbContext.LinkedInSessions.Add(existingRecord);
        }

        existingRecord.RequestHeadersJson = JsonSerializer.Serialize(sanitizedHeaders);
        existingRecord.CapturedAtUtc = sessionSnapshot.CapturedAtUtc;
        existingRecord.Source = sessionSnapshot.Source;
        existingRecord.EstimatedExpiresAtUtc = sessionSnapshot.EstimatedExpiresAtUtc;
        existingRecord.ExpirySource = sessionSnapshot.ExpirySource;
        existingRecord.IsActive = true;
        existingRecord.LastValidatedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateCurrentAsync(CancellationToken cancellationToken)
    {
        var userId = _currentAppUserContext.GetRequiredUserId();
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingRecord = await dbContext.LinkedInSessions.SingleOrDefaultAsync(
            session => session.AppUserId == userId && session.SessionKey == PrimarySessionKey && session.IsActive,
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
        var userId = _currentAppUserContext.GetRequiredUserId();
        await EnsureDatabaseAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingRecord = await dbContext.LinkedInSessions.SingleOrDefaultAsync(
            session => session.AppUserId == userId && session.SessionKey == PrimarySessionKey && session.IsActive,
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
