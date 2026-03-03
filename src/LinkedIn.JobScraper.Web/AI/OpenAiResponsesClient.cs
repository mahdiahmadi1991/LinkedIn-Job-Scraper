using System.ClientModel;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;

namespace LinkedIn.JobScraper.Web.AI;

public interface IOpenAiResponsesClient
{
    Task<OpenAiResponseSnapshot> CreateResponseAsync(
        OpenAiResponsesRequest request,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken);

    Task<OpenAiResponseSnapshot> GetResponseAsync(
        string responseId,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken);
}

public sealed record OpenAiResponsesRequest(
    string Model,
    string DeveloperPrompt,
    string UserPrompt,
    string JsonSchemaName,
    string JsonSchema,
    bool BackgroundModeEnabled);

public sealed record OpenAiResponseSnapshot(
    string? ResponseId,
    OpenAiResponseStatus Status,
    string? OutputText,
    string? ErrorMessage,
    string? IncompleteReason,
    bool BackgroundModeEnabled);

public enum OpenAiResponseStatus
{
    Unknown = 0,
    Queued = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Incomplete = 5,
    Failed = 6
}

public sealed class OpenAiResponsesTimeoutException : TimeoutException
{
    public OpenAiResponsesTimeoutException(TimeSpan timeout)
        : base($"OpenAI request timed out after {timeout}.")
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}

public sealed class OpenAiResponsesRequestException : Exception
{
    public OpenAiResponsesRequestException(
        int statusCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

#pragma warning disable OPENAI001
public sealed class OpenAiSdkResponsesClient : IOpenAiResponsesClient
{
    private readonly ResponsesClient _client;

    public OpenAiSdkResponsesClient(IOptions<OpenAiSecurityOptions> securityOptions)
    {
        var options = securityOptions.Value;
        var clientOptions = new OpenAIClientOptions();

        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri($"{options.BaseUrl.TrimEnd('/')}/", UriKind.Absolute);
        }

        _client = new ResponsesClient(new ApiKeyCredential(options.ApiKey), clientOptions);
    }

    public Task<OpenAiResponseSnapshot> CreateResponseAsync(
        OpenAiResponsesRequest request,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        var options = new CreateResponseOptions
        {
            Model = request.Model,
            BackgroundModeEnabled = request.BackgroundModeEnabled ? true : null,
            StoredOutputEnabled = request.BackgroundModeEnabled ? true : null,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    request.JsonSchemaName,
                    BinaryData.FromString(request.JsonSchema),
                    jsonSchemaIsStrict: true)
            }
        };

        options.InputItems.Add(ResponseItem.CreateDeveloperMessageItem(request.DeveloperPrompt));
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.UserPrompt));

        return ExecuteAsync(
            async timeoutCancellationToken =>
            {
                ClientResult<ResponseResult> response = await _client.CreateResponseAsync(options, timeoutCancellationToken);
                return response;
            },
            requestTimeout,
            cancellationToken);
    }

    public Task<OpenAiResponseSnapshot> GetResponseAsync(
        string responseId,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async timeoutCancellationToken =>
            {
                ClientResult<ResponseResult> response = await _client.GetResponseAsync(responseId, timeoutCancellationToken);
                return response;
            },
            requestTimeout,
            cancellationToken);
    }

    private static async Task<OpenAiResponseSnapshot> ExecuteAsync(
        Func<CancellationToken, Task<ResponseResult>> operation,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (requestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCancellationSource.CancelAfter(requestTimeout);
        }

        try
        {
            var response = await operation(timeoutCancellationSource.Token);
            return MapResponse(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellationSource.IsCancellationRequested)
        {
            throw new OpenAiResponsesTimeoutException(requestTimeout);
        }
        catch (ClientResultException exception)
        {
            throw new OpenAiResponsesRequestException(
                exception.Status,
                SensitiveDataRedaction.SanitizeForMessage(exception.Message),
                exception);
        }
    }

    private static OpenAiResponseSnapshot MapResponse(ResponseResult response)
    {
        var status = MapStatus(response.Status);
        var outputText = response.GetOutputText();

        if (status == OpenAiResponseStatus.Unknown &&
            !string.IsNullOrWhiteSpace(outputText))
        {
            status = OpenAiResponseStatus.Completed;
        }

        return new OpenAiResponseSnapshot(
            response.Id,
            status,
            outputText,
            response.Error?.Message,
            response.IncompleteStatusDetails?.Reason?.ToString(),
            response.BackgroundModeEnabled == true);
    }

    private static OpenAiResponseStatus MapStatus(ResponseStatus? status)
    {
        return status switch
        {
            ResponseStatus.Queued => OpenAiResponseStatus.Queued,
            ResponseStatus.InProgress => OpenAiResponseStatus.InProgress,
            ResponseStatus.Completed => OpenAiResponseStatus.Completed,
            ResponseStatus.Cancelled => OpenAiResponseStatus.Cancelled,
            ResponseStatus.Incomplete => OpenAiResponseStatus.Incomplete,
            ResponseStatus.Failed => OpenAiResponseStatus.Failed,
            _ => OpenAiResponseStatus.Unknown
        };
    }
}
#pragma warning restore OPENAI001
