using System.Net;

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

    public LinkedInApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LinkedInApiResponse> GetAsync(
        Uri requestUri,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        requestMessage.Version = HttpVersion.Version20;
        requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        foreach (var header in headers)
        {
            if (SkippedHeaders.Contains(header.Key))
            {
                continue;
            }

            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new LinkedInApiResponse((int)response.StatusCode, response.IsSuccessStatusCode, body);
    }
}
