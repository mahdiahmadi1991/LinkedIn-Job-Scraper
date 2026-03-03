using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
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

        using var service = new LinkedInSearchSettingsService(new TestDbContextFactory(options));

        var settings = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal(string.Empty, settings.ProfileName);
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

        using var service = new LinkedInSearchSettingsService(new TestDbContextFactory(options));

        var saved = await service.SaveAsync(
            new LinkedInSearchSettings(
                "  ",
                "  C# Backend  ",
                "  Bucharest  ",
                "  Bucharest, Romania  ",
                " 123456 ",
                true,
                ["2", "2", "1"],
                []),
            CancellationToken.None);

        Assert.Equal(string.Empty, saved.ProfileName);
        Assert.Equal("C# Backend", saved.Keywords);
        Assert.Equal("123456", saved.LocationGeoId);
        Assert.Equal(ExpectedWorkplaceTypeCodes, saved.WorkplaceTypeCodes);
        Assert.Empty(saved.JobTypeCodes);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var record = await dbContext.LinkedInSearchSettings.SingleAsync();
        Assert.Equal(string.Empty, record.ProfileName);
        Assert.Equal("C# Backend", record.Keywords);
        Assert.Equal("2,1", record.WorkplaceTypeCodesCsv);
        Assert.Equal(string.Empty, record.JobTypeCodesCsv);
    }

    [Fact]
    public async Task SaveAsyncThrowsFriendlyExceptionForMalformedConcurrencyToken()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        using var service = new LinkedInSearchSettingsService(new TestDbContextFactory(options));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(
                new LinkedInSearchSettings(
                    "Default",
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
