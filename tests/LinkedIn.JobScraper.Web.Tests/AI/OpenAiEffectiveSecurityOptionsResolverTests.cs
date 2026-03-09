using System.Text;
using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class OpenAiEffectiveSecurityOptionsResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesRuntimeTechnicalSettingsWhenConfigurationHasNoTechnicalOverrides()
    {
        var configuration = BuildConfiguration(
            """
            {
              "OpenAI": {
                "Security": {}
              }
            }
            """);

        var optionsMonitor = new MutableOptionsMonitor<OpenAiSecurityOptions>(
            new OpenAiSecurityOptions
            {
                Model = "options-model",
                BaseUrl = "https://options.example/v1",
                RequestTimeoutSeconds = 30,
                UseBackgroundMode = false,
                BackgroundPollingIntervalMilliseconds = 700,
                BackgroundPollingTimeoutSeconds = 50,
                MaxConcurrentScoringRequests = 1
            });
        var runtimeService = new FakeOpenAiRuntimeSettingsService(
            new OpenAiRuntimeSettingsProfile(
                "runtime-model",
                "https://runtime.example/v1",
                75,
                true,
                1500,
                180,
                4,
                "token-1"));
        var resolver = new OpenAiEffectiveSecurityOptionsResolver(
            configuration,
            optionsMonitor,
            new FixedOpenAiRuntimeApiKeyService(),
            runtimeService);

        var resolved = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal(string.Empty, resolved.ApiKey);
        Assert.Equal("runtime-model", resolved.Model);
        Assert.Equal("https://runtime.example/v1", resolved.BaseUrl);
        Assert.Equal(75, resolved.RequestTimeoutSeconds);
        Assert.True(resolved.UseBackgroundMode);
        Assert.Equal(1500, resolved.BackgroundPollingIntervalMilliseconds);
        Assert.Equal(180, resolved.BackgroundPollingTimeoutSeconds);
        Assert.Equal(4, resolved.MaxConcurrentScoringRequests);
    }

    [Fact]
    public async Task ResolveAsyncUsesJsonTechnicalOverridesOverRuntimeSettings()
    {
        var configuration = BuildConfiguration(
            """
            {
              "OpenAI": {
                "Security": {
                  "Model": "json-model",
                  "BaseUrl": "https://json-config.example/v1",
                  "RequestTimeoutSeconds": 40,
                  "UseBackgroundMode": false,
                  "BackgroundPollingIntervalMilliseconds": 800,
                  "BackgroundPollingTimeoutSeconds": 60,
                  "MaxConcurrentScoringRequests": 1
                }
              }
            }
            """);

        var optionsMonitor = new MutableOptionsMonitor<OpenAiSecurityOptions>(
            new OpenAiSecurityOptions
            {
                Model = "json-model",
                BaseUrl = "https://json-config.example/v1",
                RequestTimeoutSeconds = 40,
                UseBackgroundMode = false,
                BackgroundPollingIntervalMilliseconds = 800,
                BackgroundPollingTimeoutSeconds = 60,
                MaxConcurrentScoringRequests = 1
            });
        var runtimeService = new FakeOpenAiRuntimeSettingsService(
            new OpenAiRuntimeSettingsProfile(
                "runtime-model",
                "https://runtime.example/v1",
                75,
                true,
                1500,
                180,
                4,
                "token-1"));
        var resolver = new OpenAiEffectiveSecurityOptionsResolver(
            configuration,
            optionsMonitor,
            new FixedOpenAiRuntimeApiKeyService(),
            runtimeService);

        var resolved = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("json-model", resolved.Model);
        Assert.Equal("https://json-config.example/v1", resolved.BaseUrl);
        Assert.Equal(40, resolved.RequestTimeoutSeconds);
        Assert.False(resolved.UseBackgroundMode);
        Assert.Equal(800, resolved.BackgroundPollingIntervalMilliseconds);
        Assert.Equal(60, resolved.BackgroundPollingTimeoutSeconds);
        Assert.Equal(1, resolved.MaxConcurrentScoringRequests);
    }

    [Fact]
    public async Task ResolveAsyncUsesCommandLineOverridesOverRuntimeSettings()
    {
        var configuration = BuildConfiguration(
            """
            {
              "OpenAI": {
                "Security": {
                  "Model": "json-model",
                  "BaseUrl": "https://json-config.example/v1",
                  "RequestTimeoutSeconds": 40,
                  "UseBackgroundMode": false,
                  "BackgroundPollingIntervalMilliseconds": 800,
                  "BackgroundPollingTimeoutSeconds": 60,
                  "MaxConcurrentScoringRequests": 1
                }
              }
            }
            """,
            "OpenAI:Security:Model=cli-model",
            "OpenAI:Security:MaxConcurrentScoringRequests=9");

        var optionsMonitor = new MutableOptionsMonitor<OpenAiSecurityOptions>(
            new OpenAiSecurityOptions
            {
                Model = "cli-model",
                BaseUrl = "https://json-config.example/v1",
                RequestTimeoutSeconds = 40,
                UseBackgroundMode = false,
                BackgroundPollingIntervalMilliseconds = 800,
                BackgroundPollingTimeoutSeconds = 60,
                MaxConcurrentScoringRequests = 9
            });
        var runtimeService = new FakeOpenAiRuntimeSettingsService(
            new OpenAiRuntimeSettingsProfile(
                "runtime-model",
                "https://runtime.example/v1",
                75,
                true,
                1500,
                180,
                4,
                "token-1"));
        var resolver = new OpenAiEffectiveSecurityOptionsResolver(
            configuration,
            optionsMonitor,
            new FixedOpenAiRuntimeApiKeyService(),
            runtimeService);

        var resolved = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("cli-model", resolved.Model);
        Assert.Equal(9, resolved.MaxConcurrentScoringRequests);
        Assert.Equal("https://json-config.example/v1", resolved.BaseUrl);
        Assert.Equal(40, resolved.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task ResolveAsyncUsesRuntimeApiKeyOverrideWhenConfigured()
    {
        var configuration = BuildConfiguration(
            """
            {
              "OpenAI": {
                "Security": {
                  "Model": "m1"
                }
              }
            }
            """);
        var optionsMonitor = new MutableOptionsMonitor<OpenAiSecurityOptions>(
            new OpenAiSecurityOptions
            {
                Model = "m1"
            });
        var runtimeService = new FakeOpenAiRuntimeSettingsService(
            new OpenAiRuntimeSettingsProfile(
                "runtime-model",
                "https://runtime.example/v1",
                45,
                true,
                1500,
                120,
                2,
                "token-1"));
        var runtimeApiKeyService = new FixedOpenAiRuntimeApiKeyService("runtime-key");
        var resolver = new OpenAiEffectiveSecurityOptionsResolver(
            configuration,
            optionsMonitor,
            runtimeApiKeyService,
            runtimeService);

        var resolved = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("runtime-key", resolved.ApiKey);
    }

    [Fact]
    public async Task ResolveAsyncReturnsEmptyApiKeyWhenRuntimeApiKeyIsMissing()
    {
        var configuration = BuildConfiguration(
            """
            {
              "OpenAI": {
                "Security": {
                  "Model": "m1"
                }
              }
            }
            """);
        var optionsMonitor = new MutableOptionsMonitor<OpenAiSecurityOptions>(
            new OpenAiSecurityOptions
            {
                Model = "m1"
            });
        var runtimeService = new FakeOpenAiRuntimeSettingsService(
            new OpenAiRuntimeSettingsProfile(
                "runtime-model",
                "https://runtime.example/v1",
                45,
                true,
                1500,
                120,
                2,
                "token-1"));
        var resolver = new OpenAiEffectiveSecurityOptionsResolver(
            configuration,
            optionsMonitor,
            new FixedOpenAiRuntimeApiKeyService(),
            runtimeService);

        var resolved = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal(string.Empty, resolved.ApiKey);
    }

    private static IConfiguration BuildConfiguration(string json, params string[] commandLineArgs)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var builder = new ConfigurationBuilder().AddJsonStream(stream);

        if (commandLineArgs.Length > 0)
        {
            builder.AddCommandLine(commandLineArgs);
        }

        return builder.Build();
    }

    private sealed class MutableOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
        where TOptions : class
    {
        public MutableOptionsMonitor(TOptions currentValue)
        {
            CurrentValue = currentValue;
        }

        public TOptions CurrentValue { get; set; }

        public TOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<TOptions, string?> listener)
        {
            return NoopDisposable.Instance;
        }
    }

    private sealed class FakeOpenAiRuntimeSettingsService : IOpenAiRuntimeSettingsService
    {
        public FakeOpenAiRuntimeSettingsService(OpenAiRuntimeSettingsProfile activeProfile)
        {
            ActiveProfile = activeProfile;
        }

        public OpenAiRuntimeSettingsProfile ActiveProfile { get; set; }

        public Task<OpenAiRuntimeSettingsProfile> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ActiveProfile);
        }

        public Task<OpenAiRuntimeSettingsProfile> SaveAsync(
            OpenAiRuntimeSettingsProfile profile,
            CancellationToken cancellationToken)
        {
            ActiveProfile = profile;
            return Task.FromResult(ActiveProfile);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
