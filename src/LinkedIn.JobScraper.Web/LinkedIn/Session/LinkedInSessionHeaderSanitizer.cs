namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

internal static class LinkedInSessionHeaderSanitizer
{
    private static readonly string[] AllowedStoredHeaderKeys =
    [
        "Cookie",
        "csrf-token",
        "User-Agent",
        "Referer",
        "Accept-Language",
        "x-li-lang",
        "x-li-track",
        "x-li-page-instance",
        "x-li-pem-metadata",
        "x-restli-protocol-version"
    ];

    public static IReadOnlyDictionary<string, string> SanitizeForStorage(IReadOnlyDictionary<string, string> headers)
    {
        var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in AllowedStoredHeaderKeys)
        {
            if (!headers.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            sanitized[key] = value.Trim();
        }

        return sanitized;
    }
}
