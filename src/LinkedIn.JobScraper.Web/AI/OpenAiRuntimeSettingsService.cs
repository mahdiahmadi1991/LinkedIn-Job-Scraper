using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiRuntimeSettingsService : IOpenAiRuntimeSettingsService
{
    private const string GlobalSettingsKey = "global";
    private static readonly Uri DefaultOpenAiBaseUrl = new("https://api.openai.com/v1", UriKind.Absolute);
    private readonly IDbContextFactory<LinkedInJobScraperDbContext> _dbContextFactory;
    private readonly IOptionsMonitor<OpenAiSecurityOptions> _openAiSecurityOptions;

    public OpenAiRuntimeSettingsService(
        IDbContextFactory<LinkedInJobScraperDbContext> dbContextFactory,
        IOptionsMonitor<OpenAiSecurityOptions> openAiSecurityOptions)
    {
        _dbContextFactory = dbContextFactory;
        _openAiSecurityOptions = openAiSecurityOptions;
    }

    public async Task<OpenAiRuntimeSettingsProfile> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await GetOrCreateActiveRecordAsync(dbContext, cancellationToken);
        return Map(record);
    }

    public async Task<OpenAiRuntimeSettingsProfile> SaveAsync(
        OpenAiRuntimeSettingsProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var validationFailure = OpenAiRuntimeSettingsProfileValidator.Validate(profile);
        if (validationFailure is not null)
        {
            throw new InvalidOperationException(validationFailure);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await GetOrCreateActiveRecordAsync(dbContext, cancellationToken);
        var entry = dbContext.Entry(record);
        var originalRowVersion = ConcurrencyTokenCodec.Decode(profile.ConcurrencyToken);

        if (originalRowVersion is not null)
        {
            entry.Property(static settings => settings.RowVersion).OriginalValue = originalRowVersion;
        }

        record.Model = profile.Model.Trim();
        record.BaseUrl = NormalizeBaseUrl(profile.BaseUrl);
        record.RequestTimeoutSeconds = profile.RequestTimeoutSeconds;
        record.UseBackgroundMode = profile.UseBackgroundMode;
        record.BackgroundPollingIntervalMilliseconds = profile.BackgroundPollingIntervalMilliseconds;
        record.BackgroundPollingTimeoutSeconds = profile.BackgroundPollingTimeoutSeconds;
        record.MaxConcurrentScoringRequests = profile.MaxConcurrentScoringRequests;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "OpenAI runtime settings were updated by another operation. Reload the page and try again.",
                exception);
        }

        return Map(record);
    }

    private async Task<OpenAiRuntimeSettingsRecord> GetOrCreateActiveRecordAsync(
        LinkedInJobScraperDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.OpenAiRuntimeSettings
            .Where(settings => settings.SettingsKey == GlobalSettingsKey)
            .OrderBy(static settings => settings.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is not null)
        {
            return record;
        }

        var openAiSecurityOptions = _openAiSecurityOptions.CurrentValue;

        record = new OpenAiRuntimeSettingsRecord
        {
            SettingsKey = GlobalSettingsKey,
            Model = NormalizeModel(openAiSecurityOptions.Model),
            BaseUrl = NormalizeBaseUrl(openAiSecurityOptions.BaseUrl),
            RequestTimeoutSeconds = openAiSecurityOptions.RequestTimeoutSeconds > 0
                ? openAiSecurityOptions.RequestTimeoutSeconds
                : 45,
            UseBackgroundMode = openAiSecurityOptions.UseBackgroundMode,
            BackgroundPollingIntervalMilliseconds = openAiSecurityOptions.BackgroundPollingIntervalMilliseconds > 0
                ? openAiSecurityOptions.BackgroundPollingIntervalMilliseconds
                : 1500,
            BackgroundPollingTimeoutSeconds = openAiSecurityOptions.BackgroundPollingTimeoutSeconds > 0
                ? openAiSecurityOptions.BackgroundPollingTimeoutSeconds
                : 120,
            MaxConcurrentScoringRequests = openAiSecurityOptions.MaxConcurrentScoringRequests > 0
                ? openAiSecurityOptions.MaxConcurrentScoringRequests
                : 2,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.OpenAiRuntimeSettings.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    private static OpenAiRuntimeSettingsProfile Map(OpenAiRuntimeSettingsRecord record)
    {
        return new OpenAiRuntimeSettingsProfile(
            record.Model,
            record.BaseUrl,
            record.RequestTimeoutSeconds,
            record.UseBackgroundMode,
            record.BackgroundPollingIntervalMilliseconds,
            record.BackgroundPollingTimeoutSeconds,
            record.MaxConcurrentScoringRequests,
            ConcurrencyTokenCodec.Encode(record.RowVersion));
    }

    private static string NormalizeModel(string model)
    {
        return string.IsNullOrWhiteSpace(model) ? string.Empty : model.Trim();
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return DefaultOpenAiBaseUrl.OriginalString;
        }

        return baseUrl.Trim().TrimEnd('/');
    }
}

public static class OpenAiRuntimeSettingsProfileValidator
{
    public static string? Validate(OpenAiRuntimeSettingsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Model))
        {
            return "OpenAI model is required for runtime settings.";
        }

        if (profile.Model.Trim().Length > 128)
        {
            return "OpenAI model must be 128 characters or fewer.";
        }

        if (!OpenAiModelCatalog.IsSupported(profile.Model))
        {
            return "OpenAI model is not supported. Select one of the recommended models in OpenAI Setup.";
        }

        if (string.IsNullOrWhiteSpace(profile.BaseUrl))
        {
            return "OpenAI base URL is required for runtime settings.";
        }

        var normalizedBaseUrl = profile.BaseUrl.Trim();
        if (normalizedBaseUrl.Length > 512)
        {
            return "OpenAI base URL must be 512 characters or fewer.";
        }

        if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var baseUrlUri) ||
            (baseUrlUri.Scheme != Uri.UriSchemeHttp && baseUrlUri.Scheme != Uri.UriSchemeHttps))
        {
            return "OpenAI base URL must be a valid absolute HTTP or HTTPS URL.";
        }

        if (profile.RequestTimeoutSeconds <= 0)
        {
            return "OpenAI request timeout must be greater than zero.";
        }

        if (profile.UseBackgroundMode)
        {
            if (profile.BackgroundPollingIntervalMilliseconds <= 0)
            {
                return "OpenAI background polling interval must be greater than zero when background mode is enabled.";
            }

            if (profile.BackgroundPollingTimeoutSeconds <= 0)
            {
                return "OpenAI background polling timeout must be greater than zero when background mode is enabled.";
            }
        }

        if (profile.MaxConcurrentScoringRequests <= 0)
        {
            return "OpenAI concurrent scoring limit must be greater than zero.";
        }

        return null;
    }
}
