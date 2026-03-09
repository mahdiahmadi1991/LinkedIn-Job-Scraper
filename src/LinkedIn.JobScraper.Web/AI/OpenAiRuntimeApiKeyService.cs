using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiRuntimeApiKeyService : IOpenAiRuntimeApiKeyService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _secretsFilePath;

    private volatile bool _isLoaded;
    private string? _runtimeApiKey;

    public OpenAiRuntimeApiKeyService(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        _secretsFilePath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "openai-runtime-secrets.json");
    }

    public async Task<string?> GetActiveAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);

        return _runtimeApiKey;
    }

    public async Task SaveAsync(string apiKey, CancellationToken cancellationToken)
    {
        var normalizedApiKey = NormalizeApiKey(apiKey);
        if (string.IsNullOrWhiteSpace(normalizedApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is required.");
        }

        if (normalizedApiKey.Length > 512)
        {
            throw new InvalidOperationException("OpenAI API key must be 512 characters or fewer.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directoryPath = Path.GetDirectoryName(_secretsFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using var stream = File.Create(_secretsFilePath);
            await JsonSerializer.SerializeAsync(
                stream,
                new OpenAiRuntimeSecretsRecord
                {
                    ApiKey = normalizedApiKey,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                JsonOptions,
                cancellationToken);

            _runtimeApiKey = normalizedApiKey;
            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            if (File.Exists(_secretsFilePath))
            {
                await using var stream = File.OpenRead(_secretsFilePath);
                var record = await JsonSerializer.DeserializeAsync<OpenAiRuntimeSecretsRecord>(
                    stream,
                    JsonOptions,
                    cancellationToken);
                _runtimeApiKey = NormalizeApiKey(record?.ApiKey);
            }

            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? NormalizeApiKey(string? apiKey)
    {
        return string.IsNullOrWhiteSpace(apiKey)
            ? null
            : apiKey.Trim();
    }

    private sealed class OpenAiRuntimeSecretsRecord
    {
        public string? ApiKey { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
