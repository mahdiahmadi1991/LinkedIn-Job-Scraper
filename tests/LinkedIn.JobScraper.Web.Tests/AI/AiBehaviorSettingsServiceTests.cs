using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence;
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

        var service = new AiBehaviorSettingsService(new TestDbContextFactory(options));

        var profile = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal(string.Empty, profile.ProfileName);
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

        var service = new AiBehaviorSettingsService(new TestDbContextFactory(options));

        var saved = await service.SaveAsync(
            new AiBehaviorProfile(
                "  Portfolio Profile  ",
                "  Prefer backend fit  ",
                "  C#, .NET  ",
                "  Frontend-only roles  ",
                "FA"),
            CancellationToken.None);

        Assert.Equal("Portfolio Profile", saved.ProfileName);
        Assert.Equal(AiOutputLanguage.Persian, saved.OutputLanguageCode);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var record = await dbContext.AiBehaviorSettings.SingleAsync();
        Assert.Equal("Portfolio Profile", record.ProfileName);
        Assert.Equal("fa", record.OutputLanguageCode);
    }

    [Fact]
    public async Task SaveAsyncThrowsFriendlyExceptionForMalformedConcurrencyToken()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new AiBehaviorSettingsService(new TestDbContextFactory(options));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(
                new AiBehaviorProfile(
                    "Default",
                    "Behavior",
                    "Priority",
                    "Exclusion",
                    "en",
                    "not-base64"),
                CancellationToken.None));

        Assert.Contains("submitted settings state is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
