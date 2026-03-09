using System.Net;
using System.Net.Http;
using System.Threading;
using LinkedIn.JobScraper.Web.AI;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class OpenAiConnectionProbeServiceTests
{
    [Fact]
    public async Task ProbeAsyncReturnsSuccessWhenModelsEndpointRespondsOk()
    {
        var service = CreateService(HttpStatusCode.OK);

        var result = await service.ProbeAsync(
            "sk-test-key",
            "https://api.openai.com/v1",
            CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProbeAsyncReturnsFailureWhenApiKeyIsRejected()
    {
        var service = CreateService(HttpStatusCode.Unauthorized);

        var result = await service.ProbeAsync(
            "sk-invalid-key",
            "https://api.openai.com/v1",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("rejected", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsyncTreatsRateLimitAsReachableEndpoint()
    {
        var service = CreateService(HttpStatusCode.TooManyRequests);

        var result = await service.ProbeAsync(
            "sk-valid-key",
            "https://api.openai.com/v1",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("rate-limited", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OpenAiConnectionProbeService CreateService(HttpStatusCode statusCode)
    {
        var httpClient = new HttpClient(new FixedResponseHandler(statusCode))
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        return new OpenAiConnectionProbeService(httpClient);
    }

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FixedResponseHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            return Task.FromResult(response);
        }
    }
}
