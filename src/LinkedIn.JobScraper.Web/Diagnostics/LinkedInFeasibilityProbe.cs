using System.Net;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using LinkedIn.JobScraper.Web.LinkedIn.Session;

namespace LinkedIn.JobScraper.Web.Diagnostics;

public sealed class LinkedInFeasibilityProbe
{
    private readonly ILinkedInApiClient _linkedInApiClient;
    private readonly ILinkedInSessionVerificationService _linkedInSessionVerificationService;
    private readonly ILogger<LinkedInFeasibilityProbe> _logger;

    public LinkedInFeasibilityProbe(
        ILinkedInApiClient linkedInApiClient,
        ILinkedInSessionVerificationService linkedInSessionVerificationService,
        ILogger<LinkedInFeasibilityProbe> logger)
    {
        _linkedInApiClient = linkedInApiClient;
        _linkedInSessionVerificationService = linkedInSessionVerificationService;
        _logger = logger;
    }

    public async Task<LinkedInFeasibilityResult> RunAsync(CancellationToken cancellationToken)
    {
        var response = await _linkedInApiClient.GetAsync(
            new Uri("https://www.linkedin.com/robots.txt", UriKind.Absolute),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Accept"] = "text/plain"
            },
            cancellationToken);

        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            Log.LinkedInProbeReturnedNonSuccessStatusCode(_logger, response.StatusCode);

            return LinkedInFeasibilityResult.Failed(
                $"LinkedIn public reachability check failed with HTTP {response.StatusCode}.",
                response.StatusCode,
                Truncate(response.Body, 600));
        }

        return LinkedInFeasibilityResult.Succeeded(
            response.StatusCode,
            "LinkedIn public reachability check succeeded. Stored session was not evaluated.");
    }

    public async Task<LinkedInFeasibilityResult> RunUsingStoredSessionAsync(CancellationToken cancellationToken)
    {
        var verificationResult = await _linkedInSessionVerificationService.VerifyCurrentAsync(cancellationToken);

        if (!verificationResult.Success)
        {
            return LinkedInFeasibilityResult.Failed(verificationResult.Message, verificationResult.StatusCode);
        }

        return LinkedInFeasibilityResult.Succeeded(
            verificationResult.StatusCode ?? StatusCodes.Status200OK,
            verificationResult.Message);
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
        string message) =>
        new(
            true,
            message,
            statusCode,
            0,
            0,
            Array.Empty<string>(),
            null);
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "LinkedIn feasibility probe returned non-success status code {StatusCode}.")]
    public static partial void LinkedInProbeReturnedNonSuccessStatusCode(ILogger logger, int statusCode);

}
