using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.Persistence;

public sealed class UserScopedIndexContractTests
{
    [Fact]
    public void UserOwnedRootsUseUserScopedUniqueIndexes()
    {
        using var dbContext = CreateDbContext();

        AssertEntityHasUniqueIndex<LinkedInSessionRecord>(dbContext, nameof(LinkedInSessionRecord.AppUserId), nameof(LinkedInSessionRecord.SessionKey));
        AssertEntityHasUniqueIndex<LinkedInSearchSettingsRecord>(dbContext, nameof(LinkedInSearchSettingsRecord.AppUserId));
        AssertEntityHasUniqueIndex<AiBehaviorSettingsRecord>(dbContext, nameof(AiBehaviorSettingsRecord.AppUserId));
        AssertEntityHasUniqueIndex<JobRecord>(dbContext, nameof(JobRecord.AppUserId), nameof(JobRecord.LinkedInJobId));
        AssertEntityHasUniqueIndex<JobRecord>(dbContext, nameof(JobRecord.AppUserId), nameof(JobRecord.LinkedInJobPostingUrn));
    }

    [Fact]
    public void LegacyGlobalUniqueIndexesAreNotPresentOnUserOwnedRoots()
    {
        using var dbContext = CreateDbContext();

        AssertEntityDoesNotHaveUniqueIndex<JobRecord>(dbContext, nameof(JobRecord.LinkedInJobId));
        AssertEntityDoesNotHaveUniqueIndex<JobRecord>(dbContext, nameof(JobRecord.LinkedInJobPostingUrn));
        AssertEntityDoesNotHaveUniqueIndex<LinkedInSessionRecord>(dbContext, nameof(LinkedInSessionRecord.SessionKey));
    }

    private static LinkedInJobScraperDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new LinkedInJobScraperDbContext(options);
    }

    private static void AssertEntityHasUniqueIndex<TEntity>(
        LinkedInJobScraperDbContext dbContext,
        params string[] propertyNames)
        where TEntity : class
    {
        var uniqueIndexProperties = GetUniqueIndexPropertyNames<TEntity>(dbContext);
        Assert.Contains(uniqueIndexProperties, properties => properties.SequenceEqual(propertyNames));
    }

    private static void AssertEntityDoesNotHaveUniqueIndex<TEntity>(
        LinkedInJobScraperDbContext dbContext,
        params string[] propertyNames)
        where TEntity : class
    {
        var uniqueIndexProperties = GetUniqueIndexPropertyNames<TEntity>(dbContext);
        Assert.DoesNotContain(uniqueIndexProperties, properties => properties.SequenceEqual(propertyNames));
    }

    private static List<string[]> GetUniqueIndexPropertyNames<TEntity>(LinkedInJobScraperDbContext dbContext)
        where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        return entityType!
            .GetIndexes()
            .Where(static index => index.IsUnique)
            .Select(index => index.Properties.Select(static property => property.Name).ToArray())
            .ToList();
    }
}
