using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Tests.Persistence;

internal sealed class TestDbContextFactory : IDbContextFactory<LinkedInJobScraperDbContext>
{
    private readonly DbContextOptions<LinkedInJobScraperDbContext> _options;

    public TestDbContextFactory(DbContextOptions<LinkedInJobScraperDbContext> options)
    {
        _options = options;
    }

    public LinkedInJobScraperDbContext CreateDbContext()
    {
        return new LinkedInJobScraperDbContext(_options);
    }

    public Task<LinkedInJobScraperDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
