using System.Net;
using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Diagnostics;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Details;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.LinkedIn.Session;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMvpApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName));

        services.AddOptions<AppAuthenticationOptions>()
            .Bind(configuration.GetSection(AppAuthenticationOptions.SectionName));

        services.AddOptions<OpenAiSecurityOptions>()
            .Bind(configuration.GetSection(OpenAiSecurityOptions.SectionName));

        services.AddOptions<LinkedInBrowserAutomationOptions>()
            .Bind(configuration.GetSection(LinkedInBrowserAutomationOptions.SectionName));

        services.AddHostedService<ConfigurationReadinessStartupService>();
        services.AddHostedService<AppUserSeedingStartupService>();
        services.AddSignalR();
        services.AddSingleton<IAppUserPasswordHasher, AppUserPasswordHasher>();
        services.AddSingleton<ISqlServerConnectionStringProvider, ConfiguredSqlServerConnectionStringProvider>();
        services.AddSingleton<ILinkedInSessionStore, DatabaseLinkedInSessionStore>();
        services.AddSingleton<ILinkedInBrowserLoginService, PlaywrightLinkedInBrowserLoginService>();
        services.AddSingleton<ILinkedInSearchSettingsService, LinkedInSearchSettingsService>();
        services.AddSingleton<IJobsWorkflowStateStore, InMemoryJobsWorkflowStateStore>();
        services.AddSingleton<IJobsWorkflowProgressNotifier, SignalRJobsWorkflowProgressNotifier>();
        services.AddTransient<ILinkedInSessionVerificationService, LinkedInSessionVerificationService>();
        services.AddTransient<IAiBehaviorSettingsService, AiBehaviorSettingsService>();
        services.AddTransient<ILinkedInLocationLookupService, LinkedInLocationLookupService>();
        services.AddTransient<ILinkedInJobDetailService, LinkedInJobDetailService>();
        services.AddTransient<ILinkedInJobSearchService, LinkedInJobSearchService>();
        services.AddTransient<IJobImportService, JobImportService>();
        services.AddTransient<IJobEnrichmentService, JobEnrichmentService>();
        services.AddTransient<IJobBatchScoringService, JobBatchScoringService>();
        services.AddTransient<IJobsDashboardService, JobsDashboardService>();
        services.AddDbContextFactory<LinkedInJobScraperDbContext>(ConfigureSqlServerDbContext);

        services.AddHttpClient<ILinkedInApiClient, LinkedInApiClient>()
            .ConfigurePrimaryHttpMessageHandler(CreateLinkedInHttpHandler);

        services.AddHttpClient<IJobScoringGateway, OpenAiJobScoringGateway>(
            static (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<OpenAiSecurityOptions>>()
                    .Value;

                var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                    ? "https://api.openai.com/v1"
                    : options.BaseUrl.TrimEnd('/');

                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(45);
            });

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
