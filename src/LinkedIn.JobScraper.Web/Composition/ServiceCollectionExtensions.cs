using System.Net;
using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMvpApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName));

        services.AddOptions<OpenAiSecurityOptions>()
            .Bind(configuration.GetSection(OpenAiSecurityOptions.SectionName));

        services.AddOptions<LinkedInBrowserAutomationOptions>()
            .Bind(configuration.GetSection(LinkedInBrowserAutomationOptions.SectionName));

        services.AddSingleton<ISqlServerConnectionStringProvider, ConfiguredSqlServerConnectionStringProvider>();
        services.AddSingleton<ILinkedInSessionStore, DatabaseLinkedInSessionStore>();
        services.AddSingleton<ILinkedInBrowserLoginService, PlaywrightLinkedInBrowserLoginService>();
        services.AddSingleton<IJobScoringGateway, OpenAiJobScoringGateway>();
        services.AddDbContextFactory<LinkedInJobScraperDbContext>(ConfigureSqlServerDbContext);

        services.AddHttpClient<ILinkedInApiClient, LinkedInApiClient>()
            .ConfigurePrimaryHttpMessageHandler(CreateLinkedInHttpHandler);

        services.AddTransient<LinkedInFeasibilityProbe>();

        return services;
    }

    private static HttpClientHandler CreateLinkedInHttpHandler()
    {
        return new HttpClientHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip |
                                     DecompressionMethods.Deflate |
                                     DecompressionMethods.Brotli
        };
    }

    private static void ConfigureSqlServerDbContext(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder optionsBuilder)
    {
        var connectionStringProvider = serviceProvider.GetRequiredService<ISqlServerConnectionStringProvider>();
        var connectionString = connectionStringProvider.IsConfigured
            ? connectionStringProvider.GetRequiredConnectionString()
            : "Server=localhost;Database=LinkedInJobScraper;Integrated Security=True;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString);
    }
}
