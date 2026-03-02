using System.Net;
using System.Text.Json;
using LinkedIn.JobScraper.Web.LinkedIn.Api;

namespace LinkedIn.JobScraper.Web.Diagnostics;

public sealed class LinkedInFeasibilityProbe
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly ILogger<LinkedInFeasibilityProbe> _logger;

    public LinkedInFeasibilityProbe(
        ILinkedInApiClient linkedInApiClient,
        IWebHostEnvironment environment,
        ILogger<LinkedInFeasibilityProbe> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _environment = environment;
        _logger = logger;
    }

    public async Task<LinkedInFeasibilityResult> RunAsync(CancellationToken cancellationToken)
    {
        var requestFilePath = Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                "..",
                "..",
                "docs",
                "api-sample",
                "job-seaarch-request.txt"));

        if (!File.Exists(requestFilePath))
        {
            return LinkedInFeasibilityResult.Failed(
                $"Sample request file was not found at '{requestFilePath}'.");
        }

        var fileContent = await File.ReadAllTextAsync(requestFilePath, cancellationToken);
        var parsedRequest = LinkedInCurlRequestParser.Parse(fileContent);

        if (!parsedRequest.IsValid)
        {
            return LinkedInFeasibilityResult.Failed(parsedRequest.ErrorMessage!);
        }

        var response = await _linkedInApiClient.GetAsync(
            parsedRequest.Url!,
            parsedRequest.Headers,
            cancellationToken);

        var body = response.Body;

        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            Log.LinkedInProbeReturnedNonSuccessStatusCode(_logger, response.StatusCode);

            return LinkedInFeasibilityResult.Failed(
                $"LinkedIn request failed with HTTP {response.StatusCode}.",
                response.StatusCode,
                Truncate(body, 600));
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("data", out var dataNode))
            {
                return LinkedInFeasibilityResult.Failed(
                    "Response JSON did not contain a top-level 'data' node.",
                    response.StatusCode,
                    Truncate(body, 600));
            }

            var returnedCount = 0;
            var totalCount = 0;
            var jobCardUrns = new List<string>();

            if (dataNode.TryGetProperty("elements", out var elementsNode) &&
                elementsNode.ValueKind == JsonValueKind.Array)
            {
                returnedCount = elementsNode.GetArrayLength();

                foreach (var element in elementsNode.EnumerateArray())
                {
                    if (!element.TryGetProperty("jobCardUnion", out var jobCardUnionNode))
                    {
                        continue;
                    }

                    if (!jobCardUnionNode.TryGetProperty("*jobPostingCard", out var urnNode))
                    {
                        continue;
                    }

                    var urnValue = urnNode.GetString();

                    if (!string.IsNullOrWhiteSpace(urnValue))
                    {
                        jobCardUrns.Add(urnValue);
                    }
                }
            }

            if (dataNode.TryGetProperty("paging", out var pagingNode) &&
                pagingNode.TryGetProperty("total", out var totalNode) &&
                totalNode.TryGetInt32(out var parsedTotal))
            {
                totalCount = parsedTotal;
            }

            return LinkedInFeasibilityResult.Succeeded(
                (int)response.StatusCode,
                returnedCount,
                totalCount,
                jobCardUrns);
        }
        catch (JsonException exception)
        {
            Log.LinkedInProbeFailedToParseJson(_logger, exception);

            return LinkedInFeasibilityResult.Failed(
                    "LinkedIn response was not valid JSON.",
                response.StatusCode,
                Truncate(body, 600));
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..maxLength]}...";
    }
}

internal static class LinkedInCurlRequestParser
{
    public static ParsedCurlRequest Parse(string curlText)
    {
        var lines = curlText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return ParsedCurlRequest.Invalid("The cURL sample file is empty.");
        }

        var firstLine = lines[0];
        var firstQuoteIndex = firstLine.IndexOf("^\"", StringComparison.Ordinal);

        if (firstQuoteIndex < 0)
        {
            return ParsedCurlRequest.Invalid("Could not find the request URL in the first cURL line.");
        }

