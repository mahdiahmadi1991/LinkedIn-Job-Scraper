using System.Globalization;
using System.Text;
using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiJobScoringGateway : IJobScoringGateway
{
    private const string DeveloperInstruction =
        "Score the job for fit. Return concise structured JSON only. Penalize mismatches and missing evidence. Use the provided behavioral profile and honor the requested output language for all natural-language fields.";

    private const string JobScoreJsonSchema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "score": {
              "type": "integer",
              "minimum": 0,
              "maximum": 100
            },
            "label": {
              "type": "string",
              "enum": ["StrongMatch", "Review", "Skip"]
            },
            "summary": {
              "type": "string"
            },
            "whyMatched": {
              "type": "string"
            },
            "concerns": {
              "type": "string"
            }
          },
          "required": ["score", "label", "summary", "whyMatched", "concerns"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<OpenAiJobScoringGateway> _logger;
    private readonly IOpenAiResponsesClient _responsesClient;
    private readonly IOptions<OpenAiSecurityOptions> _securityOptions;

    public OpenAiJobScoringGateway(
        IOpenAiResponsesClient responsesClient,
        IOptions<OpenAiSecurityOptions> securityOptions,
        ILogger<OpenAiJobScoringGateway> logger)
    {
        _responsesClient = responsesClient;
        _securityOptions = securityOptions;
        _logger = logger;
    }

    public async Task<JobScoringGatewayResult> ScoreAsync(
        JobScoringGatewayRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var securityOptions = _securityOptions.Value;
        var validationError = securityOptions.ValidateForScoring();

        if (validationError is not null)
        {
            return new JobScoringGatewayResult(false, validationError);
        }

        if (string.IsNullOrWhiteSpace(request.JobDescription))
        {
            return new JobScoringGatewayResult(false, "Job description is required before scoring.");
        }

        try
        {
            var response = await _responsesClient.CreateResponseAsync(
                CreateResponsesRequest(securityOptions, request),
                securityOptions.GetRequestTimeout(),
                cancellationToken);

            var finalResponse = await AwaitTerminalResponseAsync(response, securityOptions, cancellationToken);
            return TryTranslateCompletedResponse(finalResponse, out var completedResult)
                ? completedResult!
                : CreateFailureResult(finalResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OpenAiResponsesTimeoutException exception)
        {
            Log.OpenAiScoringRequestFailed(_logger, exception);

            return new JobScoringGatewayResult(
                false,
                $"OpenAI scoring timed out after {FormatTimeout(exception.Timeout)}.",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (OpenAiResponsesRequestException exception)
        {
            Log.OpenAiScoringReturnedNonSuccessStatusCode(_logger, exception.StatusCode);

            return new JobScoringGatewayResult(
                false,
                $"OpenAI scoring failed with HTTP {exception.StatusCode}: {exception.Message}",
                exception.StatusCode);
        }
        catch (Exception exception)
        {
            Log.OpenAiScoringRequestFailed(_logger, exception);

            return new JobScoringGatewayResult(
                false,
                $"OpenAI scoring failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}");
        }
    }

    private static OpenAiResponsesRequest CreateResponsesRequest(
        OpenAiSecurityOptions securityOptions,
        JobScoringGatewayRequest request)
    {
        return new OpenAiResponsesRequest(
            securityOptions.Model,
            DeveloperInstruction,
            BuildUserPrompt(request),
            "job_score",
            JobScoreJsonSchema,
            securityOptions.UseBackgroundMode);
    }

    private async Task<OpenAiResponseSnapshot> AwaitTerminalResponseAsync(
        OpenAiResponseSnapshot response,
        OpenAiSecurityOptions securityOptions,
        CancellationToken cancellationToken)
    {
        if (!securityOptions.UseBackgroundMode ||
            response.Status is not (OpenAiResponseStatus.Queued or OpenAiResponseStatus.InProgress) ||
            string.IsNullOrWhiteSpace(response.ResponseId))
        {
            return response;
        }

        var deadline = DateTimeOffset.UtcNow + securityOptions.GetBackgroundPollingTimeout();
        var pollInterval = securityOptions.GetBackgroundPollingInterval();
        var responseId = response.ResponseId;

        while (response.Status is OpenAiResponseStatus.Queued or OpenAiResponseStatus.InProgress)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                throw new OpenAiResponsesTimeoutException(securityOptions.GetBackgroundPollingTimeout());
            }

            var nextDelay = remaining < pollInterval
                ? remaining
                : pollInterval;

            await Task.Delay(nextDelay, cancellationToken);

            response = await _responsesClient.GetResponseAsync(
                responseId,
                securityOptions.GetRequestTimeout(),
                cancellationToken);
        }

        return response;
    }

    private static bool TryTranslateCompletedResponse(
        OpenAiResponseSnapshot response,
        out JobScoringGatewayResult? result)
    {
        result = null;

        if (response.Status is not (OpenAiResponseStatus.Completed or OpenAiResponseStatus.Unknown))
        {
            return false;
        }

        if (!TryExtractScoreResult(response.OutputText, out result, out var errorMessage))
        {
            result = new JobScoringGatewayResult(
                false,
                errorMessage ?? "OpenAI scoring response could not be parsed.");
        }

        return true;
    }

    private static JobScoringGatewayResult CreateFailureResult(OpenAiResponseSnapshot response)
    {
        var message = response.Status switch
        {
            OpenAiResponseStatus.Cancelled => "OpenAI scoring was cancelled before completion.",
            OpenAiResponseStatus.Incomplete => CreateIncompleteMessage(response),
            OpenAiResponseStatus.Failed => CreateFailureMessage(response),
            OpenAiResponseStatus.Queued => "OpenAI scoring did not complete and is still queued.",
            OpenAiResponseStatus.InProgress => "OpenAI scoring did not complete and is still in progress.",
            _ => CreateFailureMessage(response)
        };

        return new JobScoringGatewayResult(
            false,
            message,
            StatusCodes.Status502BadGateway);
    }

    private static string CreateFailureMessage(OpenAiResponseSnapshot response)
    {
        if (string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            return "OpenAI scoring failed.";
        }

        return $"OpenAI scoring failed: {SensitiveDataRedaction.SanitizeForMessage(response.ErrorMessage)}";
    }

    private static string CreateIncompleteMessage(OpenAiResponseSnapshot response)
    {
        if (string.IsNullOrWhiteSpace(response.IncompleteReason))
        {
            return "OpenAI scoring was incomplete.";
        }

        return $"OpenAI scoring was incomplete: {SensitiveDataRedaction.SanitizeForMessage(response.IncompleteReason)}";
    }

    private static string FormatTimeout(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return "the configured timeout";
        }

        var totalSeconds = timeout.TotalSeconds;
        var formattedValue = Math.Abs(totalSeconds % 1) < double.Epsilon
            ? ((int)totalSeconds).ToString(CultureInfo.InvariantCulture)
            : totalSeconds.ToString("0.#", CultureInfo.InvariantCulture);

        return $"{formattedValue} second{(formattedValue == "1" ? string.Empty : "s")}";
    }

    private static string BuildUserPrompt(JobScoringGatewayRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Behavioral instructions:");
        builder.AppendLine(request.BehavioralInstructions);
        builder.AppendLine();
        builder.AppendLine("Priority signals:");
        builder.AppendLine(request.PrioritySignals);
        builder.AppendLine();
        builder.AppendLine("Exclusion signals:");
        builder.AppendLine(request.ExclusionSignals);
        builder.AppendLine();
        builder.Append("Output language for summary fields: ");
        builder.AppendLine(AiOutputLanguage.GetPromptLabel(request.OutputLanguageCode));
        builder.AppendLine("Write summary, whyMatched, and concerns in that language.");
        builder.AppendLine();
        builder.AppendLine("Job data:");
        builder.Append("Title: ");
        builder.AppendLine(request.JobTitle);
        builder.Append("Company: ");
        builder.AppendLine(request.CompanyName ?? "Unknown");
        builder.Append("Location: ");
        builder.AppendLine(request.LocationName ?? "Unknown");
        builder.Append("Employment status: ");
        builder.AppendLine(request.EmploymentStatus ?? "Unknown");
        builder.AppendLine("Description:");
        builder.AppendLine(request.JobDescription);

        return builder.ToString();
    }

    private static bool TryExtractScoreResult(
        string? outputText,
        out JobScoringGatewayResult? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(outputText))
        {
            errorMessage = "OpenAI scoring response did not contain output text.";
            return false;
        }

        try
        {
            using var payload = JsonDocument.Parse(outputText);
            var payloadRoot = payload.RootElement;

            if (!payloadRoot.TryGetProperty("score", out var scoreNode) ||
                !scoreNode.TryGetInt32(out var score))
            {
                errorMessage = "OpenAI scoring output did not contain a valid score.";
                return false;
            }

            var label = ReadRequiredString(payloadRoot, "label", out errorMessage);

            if (errorMessage is not null)
            {
                return false;
            }

            var summary = ReadRequiredString(payloadRoot, "summary", out errorMessage);

            if (errorMessage is not null)
            {
                return false;
            }

            var whyMatched = ReadRequiredString(payloadRoot, "whyMatched", out errorMessage);

            if (errorMessage is not null)
            {
                return false;
            }

            var concerns = ReadRequiredString(payloadRoot, "concerns", out errorMessage);

            if (errorMessage is not null)
            {
                return false;
            }

            result = new JobScoringGatewayResult(
                true,
                "OpenAI scoring succeeded.",
                StatusCodes.Status200OK,
                score,
                label,
                summary,
                whyMatched,
                concerns);

            return true;
        }
        catch (JsonException)
        {
            errorMessage = "OpenAI scoring response was not valid JSON.";
            return false;
        }
    }

    private static string ReadRequiredString(
        JsonElement element,
        string propertyName,
        out string? errorMessage)
    {
        errorMessage = null;

        if (!element.TryGetProperty(propertyName, out var propertyNode) ||
            propertyNode.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"OpenAI scoring output did not contain '{propertyName}'.";
            return string.Empty;
        }

        return propertyNode.GetString() ?? string.Empty;
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "OpenAI scoring returned non-success status code {StatusCode}.")]
    public static partial void OpenAiScoringReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "OpenAI scoring request failed.")]
    public static partial void OpenAiScoringRequestFailed(ILogger logger, Exception exception);
}
