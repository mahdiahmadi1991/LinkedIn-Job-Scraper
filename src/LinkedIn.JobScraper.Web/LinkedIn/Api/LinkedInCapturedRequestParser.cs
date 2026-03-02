namespace LinkedIn.JobScraper.Web.LinkedIn.Api;

internal static class LinkedInCapturedRequestParser
{
    public static ParsedLinkedInCapturedRequest Parse(string curlText)
    {
        var lines = curlText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return ParsedLinkedInCapturedRequest.Invalid("The cURL sample file is empty.");
        }

        var firstLine = lines[0];
        var firstQuoteIndex = firstLine.IndexOf("^\"", StringComparison.Ordinal);

        if (firstQuoteIndex < 0)
        {
            return ParsedLinkedInCapturedRequest.Invalid("Could not find the request URL in the first cURL line.");
        }

        var lastQuoteIndex = firstLine.LastIndexOf("^\"", StringComparison.Ordinal);

        if (lastQuoteIndex <= firstQuoteIndex)
        {
            return ParsedLinkedInCapturedRequest.Invalid("Could not parse the quoted request URL.");
        }

        var rawUrl = firstLine.Substring(firstQuoteIndex + 2, lastQuoteIndex - (firstQuoteIndex + 2));
        var decodedUrl = DecodeWindowsCurlEscapes(rawUrl);

        if (!Uri.TryCreate(decodedUrl, UriKind.Absolute, out var url))
        {
            return ParsedLinkedInCapturedRequest.Invalid("The decoded request URL is not a valid absolute URI.");
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

        return ParsedLinkedInCapturedRequest.Valid(url, headers);
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

internal sealed class ParsedLinkedInCapturedRequest
{
    private ParsedLinkedInCapturedRequest(
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

    public static ParsedLinkedInCapturedRequest Invalid(string errorMessage) =>
        new(false, null, new Dictionary<string, string>(), errorMessage);

    public static ParsedLinkedInCapturedRequest Valid(
        Uri url,
        IReadOnlyDictionary<string, string> headers) =>
        new(true, url, headers, null);
}
