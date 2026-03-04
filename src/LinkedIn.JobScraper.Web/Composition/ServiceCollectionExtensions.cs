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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMvpApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<AppAuthenticationOptions>()
            .Bind(configuration.GetSection(AppAuthenticationOptions.SectionName));

        services.AddOptions<OpenAiSecurityOptions>()
            .Bind(configuration.GetSection(OpenAiSecurityOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<LinkedInFetchDiagnosticsOptions>()
            .Bind(configuration.GetSection(LinkedInFetchDiagnosticsOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<LinkedInFetchLimitsOptions>()
            .Bind(configuration.GetSection(LinkedInFetchLimitsOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<LinkedInRequestOptions>()
            .Bind(configuration.GetSection(LinkedInRequestOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<LinkedInBrowserAutomationOptions>()
            .Bind(configuration.GetSection(LinkedInBrowserAutomationOptions.SectionName));

        services.AddOptions<JobsWorkflowOptions>()
            .Bind(configuration.GetSection(JobsWorkflowOptions.SectionName))
            .ValidateOnStart();

        services.AddHostedService<AppUserSeedingStartupService>();
        services.AddAuthentication(AppAuthenticationDefaults.CookieScheme)
            .AddCookie(
                AppAuthenticationDefaults.CookieScheme,
                options =>
                {
                    options.LoginPath = AppAuthenticationDefaults.LoginPath;
                    options.LogoutPath = AppAuthenticationDefaults.LogoutPath;
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = TimeSpan.FromDays(14);
                    options.Cookie.Name = "ljs.auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                });
        services.AddSignalR();
        services.AddSingleton<IValidateOptions<SqlServerOptions>, SqlServerOptionsValidator>();
        services.AddSingleton<IValidateOptions<OpenAiSecurityOptions>, OpenAiSecurityOptionsValidator>();
        services.AddSingleton<IValidateOptions<LinkedInFetchDiagnosticsOptions>, LinkedInFetchDiagnosticsOptionsValidator>();
        services.AddSingleton<IValidateOptions<LinkedInFetchLimitsOptions>, LinkedInFetchLimitsOptionsValidator>();
        services.AddSingleton<IValidateOptions<LinkedInRequestOptions>, LinkedInRequestOptionsValidator>();
        services.AddSingleton<IValidateOptions<JobsWorkflowOptions>, JobsWorkflowOptionsValidator>();
        services.AddSingleton<IAppUserPasswordHasher, AppUserPasswordHasher>();
        services.AddTransient<IAppUserAuthenticationService, AppUserAuthenticationService>();
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
        services.AddSingleton<IJobsWorkflowExecutor, ScopedJobsWorkflowExecutor>();
        services.AddDbContextFactory<LinkedInJobScraperDbContext>(ConfigureSqlServerDbContext);

        services.AddHttpClient<ILinkedInApiClient, LinkedInApiClient>()
            .ConfigurePrimaryHttpMessageHandler(CreateLinkedInHttpHandler)
            .AddStandardResilienceHandler();

        services.AddTransient<IOpenAiResponsesClient, OpenAiSdkResponsesClient>();
        services.AddTransient<IJobScoringGateway, OpenAiJobScoringGateway>();

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
