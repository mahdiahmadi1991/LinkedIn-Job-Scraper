using System.Globalization;
using System.Text;
using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiGlobalShortlistGateway : IAiGlobalShortlistGateway
{
    private const int MaxDescriptionLength = 1_600;

    private const string DeveloperInstruction =
        "Rank job candidates for overall fit and return strict JSON only. Compare candidates against each other, favor high-signal evidence, and penalize weak/noisy matches.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<OpenAiGlobalShortlistGateway> _logger;
    private readonly IOpenAiEffectiveSecurityOptionsResolver _openAiSecurityOptionsResolver;
    private readonly IOpenAiResponsesClient _responsesClient;

    public OpenAiGlobalShortlistGateway(
        IOpenAiResponsesClient responsesClient,
        IOpenAiEffectiveSecurityOptionsResolver openAiSecurityOptionsResolver,
        ILogger<OpenAiGlobalShortlistGateway> logger)
    {
        _responsesClient = responsesClient;
        _openAiSecurityOptionsResolver = openAiSecurityOptionsResolver;
        _logger = logger;
    }

    public async Task<AiGlobalShortlistBatchGatewayResult> RankBatchAsync(
        AiGlobalShortlistBatchGatewayRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        var securityOptions = await _openAiSecurityOptionsResolver.ResolveAsync(cancellationToken);

        if (request.Candidates.Count == 0)
        {
            return new AiGlobalShortlistBatchGatewayResult(
                true,
                "No candidates were provided for ranking.",
                StatusCodes.Status200OK,
                [],
                securityOptions.Model);
        }

        if (request.MaxRecommendations <= 0)
        {
            return new AiGlobalShortlistBatchGatewayResult(
                false,
                "Global shortlist max recommendations per batch must be greater than zero.",
                StatusCodes.Status500InternalServerError,
                null,
                securityOptions.Model,
                ErrorCode: "INVALID_REQUEST");
        }

        var validationError = securityOptions.ValidateForScoring();
        if (validationError is not null)
        {
            return new AiGlobalShortlistBatchGatewayResult(
                false,
                validationError,
                ErrorCode: "CONFIGURATION_INVALID",
                ModelName: securityOptions.Model);
        }

        try
        {
            var response = await _responsesClient.CreateResponseAsync(
                CreateResponsesRequest(securityOptions, request),
                securityOptions,
                securityOptions.GetRequestTimeout(),
                cancellationToken);

            var finalResponse = await AwaitTerminalResponseAsync(response, securityOptions, cancellationToken);
            return TryTranslateCompletedResponse(finalResponse, request, out var completedResult)
                ? completedResult!
                : CreateFailureResult(finalResponse, securityOptions.Model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OpenAiResponsesTimeoutException exception)
        {
            Log.OpenAiGlobalShortlistRequestTimedOut(_logger, exception);

            return new AiGlobalShortlistBatchGatewayResult(
                false,
                $"OpenAI global shortlist timed out after {FormatTimeout(exception.Timeout)}.",
                StatusCodes.Status504GatewayTimeout,
                null,
                securityOptions.Model,
                ErrorCode: "TIMEOUT");
        }
        catch (OpenAiResponsesRequestException exception)
        {
            Log.OpenAiGlobalShortlistRequestReturnedNonSuccessStatusCode(
                _logger,
                exception.StatusCode,
                exception);

            return new AiGlobalShortlistBatchGatewayResult(
                false,
                $"OpenAI global shortlist failed with HTTP {exception.StatusCode}: {exception.Message}",
                exception.StatusCode,
                null,
                securityOptions.Model,
                ErrorCode: $"HTTP_{exception.StatusCode}");
        }
        catch (Exception exception)
        {
            Log.OpenAiGlobalShortlistRequestFailed(_logger, exception);

            return new AiGlobalShortlistBatchGatewayResult(
                false,
                $"OpenAI global shortlist failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}",
                ErrorCode: "UNEXPECTED_ERROR",
                ModelName: securityOptions.Model);
        }
    }

    private static OpenAiResponsesRequest CreateResponsesRequest(
        OpenAiSecurityOptions securityOptions,
        AiGlobalShortlistBatchGatewayRequest request)
    {
        return new OpenAiResponsesRequest(
            securityOptions.Model,
            DeveloperInstruction,
            BuildUserPrompt(request),
            "global_shortlist_batch",
            BuildJsonSchema(request.MaxRecommendations),
            securityOptions.UseBackgroundMode);
    }

    private static string BuildUserPrompt(AiGlobalShortlistBatchGatewayRequest request)
    {
        var payload = request.Candidates
            .Select(
                candidate => new
                {
                    candidateId = candidate.CandidateId,
                    linkedInJobId = candidate.LinkedInJobId,
                    title = candidate.Title,
                    company = candidate.CompanyName,
                    location = candidate.LocationName,
                    employmentStatus = candidate.EmploymentStatus,
                    listedAtUtc = candidate.ListedAtUtc,
                    linkedInUpdatedAtUtc = candidate.LinkedInUpdatedAtUtc,
                    existingAiScore = candidate.ExistingAiScore,
                    existingAiLabel = candidate.ExistingAiLabel,
                    description = TruncateForPrompt(candidate.Description)
                })
            .ToArray();

        var candidatesJson = JsonSerializer.Serialize(payload, JsonOptions);

        var builder = new StringBuilder();
        builder.AppendLine("Use this behavioral profile to rank candidates globally.");
        builder.AppendLine("Return only JSON that matches the schema.");
        builder.AppendLine();
        builder.AppendLine("Behavioral instructions:");
        builder.AppendLine(request.BehavioralInstructions);
        builder.AppendLine();
        builder.AppendLine("Priority signals:");
        builder.AppendLine(request.PrioritySignals);
        builder.AppendLine();
        builder.AppendLine("Exclusion signals:");
        builder.AppendLine(request.ExclusionSignals);
        builder.AppendLine();
        builder.Append("Output language for recommendationReason and concerns: ");
        builder.AppendLine(AiOutputLanguage.GetPromptLabel(request.OutputLanguageCode));
        builder.AppendLine();
        builder.Append("Rules: return at most ");
        builder.Append(request.MaxRecommendations.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(" recommendations, sorted from best to worst. Use candidateId exactly as provided.");
        builder.AppendLine();
        builder.AppendLine("Candidates:");
        builder.AppendLine(candidatesJson);

        return builder.ToString();
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
                securityOptions,
                securityOptions.GetRequestTimeout(),
                cancellationToken);
        }

        return response;
    }

    private static bool TryTranslateCompletedResponse(
        OpenAiResponseSnapshot response,
        AiGlobalShortlistBatchGatewayRequest request,
        out AiGlobalShortlistBatchGatewayResult? result)
    {
        result = null;

        if (response.Status is not (OpenAiResponseStatus.Completed or OpenAiResponseStatus.Unknown))
        {
            return false;
        }

        if (!TryExtractRecommendations(response.OutputText, request, out var recommendations, out var errorMessage))
        {
            result = new AiGlobalShortlistBatchGatewayResult(
                false,
                errorMessage ?? "OpenAI global shortlist response could not be parsed.",
                StatusCodes.Status502BadGateway,
                ErrorCode: "INVALID_JSON_SCHEMA");
            return true;
        }

        result = new AiGlobalShortlistBatchGatewayResult(
            true,
            "OpenAI global shortlist batch ranking completed.",
            StatusCodes.Status200OK,
            recommendations);
        return true;
    }

    private static bool TryExtractRecommendations(
        string? outputText,
        AiGlobalShortlistBatchGatewayRequest request,
        out IReadOnlyList<AiGlobalShortlistBatchRecommendation>? recommendations,
        out string? errorMessage)
    {
        recommendations = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(outputText))
        {
            errorMessage = "OpenAI global shortlist response did not contain output text.";
            return false;
        }

        try
        {
            using var payload = JsonDocument.Parse(outputText);
            if (!payload.RootElement.TryGetProperty("recommendations", out var recommendationsNode) ||
                recommendationsNode.ValueKind != JsonValueKind.Array)
            {
                errorMessage = "OpenAI global shortlist output did not contain a valid recommendations array.";
                return false;
            }

            var knownCandidateIds = request.Candidates
                .Select(static candidate => candidate.CandidateId)
                .ToHashSet(StringComparer.Ordinal);
            var emittedIds = new HashSet<string>(StringComparer.Ordinal);
            var parsedRecommendations = new List<AiGlobalShortlistBatchRecommendation>();

            foreach (var recommendationNode in recommendationsNode.EnumerateArray())
            {
                var candidateId = ReadRequiredString(recommendationNode, "candidateId", out errorMessage);
                if (errorMessage is not null)
                {
                    continue;
                }

                if (!knownCandidateIds.Contains(candidateId) || !emittedIds.Add(candidateId))
                {
                    continue;
                }

                if (!TryReadInt32(recommendationNode, "score", out var score))
                {
                    continue;
                }

                if (!TryReadInt32(recommendationNode, "confidence", out var confidence))
                {
                    continue;
                }

                var recommendationReason = ReadRequiredString(recommendationNode, "recommendationReason", out errorMessage);
                if (errorMessage is not null)
                {
                    continue;
                }

                var concerns = ReadRequiredString(recommendationNode, "concerns", out errorMessage);
                if (errorMessage is not null)
                {
                    continue;
                }

                parsedRecommendations.Add(
                    new AiGlobalShortlistBatchRecommendation(
                        candidateId,
                        Math.Clamp(score, 0, 100),
                        Math.Clamp(confidence, 0, 100),
                        recommendationReason,
                        concerns));
            }

            if (parsedRecommendations.Count == 0)
            {
                errorMessage = "OpenAI global shortlist output did not contain usable recommendations.";
                return false;
            }

            recommendations = parsedRecommendations;
            return true;
        }
        catch (JsonException)
        {
            errorMessage = "OpenAI global shortlist response was not valid JSON.";
            return false;
        }
    }

    private static bool TryReadInt32(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;

        return element.TryGetProperty(propertyName, out var propertyNode) &&
               propertyNode.TryGetInt32(out value);
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
            errorMessage = $"OpenAI global shortlist output did not contain a valid '{propertyName}' field.";
            return string.Empty;
        }

        var value = propertyNode.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"OpenAI global shortlist output contained an empty '{propertyName}' value.";
            return string.Empty;
        }

        return value.Trim();
    }

    private static AiGlobalShortlistBatchGatewayResult CreateFailureResult(
        OpenAiResponseSnapshot response,
        string modelName)
    {
        var message = response.Status switch
        {
            OpenAiResponseStatus.Cancelled => "OpenAI global shortlist was cancelled before completion.",
            OpenAiResponseStatus.Incomplete => CreateIncompleteMessage(response),
            OpenAiResponseStatus.Failed => CreateFailureMessage(response),
            OpenAiResponseStatus.Queued => "OpenAI global shortlist did not complete and is still queued.",
            OpenAiResponseStatus.InProgress => "OpenAI global shortlist did not complete and is still in progress.",
            _ => CreateFailureMessage(response)
        };

        return new AiGlobalShortlistBatchGatewayResult(
            false,
            message,
            StatusCodes.Status502BadGateway,
            null,
            modelName,
            ErrorCode: response.Status.ToString().ToUpperInvariant());
    }

    private static string CreateFailureMessage(OpenAiResponseSnapshot response)
    {
        if (string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            return "OpenAI global shortlist failed.";
        }

        return $"OpenAI global shortlist failed: {SensitiveDataRedaction.SanitizeForMessage(response.ErrorMessage)}";
    }

    private static string CreateIncompleteMessage(OpenAiResponseSnapshot response)
    {
        if (string.IsNullOrWhiteSpace(response.IncompleteReason))
        {
            return "OpenAI global shortlist was incomplete.";
        }

        return $"OpenAI global shortlist was incomplete: {SensitiveDataRedaction.SanitizeForMessage(response.IncompleteReason)}";
    }

    private static string BuildJsonSchema(int maxRecommendations)
    {
        return $$"""
                 {
                   "type": "object",
                   "additionalProperties": false,
                   "properties": {
                     "recommendations": {
                       "type": "array",
                       "maxItems": {{maxRecommendations}},
                       "items": {
                         "type": "object",
                         "additionalProperties": false,
                         "properties": {
                           "candidateId": { "type": "string" },
                           "score": { "type": "integer", "minimum": 0, "maximum": 100 },
                           "confidence": { "type": "integer", "minimum": 0, "maximum": 100 },
                           "recommendationReason": { "type": "string" },
                           "concerns": { "type": "string" }
                         },
                         "required": ["candidateId", "score", "confidence", "recommendationReason", "concerns"]
                       }
                     }
                   },
                   "required": ["recommendations"]
                 }
                 """;
    }

    private static string TruncateForPrompt(string value)
    {
        if (value.Length <= MaxDescriptionLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, MaxDescriptionLength), "...");
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
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "OpenAI global shortlist request timed out.")]
    public static partial void OpenAiGlobalShortlistRequestTimedOut(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "OpenAI global shortlist request failed with HTTP {StatusCode}.")]
    public static partial void OpenAiGlobalShortlistRequestReturnedNonSuccessStatusCode(
        ILogger logger,
        int statusCode,
        Exception exception);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Error,
        Message = "OpenAI global shortlist request failed unexpectedly.")]
    public static partial void OpenAiGlobalShortlistRequestFailed(
        ILogger logger,
        Exception exception);
}
