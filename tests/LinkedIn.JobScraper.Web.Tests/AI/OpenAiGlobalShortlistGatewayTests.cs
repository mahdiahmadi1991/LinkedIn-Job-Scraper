using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class OpenAiGlobalShortlistGatewayTests
{
    [Fact]
    public async Task RankBatchAsyncParsesRecommendationsAndNormalizesScores()
    {
        var client = new FakeOpenAiResponsesClient();
        client.CreateResponses.Enqueue(
            new OpenAiResponseSnapshot(
                "response-1",
                OpenAiResponseStatus.Completed,
                """
                {
                  "recommendations": [
                    {
                      "candidateId": "candidate-1",
                      "score": 130,
                      "confidence": -5,
                      "recommendationReason": "Strong backend match",
                      "concerns": "Timezone overlap required"
                    },
                    {
                      "candidateId": "candidate-unknown",
                      "score": 50,
                      "confidence": 50,
                      "recommendationReason": "Unknown candidate",
                      "concerns": "Should be ignored"
                    },
                    {
                      "candidateId": "candidate-1",
                      "score": 99,
                      "confidence": 99,
                      "recommendationReason": "Duplicate should be ignored",
                      "concerns": "Duplicate"
                    }
                  ]
                }
                """,
                null,
                null,
                false));

        var gateway = CreateGateway(client);
        var result = await gateway.RankBatchAsync(CreateBatchRequest(), CancellationToken.None);

        Assert.True(result.CanRank);
        var recommendations = Assert.IsAssignableFrom<IReadOnlyList<AiGlobalShortlistBatchRecommendation>>(result.Recommendations);
        var recommendation = Assert.Single(recommendations);
        Assert.Equal("candidate-1", recommendation.CandidateId);
        Assert.Equal(100, recommendation.Score);
        Assert.Equal(0, recommendation.Confidence);
    }

    [Fact]
    public async Task RankBatchAsyncReturnsFailureWhenNoUsableRecommendationsArePresent()
    {
        var client = new FakeOpenAiResponsesClient();
        client.CreateResponses.Enqueue(
            new OpenAiResponseSnapshot(
                "response-1",
                OpenAiResponseStatus.Completed,
                """
                {
                  "recommendations": [
                    {
                      "candidateId": "outside-batch",
                      "score": 85,
                      "confidence": 70,
                      "recommendationReason": "Not from this batch",
                      "concerns": "Ignore"
                    }
                  ]
                }
                """,
                null,
                null,
                false));

        var gateway = CreateGateway(client);
        var result = await gateway.RankBatchAsync(CreateBatchRequest(), CancellationToken.None);

        Assert.False(result.CanRank);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Contains("usable recommendations", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RankBatchAsyncPollsBackgroundResponsesUntilCompleted()
    {
        var client = new FakeOpenAiResponsesClient();
        client.CreateResponses.Enqueue(
            new OpenAiResponseSnapshot(
                "response-1",
                OpenAiResponseStatus.Queued,
                null,
                null,
                null,
                true));
        client.GetResponses.Enqueue(
            new OpenAiResponseSnapshot(
                "response-1",
                OpenAiResponseStatus.Completed,
                """
                {
                  "recommendations": [
                    {
                      "candidateId": "candidate-1",
                      "score": 92,
                      "confidence": 89,
                      "recommendationReason": "Great fit",
                      "concerns": "None"
                    }
                  ]
                }
                """,
                null,
                null,
                true));

        var gateway = CreateGateway(
            client,
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                UseBackgroundMode = true,
                BackgroundPollingIntervalMilliseconds = 1,
                BackgroundPollingTimeoutSeconds = 1
            });

        var result = await gateway.RankBatchAsync(CreateBatchRequest(), CancellationToken.None);

        Assert.True(result.CanRank);
        Assert.Single(client.GetRequests);
        Assert.True(Assert.Single(client.CreateRequests).BackgroundModeEnabled);
    }

    private static OpenAiGlobalShortlistGateway CreateGateway(
        FakeOpenAiResponsesClient client,
        OpenAiSecurityOptions? options = null)
    {
        return new OpenAiGlobalShortlistGateway(
            client,
            Options.Create(
                options ?? new OpenAiSecurityOptions
                {
                    ApiKey = "test-key",
                    Model = "gpt-5-mini",
                    UseBackgroundMode = false
                }),
            NullLogger<OpenAiGlobalShortlistGateway>.Instance);
    }

    private static AiGlobalShortlistBatchGatewayRequest CreateBatchRequest()
    {
        return new AiGlobalShortlistBatchGatewayRequest(
            [
                new AiGlobalShortlistBatchCandidate(
                    "candidate-1",
                    "4379963196",
                    "Senior .NET Engineer",
                    "Strong C# and distributed systems background.",
                    "Acme",
                    "Limassol",
                    "Full-time",
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddHours(-2),
                    88,
                    "Review"),
                new AiGlobalShortlistBatchCandidate(
                    "candidate-2",
                    "4379963197",
                    "Platform Engineer",
                    "Backend platform role.",
                    "Beta",
                    "Nicosia",
                    "Full-time",
                    DateTimeOffset.UtcNow.AddDays(-2),
                    DateTimeOffset.UtcNow.AddHours(-3),
                    74,
                    "Review")
            ],
            "Behavior",
            "Priority",
            "Exclusion",
            "en",
            2);
    }

    private sealed class FakeOpenAiResponsesClient : IOpenAiResponsesClient
    {
        public Queue<OpenAiResponsesRequest> CreateRequests { get; } = new();

        public Queue<(string ResponseId, TimeSpan Timeout)> GetRequests { get; } = new();

        public Queue<OpenAiResponseSnapshot> CreateResponses { get; } = new();

        public Queue<OpenAiResponseSnapshot> GetResponses { get; } = new();

        public Task<OpenAiResponseSnapshot> CreateResponseAsync(
            OpenAiResponsesRequest request,
            TimeSpan requestTimeout,
            CancellationToken cancellationToken)
        {
            CreateRequests.Enqueue(request);

            if (CreateResponses.TryDequeue(out var response))
            {
                return Task.FromResult(response);
            }

            throw new InvalidOperationException("No fake create response was queued.");
        }

        public Task<OpenAiResponseSnapshot> GetResponseAsync(
            string responseId,
            TimeSpan requestTimeout,
            CancellationToken cancellationToken)
        {
            GetRequests.Enqueue((responseId, requestTimeout));

            if (GetResponses.TryDequeue(out var response))
            {
                return Task.FromResult(response);
            }

            throw new InvalidOperationException("No fake get response was queued.");
        }
    }
}
