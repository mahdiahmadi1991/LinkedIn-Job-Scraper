using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class AiBehaviorSettingsServiceTests
{
    [Fact]
    public async Task GetActiveAsyncCreatesAndReturnsDefaultProfile()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var service = new AiBehaviorSettingsService(new TestDbContextFactory(options));

        var profile = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal("Default", profile.ProfileName);
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
}
