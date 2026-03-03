using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.AI;

public sealed class OpenAiJobScoringGateway : IJobScoringGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiJobScoringGateway> _logger;
    private readonly IOptions<OpenAiSecurityOptions> _securityOptions;

    public OpenAiJobScoringGateway(
        HttpClient httpClient,
        IOptions<OpenAiSecurityOptions> securityOptions,
        ILogger<OpenAiJobScoringGateway> logger)
    {
        _httpClient = httpClient;
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
            var requestUri = BuildResponsesUri(securityOptions.BaseUrl);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", securityOptions.ApiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreateRequestBody(securityOptions.Model, request), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Log.OpenAiScoringReturnedNonSuccessStatusCode(_logger, (int)response.StatusCode);
                var responseErrorMessage = TryReadErrorMessage(responseBody);
                var message = responseErrorMessage is null
                    ? $"OpenAI scoring failed with HTTP {(int)response.StatusCode}."
                    : $"OpenAI scoring failed with HTTP {(int)response.StatusCode}: {SensitiveDataRedaction.SanitizeForMessage(responseErrorMessage)}";

                return new JobScoringGatewayResult(
                    false,
                    message,
                    (int)response.StatusCode);
            }

            if (!TryExtractScoreResult(responseBody, out var result, out var errorMessage))
            {
                return new JobScoringGatewayResult(
                    false,
                    errorMessage ?? "OpenAI scoring response could not be parsed.");
            }

            return result!;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.OpenAiScoringRequestFailed(_logger, exception);

            return new JobScoringGatewayResult(
                false,
                $"OpenAI scoring failed: {SensitiveDataRedaction.SanitizeForMessage(exception.Message)}");
        }
    }

    private static Uri BuildResponsesUri(string? baseUrl)
    {
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.TrimEnd('/');

        return new Uri($"{normalizedBaseUrl}/responses", UriKind.Absolute);
    }

    private static object CreateRequestBody(string model, JobScoringGatewayRequest request)
    {
        var userPrompt = BuildUserPrompt(request);

        return new
        {
            model,
            input = new object[]
            {
                new
                {
                    role = "developer",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text =
                                "Score the job for fit. Return concise structured JSON only. Penalize mismatches and missing evidence. Use the provided behavioral profile and honor the requested output language for all natural-language fields."
                        }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = userPrompt
                        }
                    }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "job_score",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            score = new
                            {
                                type = "integer",
                                minimum = 0,
                                maximum = 100
                            },
                            label = new
                            {
                                type = "string",
                                @enum = new[] { "StrongMatch", "Review", "Skip" }
                            },
                            summary = new { type = "string" },
                            whyMatched = new { type = "string" },
                            concerns = new { type = "string" }
                        },
                        required = new[] { "score", "label", "summary", "whyMatched", "concerns" }
                    }
                }
            }
        };
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
        string responseBody,
        out JobScoringGatewayResult? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var outputText = TryReadOutputText(root);

            if (string.IsNullOrWhiteSpace(outputText))
            {
                errorMessage = "OpenAI scoring response did not contain output text.";
                return false;
            }

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

    private static string? TryReadOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputTextNode) &&
            outputTextNode.ValueKind == JsonValueKind.String)
        {
            return outputTextNode.GetString();
        }

        if (!root.TryGetProperty("output", out var outputNode) ||
            outputNode.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in outputNode.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentNode) ||
                contentNode.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in contentNode.EnumerateArray())
            {
                if (!contentItem.TryGetProperty("type", out var typeNode) ||
                    !string.Equals(typeNode.GetString(), "output_text", StringComparison.Ordinal) ||
                    !contentItem.TryGetProperty("text", out var textNode) ||
                    textNode.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                return textNode.GetString();
            }
        }

        return null;
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

    private static string? TryReadErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty("error", out var errorNode) ||
                errorNode.ValueKind != JsonValueKind.Object ||
                !errorNode.TryGetProperty("message", out var messageNode) ||
                messageNode.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return messageNode.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
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
