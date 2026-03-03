using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Logging;

public sealed class PerRunFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private const int LogMessageMaxLength = 4000;
    private static readonly string[] InformationalDiagnosticsCategories =
    [
        "LinkedIn.JobScraper.Web.LinkedIn.Api.LinkedInApiClient",
        "LinkedIn.JobScraper.Web.LinkedIn.Search.LinkedInJobSearchService",
        "LinkedIn.JobScraper.Web.Jobs.JobImportService"
    ];

    private readonly StreamWriter _writer;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, PerRunFileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly bool _enableInformationalDiagnostics;
    private IExternalScopeProvider? _scopeProvider;

    public PerRunFileLoggerProvider(string filePath, bool enableInformationalDiagnostics = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(
            new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
        _enableInformationalDiagnostics = enableInformationalDiagnostics;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, static (name, provider) => new PerRunFileLogger(name, provider), this);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    private void WriteLogEntry(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (ShouldSkip(categoryName, logLevel))
        {
            return;
        }

        var sanitizedMessage = SensitiveDataRedaction.SanitizeForMessage(message, LogMessageMaxLength);
        var sanitizedException = exception is null
            ? null
            : SensitiveDataRedaction.SanitizeForMessage(exception.ToString(), LogMessageMaxLength);

        string? correlationId = null;
        _scopeProvider?.ForEachScope(
            static (scope, state) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> properties)
                {
                    foreach (var property in properties)
                    {
                        if (string.Equals(property.Key, "CorrelationId", StringComparison.Ordinal) &&
                            property.Value is not null)
                        {
                            state.CorrelationId = property.Value.ToString();
                            return;
                        }
                    }
                }
            },
            new CorrelationCaptureState(value => correlationId = value));

        var logLine =
            $"[{DateTimeOffset.UtcNow:O}] [{logLevel}] [{categoryName}] [EventId:{eventId.Id}]";

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            logLine += $" [CorrelationId:{correlationId}]";
        }

        logLine += $" {sanitizedMessage}";

        lock (_writeLock)
        {
            _writer.WriteLine(logLine);

            if (!string.IsNullOrWhiteSpace(sanitizedException))
            {
                _writer.WriteLine($"    {sanitizedException}");
            }
        }
    }

    private bool ShouldSkip(string categoryName, LogLevel logLevel)
    {
        if (logLevel == LogLevel.None || logLevel == LogLevel.Trace || logLevel == LogLevel.Debug)
        {
            return true;
        }

        if (logLevel == LogLevel.Information &&
            !ShouldIncludeInformationalDiagnosticsCategory(categoryName))
        {
            return true;
        }

        return string.Equals(categoryName, "Microsoft.EntityFrameworkCore.Database.Command", StringComparison.Ordinal)
            && logLevel < LogLevel.Warning;
    }

    private bool ShouldIncludeInformationalDiagnosticsCategory(string categoryName)
    {
        if (!_enableInformationalDiagnostics)
        {
            return false;
        }

        return InformationalDiagnosticsCategories.Any(
            configuredCategory => string.Equals(configuredCategory, categoryName, StringComparison.Ordinal));
    }

    private sealed class PerRunFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly PerRunFileLoggerProvider _provider;

        public PerRunFileLogger(string categoryName, PerRunFileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _provider._scopeProvider?.Push(state) ?? NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return !_provider.ShouldSkip(_categoryName, logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.WriteLogEntry(_categoryName, logLevel, eventId, formatter(state, exception), exception);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class CorrelationCaptureState
    {
        private readonly Action<string?> _setter;

        public CorrelationCaptureState(Action<string?> setter)
        {
            _setter = setter;
        }

        public string? CorrelationId
        {
            get => null;
            set => _setter(value);
        }
    }
}
