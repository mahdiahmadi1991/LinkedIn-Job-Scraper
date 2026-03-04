using System.Net;
using System.Threading.RateLimiting;
using LinkedIn.JobScraper.Web.Composition;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Jobs;
using LinkedIn.JobScraper.Web.Logging;
using LinkedIn.JobScraper.Web.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var perRunLogFilePath = PerRunLogFilePath.Create(
    builder.Environment.ContentRootPath,
    DateTimeOffset.UtcNow,
    Environment.ProcessId);
var enableFetchDiagnostics = builder.Configuration.GetValue<bool>("LinkedIn:FetchDiagnostics:Enabled");

builder.Logging.AddProvider(new PerRunFileLoggerProvider(perRunLogFilePath, enableFetchDiagnostics));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks()
    .AddCheck<ConfigurationReadinessHealthCheck>(
        "configuration_readiness",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(SecurityRateLimitPolicies.SensitiveLocalActions, limiterOptions =>
    {
        limiterOptions.PermitLimit = 6;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
        limiterOptions.AutoReplenishment = true;
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});
builder.Services.AddMvpApplication(builder.Configuration);

var app = builder.Build();

StartupLog.PerRunLogFileInitialized(app.Logger, perRunLogFilePath);
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRequestCorrelation();
app.UseBasicSecurityHeaders();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks(
    "/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    });
app.MapStaticAssets();
app.MapHub<JobsWorkflowProgressHub>("/hubs/jobs-workflow-progress");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program;
