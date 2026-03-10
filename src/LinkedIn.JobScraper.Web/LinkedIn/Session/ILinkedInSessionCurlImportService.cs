using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.LinkedIn.Api;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInSessionCurlImportService
{
    Task<LinkedInSessionCurlImportResult> ImportAsync(string? curlText, CancellationToken cancellationToken);
}

public sealed class LinkedInSessionCurlImportService : ILinkedInSessionCurlImportService
{
    private const string CurlImportSource = "CurlImport";

    private readonly ILogger<LinkedInSessionCurlImportService> _logger;
    private readonly ILinkedInSessionStore _sessionStore;
    private readonly ILinkedInSessionVerificationService _sessionVerificationService;

    public LinkedInSessionCurlImportService(
        ILinkedInSessionStore sessionStore,
        ILinkedInSessionVerificationService sessionVerificationService,
        ILogger<LinkedInSessionCurlImportService> logger)
    {
        _sessionStore = sessionStore;
        _sessionVerificationService = sessionVerificationService;
        _logger = logger;
    }

    public async Task<LinkedInSessionCurlImportResult> ImportAsync(string? curlText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(curlText))
        {
            return LinkedInSessionCurlImportResult.Failed("Paste a cURL command before importing.");
        }

        var parsedRequest = LinkedInCapturedRequestParser.Parse(curlText);

        if (!parsedRequest.IsValid || parsedRequest.Url is null)
        {
            var friendlyMessage = BuildParserFailureMessage(parsedRequest.ErrorMessage, curlText);
            Log.CurlImportParseFailed(_logger, parsedRequest.ErrorMessage ?? "unknown_parse_error");

            return LinkedInSessionCurlImportResult.Failed(
                friendlyMessage);
        }

        if (!IsSupportedLinkedInUrl(parsedRequest.Url))
        {
            Log.CurlImportRejectedUnsupportedUrl(_logger, parsedRequest.Url.Host);
            return LinkedInSessionCurlImportResult.Failed(
                "The pasted cURL command must target a linkedin.com request.");
        }

        var sanitizedHeaders = new Dictionary<string, string>(
            LinkedInSessionHeaderSanitizer.SanitizeForStorage(parsedRequest.Headers),
            StringComparer.OrdinalIgnoreCase);

        if (!sanitizedHeaders.TryGetValue("Cookie", out var cookieHeader) || string.IsNullOrWhiteSpace(cookieHeader))
        {
            Log.CurlImportMissingCookieHeader(_logger);
            return LinkedInSessionCurlImportResult.Failed(
                "The pasted request does not include LinkedIn cookies. Copy a LinkedIn /voyager/api request as cURL while logged in, then try again.");
        }

        if (!ContainsRequiredCookie(cookieHeader, "li_at") ||
            !ContainsRequiredCookie(cookieHeader, "JSESSIONID"))
        {
            Log.CurlImportMissingRequiredCookies(_logger);
            return LinkedInSessionCurlImportResult.Failed(
                "The pasted request is missing required LinkedIn session cookies (li_at and JSESSIONID). Make sure you copy from an authenticated LinkedIn tab.");
        }

        if (!sanitizedHeaders.TryGetValue("csrf-token", out var csrfToken) || string.IsNullOrWhiteSpace(csrfToken))
        {
            if (TryExtractCsrfTokenFromCookie(cookieHeader, out var extractedCsrfToken))
            {
                sanitizedHeaders["csrf-token"] = extractedCsrfToken;
                Log.CurlImportDerivedCsrfFromCookie(_logger);
            }
            else
            {
                Log.CurlImportMissingCsrfToken(_logger);
                return LinkedInSessionCurlImportResult.Failed(
                    "The pasted request is missing csrf-token. Copy an authenticated LinkedIn /voyager/api request as cURL, not a page navigation request.");
            }
        }

        var snapshot = new LinkedInSessionSnapshot(
            sanitizedHeaders,
            DateTimeOffset.UtcNow,
            CurlImportSource);

        var verificationResult = await _sessionVerificationService.VerifyAsync(snapshot, cancellationToken);

        if (!verificationResult.Success)
        {
            Log.CurlImportVerificationFailed(_logger, verificationResult.StatusCode);
            return LinkedInSessionCurlImportResult.Failed(
                "LinkedIn rejected the imported session. Confirm you are logged in to LinkedIn, copy a fresh request, and try again.",
                verificationResult.StatusCode ?? StatusCodes.Status409Conflict);
        }

        await _sessionStore.SaveAsync(snapshot, cancellationToken);
        await _sessionStore.MarkCurrentValidatedAsync(DateTimeOffset.UtcNow, cancellationToken);
        Log.CurlImportSucceeded(_logger);

