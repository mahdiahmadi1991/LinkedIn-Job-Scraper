using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LinkedIn.JobScraper.Web.Persistence;

public sealed class LinkedInJobScraperDbContextFactory : IDesignTimeDbContextFactory<LinkedInJobScraperDbContext>
{
    public LinkedInJobScraperDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var projectDirectory = ResolveProjectDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetSection(SqlServerOptions.SectionName)["ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SqlServer:ConnectionString is not configured for design-time operations.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new LinkedInJobScraperDbContext(optionsBuilder.Options);
    }

    private static string ResolveProjectDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        if (File.Exists(Path.Combine(currentDirectory, "LinkedIn.JobScraper.Web.csproj")))
        {
            return currentDirectory;
        }

        return Path.Combine(currentDirectory, "src", "LinkedIn.JobScraper.Web");
    }
}
