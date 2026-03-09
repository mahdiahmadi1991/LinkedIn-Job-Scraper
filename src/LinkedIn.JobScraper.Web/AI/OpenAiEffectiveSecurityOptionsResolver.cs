using System.Linq;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public interface IOpenAiEffectiveSecurityOptionsResolver
{
    Task<OpenAiSecurityOptions> ResolveAsync(CancellationToken cancellationToken);
}

public sealed class OpenAiEffectiveSecurityOptionsResolver : IOpenAiEffectiveSecurityOptionsResolver
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<OpenAiSecurityOptions> _openAiSecurityOptions;
    private readonly IOpenAiRuntimeApiKeyService _openAiRuntimeApiKeyService;
    private readonly IOpenAiRuntimeSettingsService _runtimeSettingsService;

    public OpenAiEffectiveSecurityOptionsResolver(
        IConfiguration configuration,
        IOptionsMonitor<OpenAiSecurityOptions> openAiSecurityOptions,
        IOpenAiRuntimeApiKeyService openAiRuntimeApiKeyService,
        IOpenAiRuntimeSettingsService runtimeSettingsService)
    {
        _configuration = configuration;
        _openAiSecurityOptions = openAiSecurityOptions;
        _openAiRuntimeApiKeyService = openAiRuntimeApiKeyService;
        _runtimeSettingsService = runtimeSettingsService;
    }

    public async Task<OpenAiSecurityOptions> ResolveAsync(CancellationToken cancellationToken)
    {
        var runtimeProfile = await _runtimeSettingsService.GetActiveAsync(cancellationToken);
        var runtimeApiKey = await _openAiRuntimeApiKeyService.GetActiveAsync(cancellationToken);
        var configuredOptions = _openAiSecurityOptions.CurrentValue;

        return new OpenAiSecurityOptions
        {
            ApiKey = runtimeApiKey ?? string.Empty,
            Model = HasConfigurationOverride("Model")
                ? configuredOptions.Model
                : runtimeProfile.Model,
            BaseUrl = HasConfigurationOverride("BaseUrl")
                ? configuredOptions.BaseUrl
                : runtimeProfile.BaseUrl,
            RequestTimeoutSeconds = HasConfigurationOverride("RequestTimeoutSeconds")
                ? configuredOptions.RequestTimeoutSeconds
                : runtimeProfile.RequestTimeoutSeconds,
            UseBackgroundMode = HasConfigurationOverride("UseBackgroundMode")
                ? configuredOptions.UseBackgroundMode
                : runtimeProfile.UseBackgroundMode,
            BackgroundPollingIntervalMilliseconds = HasConfigurationOverride("BackgroundPollingIntervalMilliseconds")
                ? configuredOptions.BackgroundPollingIntervalMilliseconds
                : runtimeProfile.BackgroundPollingIntervalMilliseconds,
            BackgroundPollingTimeoutSeconds = HasConfigurationOverride("BackgroundPollingTimeoutSeconds")
                ? configuredOptions.BackgroundPollingTimeoutSeconds
                : runtimeProfile.BackgroundPollingTimeoutSeconds,
            MaxConcurrentScoringRequests = HasConfigurationOverride("MaxConcurrentScoringRequests")
                ? configuredOptions.MaxConcurrentScoringRequests
                : runtimeProfile.MaxConcurrentScoringRequests
        };
    }

    private bool HasConfigurationOverride(string keySuffix)
    {
        var key = $"{OpenAiSecurityOptions.SectionName}:{keySuffix}";
        return HasConfiguredValue(key);
    }

    private bool HasConfiguredValue(string key)
    {
        if (_configuration is not IConfigurationRoot configurationRoot)
        {
            return false;
        }

        foreach (var provider in configurationRoot.Providers.Reverse())
        {
            if (provider.TryGet(key, out _))
            {
                return true;
            }
        }

        return false;
    }
}
