using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSearchSettingsServiceTests
{
    private static readonly string[] ExpectedWorkplaceTypeCodes = ["2", "1"];
    private static readonly string[] ExpectedDefaultJobTypeCodes = ["F", "P", "C", "T", "I", "O"];

    [Fact]
    public async Task GetActiveAsyncCreatesDefaultSettingsOnNonRelationalProvider()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        using var service = new LinkedInSearchSettingsService(new TestDbContextFactory(options));

        var settings = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal("Default", settings.ProfileName);
        Assert.Equal("C# .Net", settings.Keywords);
        Assert.Equal("106394980", settings.LocationGeoId);

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

        Assert.Equal("Default", saved.ProfileName);
        Assert.Equal("C# Backend", saved.Keywords);
        Assert.Equal("123456", saved.LocationGeoId);
        Assert.Equal(ExpectedWorkplaceTypeCodes, saved.WorkplaceTypeCodes);
        Assert.Equal(ExpectedDefaultJobTypeCodes, saved.JobTypeCodes);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var record = await dbContext.LinkedInSearchSettings.SingleAsync();
        Assert.Equal("Default", record.ProfileName);
        Assert.Equal("C# Backend", record.Keywords);
        Assert.Equal("2,1", record.WorkplaceTypeCodesCsv);
        Assert.Equal("F,P,C,T,I,O", record.JobTypeCodesCsv);
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
