using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Tests.Authentication;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class AiBehaviorSettingsServiceTests
{
    [Fact]
    public async Task GetActiveAsyncCreatesAndReturnsBlankProfile()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new AiBehaviorSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext());

        var profile = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal(string.Empty, profile.BehavioralInstructions);
        Assert.Equal(string.Empty, profile.PrioritySignals);
        Assert.Equal(string.Empty, profile.ExclusionSignals);
        Assert.Equal(AiOutputLanguage.English, profile.OutputLanguageCode);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        Assert.Equal(1, await dbContext.AiBehaviorSettings.CountAsync());
    }

    [Fact]
    public async Task SaveAsyncPersistsUpdatedProfileAndNormalizesLanguage()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new AiBehaviorSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext());

        var saved = await service.SaveAsync(
            new AiBehaviorProfile(
                "  Prefer backend fit  ",
                "  C#, .NET  ",
                "  Frontend-only roles  ",
                "FA"),
            CancellationToken.None);

        Assert.Equal(AiOutputLanguage.Persian, saved.OutputLanguageCode);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var record = await dbContext.AiBehaviorSettings.SingleAsync();
        Assert.Equal("fa", record.OutputLanguageCode);
    }

    [Fact]
    public async Task GetActiveAsyncKeepsProfilesIsolatedPerUser()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var firstUserService = new AiBehaviorSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext(1));
        var secondUserService = new AiBehaviorSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext(2));

        _ = await firstUserService.SaveAsync(
            new AiBehaviorProfile(
                "Prefer backend",
                "C#",
                "No frontend",
                "en"),
            CancellationToken.None);

        var firstUserProfile = await firstUserService.GetActiveAsync(CancellationToken.None);
        var secondUserProfile = await secondUserService.GetActiveAsync(CancellationToken.None);

        Assert.Equal("Prefer backend", firstUserProfile.BehavioralInstructions);
        Assert.Equal(string.Empty, secondUserProfile.BehavioralInstructions);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        Assert.Equal(2, await dbContext.AiBehaviorSettings.CountAsync());
        Assert.Equal(1, await dbContext.AiBehaviorSettings.CountAsync(settings => settings.AppUserId == 1));
        Assert.Equal(1, await dbContext.AiBehaviorSettings.CountAsync(settings => settings.AppUserId == 2));
    }

    [Fact]
    public async Task SaveAsyncThrowsFriendlyExceptionForMalformedConcurrencyToken()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new AiBehaviorSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(
                new AiBehaviorProfile(
                    "Behavior",
                    "Priority",
                    "Exclusion",
                    "en",
                    "not-base64"),
                CancellationToken.None));

        Assert.Contains("submitted settings state is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
