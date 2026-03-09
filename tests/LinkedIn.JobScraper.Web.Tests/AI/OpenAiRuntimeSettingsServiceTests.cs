using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class OpenAiRuntimeSettingsServiceTests
{
    [Fact]
    public async Task GetActiveAsyncCreatesDefaultRecordFromConfiguredOptions()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new OpenAiRuntimeSettingsService(
            new TestDbContextFactory(options),
            CreateOptionsMonitor(
                new OpenAiSecurityOptions
                {
                    Model = "gpt-5-mini",
                    BaseUrl = "https://api.openai.com/v1",
                    RequestTimeoutSeconds = 60,
                    UseBackgroundMode = true,
                    BackgroundPollingIntervalMilliseconds = 900,
                    BackgroundPollingTimeoutSeconds = 90,
                    MaxConcurrentScoringRequests = 3
                }));

        var profile = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal("gpt-5-mini", profile.Model);
        Assert.Equal("https://api.openai.com/v1", profile.BaseUrl);
        Assert.Equal(60, profile.RequestTimeoutSeconds);
        Assert.True(profile.UseBackgroundMode);
        Assert.Equal(900, profile.BackgroundPollingIntervalMilliseconds);
        Assert.Equal(90, profile.BackgroundPollingTimeoutSeconds);
        Assert.Equal(3, profile.MaxConcurrentScoringRequests);
        // InMemory provider does not generate rowversion values.
        Assert.Null(profile.ConcurrencyToken);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        Assert.Equal(1, await dbContext.OpenAiRuntimeSettings.CountAsync());
    }

    [Fact]
    public async Task SaveAsyncPersistsNormalizedValues()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new OpenAiRuntimeSettingsService(
            new TestDbContextFactory(options),
            CreateOptionsMonitor(new OpenAiSecurityOptions()));

        var initial = await service.GetActiveAsync(CancellationToken.None);
        var saved = await service.SaveAsync(
            new OpenAiRuntimeSettingsProfile(
                "  gpt-5  ",
                " https://api.openai.com/v1/ ",
                55,
                true,
                1100,
                140,
                4,
                initial.ConcurrencyToken),
            CancellationToken.None);

        Assert.Equal("gpt-5", saved.Model);
        Assert.Equal("https://api.openai.com/v1", saved.BaseUrl);
        Assert.Equal(55, saved.RequestTimeoutSeconds);
        Assert.True(saved.UseBackgroundMode);
        Assert.Equal(1100, saved.BackgroundPollingIntervalMilliseconds);
        Assert.Equal(140, saved.BackgroundPollingTimeoutSeconds);
        Assert.Equal(4, saved.MaxConcurrentScoringRequests);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var record = await dbContext.OpenAiRuntimeSettings.SingleAsync();
        Assert.Equal("gpt-5", record.Model);
        Assert.Equal("https://api.openai.com/v1", record.BaseUrl);
    }

    [Fact]
    public async Task SaveAsyncRejectsInvalidRuntimeSettings()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new OpenAiRuntimeSettingsService(
            new TestDbContextFactory(options),
            CreateOptionsMonitor(new OpenAiSecurityOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(
                new OpenAiRuntimeSettingsProfile(
                    "gpt-5-mini",
                    "not-a-url",
                    45,
                    true,
                    1500,
                    120,
                    2),
                CancellationToken.None));

        Assert.Contains("base URL", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsyncThrowsFriendlyExceptionForMalformedConcurrencyToken()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new OpenAiRuntimeSettingsService(
            new TestDbContextFactory(options),
            CreateOptionsMonitor(new OpenAiSecurityOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(
                new OpenAiRuntimeSettingsProfile(
                    "gpt-5-mini",
                    "https://api.openai.com/v1",
                    45,
                    true,
                    1500,
                    120,
                    2,
                    "not-base64"),
                CancellationToken.None));

        Assert.Contains("submitted settings state is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static StaticOptionsMonitor<OpenAiSecurityOptions> CreateOptionsMonitor(OpenAiSecurityOptions options)
    {
        return new StaticOptionsMonitor<OpenAiSecurityOptions>(options);
    }
}

public sealed class OpenAiRuntimeSettingsProfileValidatorTests
{
    [Fact]
    public void ValidateReturnsFailureForInvalidBackgroundPollingValues()
    {
        var message = OpenAiRuntimeSettingsProfileValidator.Validate(
            new OpenAiRuntimeSettingsProfile(
                "gpt-5-mini",
                "https://api.openai.com/v1",
                45,
                true,
                0,
                120,
                2));

        Assert.NotNull(message);
        Assert.Contains("polling interval", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateReturnsNullForValidProfile()
    {
        var message = OpenAiRuntimeSettingsProfileValidator.Validate(
            new OpenAiRuntimeSettingsProfile(
                "gpt-5-mini",
                "https://api.openai.com/v1",
                45,
                true,
                1500,
                120,
                2));

        Assert.Null(message);
    }
}

sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class
{
    public StaticOptionsMonitor(TOptions currentValue)
    {
        CurrentValue = currentValue;
    }

    public TOptions CurrentValue { get; }

    public TOptions Get(string? name)
    {
        return CurrentValue;
    }

    public IDisposable OnChange(Action<TOptions, string?> listener)
    {
        return NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
