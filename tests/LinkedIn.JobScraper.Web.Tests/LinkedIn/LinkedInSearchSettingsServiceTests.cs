using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Tests.Authentication;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSearchSettingsServiceTests
{
    private static readonly string[] ExpectedWorkplaceTypeCodes = ["2", "1"];

    [Fact]
    public async Task GetActiveAsyncCreatesBlankSettingsOnNonRelationalProvider()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        using var service = new LinkedInSearchSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext());

        var settings = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal(string.Empty, settings.Keywords);
        Assert.Null(settings.LocationGeoId);
        Assert.False(settings.EasyApply);
        Assert.Empty(settings.WorkplaceTypeCodes);
        Assert.Empty(settings.JobTypeCodes);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        Assert.Equal(1, await dbContext.LinkedInSearchSettings.CountAsync());
    }

    [Fact]
    public async Task SaveAsyncPersistsNormalizedSettings()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        using var service = new LinkedInSearchSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext());

        var saved = await service.SaveAsync(
            new LinkedInSearchSettings(
                "  C# Backend  ",
                "  Bucharest  ",
                "  Bucharest, Romania  ",
                " 123456 ",
                true,
                ["2", "2", "1"],
                []),
            CancellationToken.None);

        Assert.Equal("C# Backend", saved.Keywords);
        Assert.Equal("123456", saved.LocationGeoId);
        Assert.Equal(ExpectedWorkplaceTypeCodes, saved.WorkplaceTypeCodes);
        Assert.Empty(saved.JobTypeCodes);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var record = await dbContext.LinkedInSearchSettings.SingleAsync();
        Assert.Equal("C# Backend", record.Keywords);
        Assert.Equal("2,1", record.WorkplaceTypeCodesCsv);
        Assert.Equal(string.Empty, record.JobTypeCodesCsv);
    }

    [Fact]
    public async Task GetActiveAsyncKeepsSettingsIsolatedPerUser()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        using var firstUserService = new LinkedInSearchSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext(1));
        using var secondUserService = new LinkedInSearchSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext(2));

        _ = await firstUserService.SaveAsync(
            new LinkedInSearchSettings(
                "DotNet",
                "Limassol",
                "Limassol, Cyprus",
                "101",
                true,
                ["2"],
                ["F"]),
            CancellationToken.None);

        var firstUserSettings = await firstUserService.GetActiveAsync(CancellationToken.None);
        var secondUserSettings = await secondUserService.GetActiveAsync(CancellationToken.None);

        Assert.Equal("DotNet", firstUserSettings.Keywords);
        Assert.Equal("101", firstUserSettings.LocationGeoId);
        Assert.Equal(string.Empty, secondUserSettings.Keywords);
        Assert.Null(secondUserSettings.LocationGeoId);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        Assert.Equal(2, await dbContext.LinkedInSearchSettings.CountAsync());
        Assert.Equal(1, await dbContext.LinkedInSearchSettings.CountAsync(settings => settings.AppUserId == 1));
        Assert.Equal(1, await dbContext.LinkedInSearchSettings.CountAsync(settings => settings.AppUserId == 2));
    }

    [Fact]
    public async Task SaveAsyncThrowsFriendlyExceptionForMalformedConcurrencyToken()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        using var service = new LinkedInSearchSettingsService(
            new TestDbContextFactory(options),
            new TestCurrentAppUserContext());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(
                new LinkedInSearchSettings(
                    "C# Backend",
                    null,
                    null,
                    null,
                    true,
                    ["1"],
                    ["F"],
                    "not-base64"),
                CancellationToken.None));

        Assert.Contains("submitted settings state is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
