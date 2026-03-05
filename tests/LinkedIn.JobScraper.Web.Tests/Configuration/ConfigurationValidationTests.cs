using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void SqlServerOptionsValidatorReturnsFailureWhenConnectionStringIsMissing()
    {
        var validator = new SqlServerOptionsValidator();

        var result = validator.Validate(
            Options.DefaultName,
            new SqlServerOptions
            {
                ConnectionString = ""
            });

        Assert.True(result.Failed);
        Assert.Contains("SqlServer:ConnectionString", Assert.Single(result.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerOptionsThrowsActionableMessageWhenConnectionStringIsMissing()
    {
        var options = new SqlServerOptions
        {
            ConnectionString = ""
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetRequiredConnectionString());

        Assert.Contains("SqlServer:ConnectionString", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets set", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredSqlServerConnectionStringProviderUsesOptionsValidation()
    {
        var provider = new ConfiguredSqlServerConnectionStringProvider(
            Options.Create(
                new SqlServerOptions
                {
                    ConnectionString = ""
                }));

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredConnectionString());

        Assert.Contains("SqlServer:ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenAiSecurityOptionsValidatorAllowsMissingCredentialsButRejectsInvalidRuntimeValues()
    {
        var validator = new OpenAiSecurityOptionsValidator();

        var validWithoutCredentials = validator.Validate(
            Options.DefaultName,
            new OpenAiSecurityOptions
            {
                ApiKey = "",
                Model = "",
                RequestTimeoutSeconds = 45
            });

        Assert.True(validWithoutCredentials.Succeeded);

        var invalidTimeout = validator.Validate(
            Options.DefaultName,
            new OpenAiSecurityOptions
            {
                RequestTimeoutSeconds = 0
            });

        Assert.True(invalidTimeout.Failed);
        Assert.Contains("OpenAI:Security:RequestTimeoutSeconds", Assert.Single(invalidTimeout.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedInAndWorkflowOptionsValidatorsRejectInvalidConfiguredValues()
    {
        var diagnosticsValidation = new LinkedInFetchDiagnosticsOptionsValidator().Validate(
            Options.DefaultName,
            new LinkedInFetchDiagnosticsOptions
            {
                ResponseBodyMaxLength = 0
            });

        Assert.True(diagnosticsValidation.Failed);

        var limitsValidation = new LinkedInFetchLimitsOptionsValidator().Validate(
            Options.DefaultName,
            new LinkedInFetchLimitsOptions
            {
                SearchPageCap = 0
            });

        Assert.True(limitsValidation.Failed);

        var incrementalValidation = new LinkedInIncrementalFetchOptionsValidator().Validate(
            Options.DefaultName,
            new LinkedInIncrementalFetchOptions
            {
                MinimumPagesBeforeStop = 0
            });

        Assert.True(incrementalValidation.Failed);

        var requestValidation = new LinkedInRequestOptionsValidator().Validate(
            Options.DefaultName,
            new LinkedInRequestOptions
            {
                GraphQlQueryId = "",
                GeoTypeaheadQueryId = ""
            });

        Assert.True(requestValidation.Failed);

        var requestSafetyValidation = new LinkedInRequestSafetyOptionsValidator().Validate(
            Options.DefaultName,
            new LinkedInRequestSafetyOptions
            {
                MinimumDelayMilliseconds = 0
            });

        Assert.True(requestSafetyValidation.Failed);

        var workflowValidation = new JobsWorkflowOptionsValidator().Validate(
            Options.DefaultName,
            new JobsWorkflowOptions
            {
                EnrichmentBatchSize = 0
            });

        Assert.True(workflowValidation.Failed);

        var staleRefreshValidation = new JobsWorkflowOptionsValidator().Validate(
            Options.DefaultName,
            new JobsWorkflowOptions
            {
                StaleDetailRefreshRunCap = -1
            });

        Assert.True(staleRefreshValidation.Failed);

        var detailResyncValidation = new JobsWorkflowOptionsValidator().Validate(
            Options.DefaultName,
            new JobsWorkflowOptions
            {
                DetailResyncAfterHours = 0
            });

        Assert.True(detailResyncValidation.Failed);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsActionableMessageWhenApiKeyIsMissing()
    {
        var gateway = CreateGateway(
            options: new OpenAiSecurityOptions
            {
                ApiKey = "",
                Model = "gpt-5-mini"
            });

        var result = await gateway.ScoreAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Contains("OpenAI:Security:ApiKey", result.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsActionableMessageWhenModelIsMissing()
    {
        var gateway = CreateGateway(
            options: new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = ""
            });

        var result = await gateway.ScoreAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Contains("OpenAI:Security:Model", result.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsActionableMessageWhenTimeoutConfigIsInvalid()
    {
        var gateway = CreateGateway(
            options: new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                RequestTimeoutSeconds = 0
            });

        var result = await gateway.ScoreAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Contains("OpenAI:Security:RequestTimeoutSeconds", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsGracefulFailureWhenRequestTimesOut()
    {
        var client = new FakeOpenAiResponsesClient
        {
            CreateException = new OpenAiResponsesTimeoutException(TimeSpan.FromSeconds(45))
        };

        var gateway = CreateGateway(client);
        var result = await gateway.ScoreAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, result.StatusCode);
        Assert.Contains("timed out after 45 seconds", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayPollsBackgroundResponsesUntilCompleted()
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
                {"score":91,"label":"StrongMatch","summary":"Good fit","whyMatched":"Matches priorities","concerns":"Few concerns"}
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
                BackgroundPollingIntervalMilliseconds = 1,
                BackgroundPollingTimeoutSeconds = 1
            });

        var result = await gateway.ScoreAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.CanScore);
        Assert.Equal(91, result.Score);
        Assert.Single(client.GetRequests);
        Assert.True(Assert.Single(client.CreateRequests).BackgroundModeEnabled);
    }

    [Fact]
    public async Task OpenAiJobScoringGatewayReturnsGracefulFailureWhenBackgroundModeDoesNotCompleteInTime()
    {
        var client = new FakeOpenAiResponsesClient
        {
            DefaultGetResponse = new OpenAiResponseSnapshot(
                "response-1",
                OpenAiResponseStatus.InProgress,
                null,
                null,
                null,
                true)
        };
        client.CreateResponses.Enqueue(
            new OpenAiResponseSnapshot(
                "response-1",
                OpenAiResponseStatus.Queued,
                null,
                null,
                null,
                true));

        var gateway = CreateGateway(
            client,
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                BackgroundPollingIntervalMilliseconds = 1,
                BackgroundPollingTimeoutSeconds = 1
            });

        var result = await gateway.ScoreAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.CanScore);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, result.StatusCode);
        Assert.Contains("timed out after 1 second", result.Message, StringComparison.Ordinal);
        Assert.NotEmpty(client.GetRequests);
    }

    private static OpenAiJobScoringGateway CreateGateway(
        FakeOpenAiResponsesClient? client = null,
        OpenAiSecurityOptions? options = null)
    {
        return new OpenAiJobScoringGateway(
            client ?? new FakeOpenAiResponsesClient(),
            Options.Create(
                options ?? new OpenAiSecurityOptions
                {
                    ApiKey = "test-key",
                    Model = "gpt-5-mini"
                }),
            NullLogger<OpenAiJobScoringGateway>.Instance);
    }

    private static JobScoringGatewayRequest CreateRequest()
    {
        return new JobScoringGatewayRequest(
            "Title",
            "Description",
            "Behavior",
            "Priority",
            "Exclusion",
            "en",
            "Company",
            "Location",
            "Full-time");
    }

    private sealed class FakeOpenAiResponsesClient : IOpenAiResponsesClient
    {
        public Exception? CreateException { get; init; }

        public Exception? GetException { get; init; }

        public OpenAiResponseSnapshot? DefaultGetResponse { get; init; }

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

            if (CreateException is not null)
            {
                throw CreateException;
            }

            if (CreateResponses.TryDequeue(out var response))
            {
                return Task.FromResult(response);
            }

            throw new InvalidOperationException("No fake OpenAI create response was configured.");
        }

        public Task<OpenAiResponseSnapshot> GetResponseAsync(
            string responseId,
            TimeSpan requestTimeout,
            CancellationToken cancellationToken)
        {
            GetRequests.Enqueue((responseId, requestTimeout));

            if (GetException is not null)
            {
                throw GetException;
            }

            if (GetResponses.TryDequeue(out var response))
            {
                return Task.FromResult(response);
            }

            if (DefaultGetResponse is not null)
            {
                return Task.FromResult(DefaultGetResponse);
            }

            throw new InvalidOperationException("No fake OpenAI get response was configured.");
        }
    }
}