        return LinkedInSessionCurlImportResult.Succeeded(
            $"LinkedIn session was imported from cURL and verified successfully. {verificationResult.Message}");
    }

    private static string BuildParserFailureMessage(string? parserErrorMessage, string curlText)
    {
        var input = curlText.TrimStart();

        if (input.StartsWith("fetch(", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("await fetch(", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("Copy as fetch", StringComparison.OrdinalIgnoreCase))
        {
            return "This looks like 'Copy as fetch'. Use 'Copy as cURL' from browser Network tools and paste the full command that starts with curl.";
        }

        if (input.StartsWith("Invoke-WebRequest", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("-Headers @", StringComparison.OrdinalIgnoreCase))
        {
            return "PowerShell request format is not supported here. Use browser Network tools and copy as cURL.";
        }

        if (parserErrorMessage?.Contains("unterminated quoted value", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "The pasted cURL looks incomplete (broken quotes). Copy again as cURL and paste the entire command.";
        }

        if (parserErrorMessage?.Contains("Could not find the request URL", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Could not find a valid request URL in the pasted text. Copy the full request as cURL, including the URL.";
        }

        if (parserErrorMessage?.Contains("Only 'Copy as cURL' input is supported.", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Only full cURL commands are supported. In browser Network tools choose 'Copy as cURL' and paste it here.";
        }

        return "The pasted text could not be parsed as a cURL command. Copy a LinkedIn /voyager/api request as cURL and paste the full command.";
    }

    private static bool IsSupportedLinkedInUrl(Uri requestUri)
    {
        return requestUri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
               requestUri.Host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRequiredCookie(string cookieHeader, string cookieName)
    {
        return Regex.IsMatch(
            cookieHeader,
            $"(?i)(?:^|;)\\s*{Regex.Escape(cookieName)}=",
            RegexOptions.CultureInvariant);
    }

    private static bool TryExtractCsrfTokenFromCookie(string cookieHeader, out string csrfToken)
    {
        csrfToken = string.Empty;

        var match = Regex.Match(
            cookieHeader,
            "(?i)(?:^|;)\\s*JSESSIONID\\s*=\\s*\"?(?<value>[^\";]+)\"?",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return false;
        }

        var value = match.Groups["value"].Value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        csrfToken = value;
        return true;
    }
}

public sealed record LinkedInSessionCurlImportResult(
    bool Success,
    string Message,
    int StatusCode) : OperationResult(Success, Message)
{
    public static LinkedInSessionCurlImportResult Failed(
        string message,
        int statusCode = StatusCodes.Status400BadRequest) =>
        new(false, message, statusCode);

    public static LinkedInSessionCurlImportResult Succeeded(string message) =>
        new(true, message, StatusCodes.Status200OK);
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 2521,
        Level = LogLevel.Warning,
        Message = "LinkedIn cURL import parse failed. ParserError={ParserError}")]
    public static partial void CurlImportParseFailed(
        ILogger logger,
        string parserError);

    [LoggerMessage(
        EventId = 2522,
        Level = LogLevel.Warning,
        Message = "LinkedIn cURL import rejected unsupported URL host. Host={Host}")]
    public static partial void CurlImportRejectedUnsupportedUrl(
        ILogger logger,
        string host);

    [LoggerMessage(
        EventId = 2523,
        Level = LogLevel.Warning,
        Message = "LinkedIn cURL import failed because cookie header was missing.")]
    public static partial void CurlImportMissingCookieHeader(ILogger logger);

    [LoggerMessage(
        EventId = 2524,
        Level = LogLevel.Warning,
        Message = "LinkedIn cURL import failed because required cookies were missing.")]
    public static partial void CurlImportMissingRequiredCookies(ILogger logger);

    [LoggerMessage(
        EventId = 2525,
        Level = LogLevel.Information,
        Message = "LinkedIn cURL import derived csrf-token from JSESSIONID cookie.")]
    public static partial void CurlImportDerivedCsrfFromCookie(ILogger logger);

    [LoggerMessage(
        EventId = 2526,
        Level = LogLevel.Warning,
        Message = "LinkedIn cURL import failed because csrf-token header was missing and could not be derived.")]
    public static partial void CurlImportMissingCsrfToken(ILogger logger);

    [LoggerMessage(
        EventId = 2527,
        Level = LogLevel.Warning,
        Message = "LinkedIn cURL import verification failed. StatusCode={StatusCode}")]
    public static partial void CurlImportVerificationFailed(
        ILogger logger,
        int? statusCode);

    [LoggerMessage(
        EventId = 2528,
        Level = LogLevel.Information,
        Message = "LinkedIn cURL import succeeded and session was saved.")]
    public static partial void CurlImportSucceeded(ILogger logger);
}
