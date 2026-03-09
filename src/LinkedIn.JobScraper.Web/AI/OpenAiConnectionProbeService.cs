using System.Net;
using System.Net.Http.Headers;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiConnectionProbeService : IOpenAiConnectionProbeService
{
    private readonly HttpClient _httpClient;

    public OpenAiConnectionProbeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OpenAiConnectionProbeResult> ProbeAsync(
        string apiKey,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new OpenAiConnectionProbeResult(false, "OpenAI API key is required.");
        }

        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return new OpenAiConnectionProbeResult(false, "OpenAI base URL must be a valid absolute HTTP or HTTPS URL.");
        }

        var modelsEndpoint = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/models", UriKind.Absolute);

        using var request = new HttpRequestMessage(HttpMethod.Get, modelsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new OpenAiConnectionProbeResult(true, "OpenAI API key verification succeeded.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new OpenAiConnectionProbeResult(
                    false,
                    "OpenAI API key was rejected by the server. Verify the key and permissions.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new OpenAiConnectionProbeResult(
                    true,
                    "OpenAI API key was accepted, but the probe request is currently rate-limited.");
            }

            if ((int)response.StatusCode >= 500)
            {
                return new OpenAiConnectionProbeResult(
                    false,
                    "OpenAI servers are currently unavailable. Retry in a few moments.");
            }

            return new OpenAiConnectionProbeResult(
                false,
                $"OpenAI API probe failed with status code {(int)response.StatusCode} ({response.StatusCode}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OpenAiConnectionProbeResult(
                false,
                "OpenAI API probe timed out. Check connectivity and try again.");
        }
        catch (HttpRequestException)
        {
            return new OpenAiConnectionProbeResult(
                false,
                "OpenAI API probe failed due to a network or TLS error.");
        }
    }
}
