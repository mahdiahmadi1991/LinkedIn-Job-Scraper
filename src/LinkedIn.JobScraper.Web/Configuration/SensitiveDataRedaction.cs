using System.Text.RegularExpressions;

namespace LinkedIn.JobScraper.Web.Configuration;

public static partial class SensitiveDataRedaction
{
    private const int DefaultMaxLength = 220;
    private const string EmptyFallbackMessage = "An internal error occurred.";

    public static string SanitizeForMessage(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmptyFallbackMessage;
        }

        var sanitized = AuthorizationBearerAssignmentPattern().Replace(
            value,
            static match => $"{match.Groups["name"].Value}=[redacted]");

        sanitized = SensitiveAssignmentPattern().Replace(
            sanitized,
            static match => $"{match.Groups["name"].Value}=[redacted]");

        sanitized = BearerTokenPattern().Replace(sanitized, "Bearer [redacted]");
        sanitized = SecretTokenPattern().Replace(sanitized, "[redacted]");
        sanitized = sanitized.Trim();

        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return $"{sanitized[..maxLength]}...";
    }

    [GeneratedRegex(
        "(?i)(?<name>authorization)\\s*[:=]\\s*Bearer\\s+[^\\s;,]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationBearerAssignmentPattern();

    [GeneratedRegex(
        "(?i)(?<name>authorization|cookie|set-cookie|csrf-token|api[-_ ]?key|token|secret|password)\\s*[:=]\\s*(?<value>(?:Bearer\\s+)?[^\\s;,]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex(
        "(?i)\\bBearer\\s+[A-Za-z0-9._-]+\\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(
        "(?i)\\b(sk-[A-Za-z0-9_-]+|li_at=[^;\\s]+|jsessionid=\"?[^\"]+\"?)\\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretTokenPattern();
}
