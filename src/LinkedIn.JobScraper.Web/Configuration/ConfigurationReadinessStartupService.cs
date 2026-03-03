using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class ConfigurationReadinessStartupService : IHostedService
{
    private static readonly Action<ILogger, string, Exception?> LogConfigurationReadinessWarning =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1001, nameof(ConfigurationReadinessStartupService)),
            "{ConfigurationReadinessWarning}");

    private readonly IOptions<SqlServerOptions> _sqlServerOptions;
    private readonly IOptions<OpenAiSecurityOptions> _openAiSecurityOptions;
    private readonly ILogger<ConfigurationReadinessStartupService> _logger;

    public ConfigurationReadinessStartupService(
        IOptions<SqlServerOptions> sqlServerOptions,
        IOptions<OpenAiSecurityOptions> openAiSecurityOptions,
        ILogger<ConfigurationReadinessStartupService> logger)
    {
        _sqlServerOptions = sqlServerOptions;
        _openAiSecurityOptions = openAiSecurityOptions;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var warnings = ConfigurationReadinessValidator.GetWarnings(
            _sqlServerOptions.Value,
            _openAiSecurityOptions.Value);

        foreach (var warning in warnings)
        {
            LogConfigurationReadinessWarning(_logger, warning, null);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
