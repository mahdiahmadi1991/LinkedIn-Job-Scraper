using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public interface ILinkedInSearchSettingsService
{
    Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken);

    Task<LinkedInSearchSettings> SaveAsync(LinkedInSearchSettings settings, CancellationToken cancellationToken);
}

public sealed class LinkedInSearchSettingsService : ILinkedInSearchSettingsService, IDisposable
{
    private static readonly string[] DefaultWorkplaceTypeCodes = ["1", "2", "3"];
    private static readonly string[] DefaultJobTypeCodes = ["F", "P", "C", "T", "I", "O"];

    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _databaseEnsured;

    public LinkedInSearchSettingsService(IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(cancellationToken);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await GetOrCreateActiveRecordAsync(dbContext, cancellationToken);
        return Map(record);
    }

    public async Task<LinkedInSearchSettings> SaveAsync(
        LinkedInSearchSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await EnsureDatabaseAsync(cancellationToken);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await GetOrCreateActiveRecordAsync(dbContext, cancellationToken);
        var entry = dbContext.Entry(record);
        var originalRowVersion = ConcurrencyTokenCodec.Decode(settings.ConcurrencyToken);

        if (originalRowVersion is not null)
        {
            entry.Property(static current => current.RowVersion).OriginalValue = originalRowVersion;
        }

        record.ProfileName = string.IsNullOrWhiteSpace(settings.ProfileName) ? "Default" : settings.ProfileName.Trim();
        record.Keywords = settings.Keywords.Trim();
        record.LocationInput = NormalizeNullable(settings.LocationInput);
        record.LocationDisplayName = NormalizeNullable(settings.LocationDisplayName);
        record.LocationGeoId = NormalizeNullable(settings.LocationGeoId);
        record.EasyApply = settings.EasyApply;
        record.WorkplaceTypeCodesCsv = ToCsv(settings.WorkplaceTypeCodes, DefaultWorkplaceTypeCodes);
        record.JobTypeCodesCsv = ToCsv(settings.JobTypeCodes, DefaultJobTypeCodes);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "LinkedIn search settings were updated by another operation. Reload the page and try again.",
                exception);
        }

        return Map(record);
    }

    private static async Task<LinkedInSearchSettingsRecord> GetOrCreateActiveRecordAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.LinkedInSearchSettings
            .OrderBy(static settings => settings.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            record = new LinkedInSearchSettingsRecord
            {
                ProfileName = "Default",
                Keywords = "C# .Net",
                LocationInput = "Limassol, Cyprus",
                LocationDisplayName = "Limassol, Cyprus",
                LocationGeoId = "106394980",
                EasyApply = true,
                WorkplaceTypeCodesCsv = ToCsv(DefaultWorkplaceTypeCodes, DefaultWorkplaceTypeCodes),
                JobTypeCodesCsv = ToCsv(DefaultJobTypeCodes, DefaultJobTypeCodes),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            dbContext.LinkedInSearchSettings.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return record;
    }

    private static LinkedInSearchSettings Map(LinkedInSearchSettingsRecord record)
    {
        return new LinkedInSearchSettings(
            record.ProfileName,
            record.Keywords,
            record.LocationInput,
            record.LocationDisplayName,
            record.LocationGeoId,
            record.EasyApply,
            SplitCsv(record.WorkplaceTypeCodesCsv, DefaultWorkplaceTypeCodes),
            SplitCsv(record.JobTypeCodesCsv, DefaultJobTypeCodes),
            ConcurrencyTokenCodec.Encode(record.RowVersion));
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ToCsv(IEnumerable<string> values, IReadOnlyCollection<string> fallback)
    {
        var normalized = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(',', normalized.Length == 0 ? fallback : normalized);
    }

    private static string[] SplitCsv(string csv, IReadOnlyCollection<string> fallback)
    {
        var values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? [.. fallback] : values;
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
            if (dbContext.Database.IsRelational())
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

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

public sealed record LinkedInSearchSettings(
    string ProfileName,
    string Keywords,
    string? LocationInput,
    string? LocationDisplayName,
    string? LocationGeoId,
    bool EasyApply,
    IReadOnlyList<string> WorkplaceTypeCodes,
    IReadOnlyList<string> JobTypeCodes,
    string? ConcurrencyToken = null);
