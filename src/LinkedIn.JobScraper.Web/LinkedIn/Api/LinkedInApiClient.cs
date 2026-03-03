using System.Net;

using System.Diagnostics;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.LinkedIn.Api;

public sealed class LinkedInApiClient : ILinkedInApiClient
{
    private static readonly HashSet<string> SkippedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept-Encoding",
        "Connection",
        "Host",
        "TE"
    };

    private readonly HttpClient _httpClient;
    private readonly LinkedInFetchDiagnosticsOptions _fetchDiagnosticsOptions;
    private readonly ILogger<LinkedInApiClient> _logger;

    public LinkedInApiClient(
        HttpClient httpClient,
        IOptions<LinkedInFetchDiagnosticsOptions> fetchDiagnosticsOptions,
        ILogger<LinkedInApiClient> logger)
    {
        _httpClient = httpClient;
        _fetchDiagnosticsOptions = fetchDiagnosticsOptions.Value;
        _logger = logger;
    }

    public async Task<LinkedInApiResponse> GetAsync(
        Uri requestUri,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        requestMessage.Version = HttpVersion.Version20;
        requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        var diagnosticsEnabled = _fetchDiagnosticsOptions.Enabled;
        var requestPathAndQuery = SensitiveDataRedaction.SanitizeForMessage(
            requestUri.PathAndQuery,
            maxLength: 1200);

        foreach (var header in headers)
        {
            if (SkippedHeaders.Contains(header.Key))
            {
                continue;
            }

            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (diagnosticsEnabled)
        {
            _logger.LogInformation(
                "LinkedIn API GET started. RequestPathAndQuery={RequestPathAndQuery}, HeaderCount={HeaderCount}",
                requestPathAndQuery,
                headers.Count);
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (diagnosticsEnabled)
        {
            _logger.LogInformation(
                "LinkedIn API GET completed. RequestPathAndQuery={RequestPathAndQuery}, StatusCode={StatusCode}, Success={Success}, BodyLength={BodyLength}, ElapsedMilliseconds={ElapsedMilliseconds}",
                requestPathAndQuery,
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                body.Length,
                stopwatch.ElapsedMilliseconds);

            if (_fetchDiagnosticsOptions.LogResponseBodies)
            {
                _logger.LogInformation(
                    "LinkedIn API GET response body sample. RequestPathAndQuery={RequestPathAndQuery}, BodySample={BodySample}",
                    requestPathAndQuery,
                    SensitiveDataRedaction.SanitizeForMessage(
                        body,
                        _fetchDiagnosticsOptions.GetResponseBodyMaxLength()));
            }
        }

        return new LinkedInApiResponse((int)response.StatusCode, response.IsSuccessStatusCode, body);
    }
}