        var lastQuoteIndex = firstLine.LastIndexOf("^\"", StringComparison.Ordinal);

        if (lastQuoteIndex <= firstQuoteIndex)
        {
            return ParsedCurlRequest.Invalid("Could not parse the quoted request URL.");
        }

        var rawUrl = firstLine.Substring(firstQuoteIndex + 2, lastQuoteIndex - (firstQuoteIndex + 2));
        var decodedUrl = DecodeWindowsCurlEscapes(rawUrl);

        if (!Uri.TryCreate(decodedUrl, UriKind.Absolute, out var url))
        {
            return ParsedCurlRequest.Invalid("The decoded request URL is not a valid absolute URI.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (!line.StartsWith("-H ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var headerStart = line.IndexOf("^\"", StringComparison.Ordinal);
            var headerEnd = line.LastIndexOf("^\"", StringComparison.Ordinal);

            if (headerStart < 0 || headerEnd <= headerStart)
            {
                continue;
            }

            var rawHeader = line.Substring(headerStart + 2, headerEnd - (headerStart + 2));
            var decodedHeader = DecodeWindowsCurlEscapes(rawHeader);
            var separatorIndex = decodedHeader.IndexOf(':');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = decodedHeader[..separatorIndex].Trim();
            var value = decodedHeader[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            headers[name] = value;
        }

        return ParsedCurlRequest.Valid(url, headers);
    }

    private static string DecodeWindowsCurlEscapes(string value)
    {
        value = value.Replace("^\\^\"", "\"", StringComparison.Ordinal);

        var buffer = new List<char>(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (current == '^' && index + 1 < value.Length)
            {
                index++;
                buffer.Add(value[index]);
                continue;
            }

            buffer.Add(current);
        }

        return new string([.. buffer]);
    }
}

internal sealed class ParsedCurlRequest
{
    private ParsedCurlRequest(
        bool isValid,
        Uri? url,
        IReadOnlyDictionary<string, string> headers,
        string? errorMessage)
    {
        IsValid = isValid;
        Url = url;
        Headers = headers;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public Uri? Url { get; }

    public static ParsedCurlRequest Invalid(string errorMessage) =>
        new(false, null, new Dictionary<string, string>(), errorMessage);

    public static ParsedCurlRequest Valid(Uri url, IReadOnlyDictionary<string, string> headers) =>
        new(true, url, headers, null);
}

public sealed class LinkedInFeasibilityResult
{
    private LinkedInFeasibilityResult(
        bool success,
        string message,
        int? statusCode,
        int returnedCount,
        int totalCount,
        IReadOnlyList<string> sampledJobCardUrns,
        string? responsePreview)
    {
        Success = success;
        Message = message;
        StatusCode = statusCode;
        ReturnedCount = returnedCount;
        TotalCount = totalCount;
        SampledJobCardUrns = sampledJobCardUrns;
        ResponsePreview = responsePreview;
    }

    public string Message { get; }

    public string? ResponsePreview { get; }

    public IReadOnlyList<string> SampledJobCardUrns { get; }

    public bool Success { get; }

    public int ReturnedCount { get; }

    public int? StatusCode { get; }

    public int TotalCount { get; }

    public static LinkedInFeasibilityResult Failed(
        string message,
        int? statusCode = null,
        string? responsePreview = null) =>
        new(false, message, statusCode, 0, 0, Array.Empty<string>(), responsePreview);

    public static LinkedInFeasibilityResult Succeeded(
        int statusCode,
        int returnedCount,
        int totalCount,
        IReadOnlyList<string> sampledJobCardUrns) =>
        new(
            true,
            "LinkedIn job search replay succeeded.",
            statusCode,
            returnedCount,
            totalCount,
            sampledJobCardUrns,
            null);
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "LinkedIn feasibility probe returned non-success status code {StatusCode}.")]
    public static partial void LinkedInProbeReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Failed to parse LinkedIn feasibility response JSON.")]
    public static partial void LinkedInProbeFailedToParseJson(ILogger logger, Exception exception);
}
