namespace LinkedIn.JobScraper.Web.LinkedIn.Api;

internal static class LinkedInCapturedRequestParser
{
    public static ParsedLinkedInCapturedRequest Parse(string curlText)
    {
        if (curlText.Contains("^\"", StringComparison.Ordinal))
        {
            return ParseWindowsCurl(curlText);
        }

        return ParseBashCurl(curlText);
    }

    private static ParsedLinkedInCapturedRequest ParseWindowsCurl(string curlText)
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

    private static ParsedLinkedInCapturedRequest ParseBashCurl(string curlText)
    {
        if (string.IsNullOrWhiteSpace(curlText))
        {
            return ParsedLinkedInCapturedRequest.Invalid("The cURL text is empty.");
        }

        var tokenizeResult = TokenizeBashCurl(curlText);

        if (!tokenizeResult.Success)
        {
            return ParsedLinkedInCapturedRequest.Invalid(tokenizeResult.ErrorMessage!);
        }

        var tokens = tokenizeResult.Tokens;

        if (tokens.Count == 0)
        {
            return ParsedLinkedInCapturedRequest.Invalid("The cURL text is empty.");
        }

        if (!string.Equals(tokens[0], "curl", StringComparison.Ordinal))
        {
            return ParsedLinkedInCapturedRequest.Invalid("Only 'Copy as cURL' input is supported.");
        }

        Uri? requestUri = null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (string.Equals(token, "-H", StringComparison.Ordinal) ||
                string.Equals(token, "--header", StringComparison.Ordinal))
            {
                if (index + 1 >= tokens.Count)
                {
                    return ParsedLinkedInCapturedRequest.Invalid("A header flag in the cURL text is missing its value.");
                }

                index++;

                if (!TryAddHeader(headers, tokens[index]))
                {
                    return ParsedLinkedInCapturedRequest.Invalid("A header in the cURL text could not be parsed.");
                }

                continue;
            }

            if (token.Length > 0 && token[0] == '-')
            {
                continue;
            }

            if (requestUri is not null)
            {
                continue;
            }

            if (!Uri.TryCreate(token, UriKind.Absolute, out requestUri))
            {
                return ParsedLinkedInCapturedRequest.Invalid("The cURL request URL is not a valid absolute URI.");
            }
        }

        if (requestUri is null)
        {
            return ParsedLinkedInCapturedRequest.Invalid("Could not find the request URL in the cURL text.");
        }

        return ParsedLinkedInCapturedRequest.Valid(requestUri, headers);
    }

    private static ShellTokenizationResult TokenizeBashCurl(string curlText)
    {
        var tokens = new List<string>();
        var buffer = new List<char>();
        var state = ShellTokenizationState.None;

        for (var index = 0; index < curlText.Length; index++)
        {
            var current = curlText[index];

            switch (state)
            {
                case ShellTokenizationState.None:
                    if (char.IsWhiteSpace(current))
                    {
                        FlushToken(tokens, buffer);
                        continue;
                    }

                    if (current == '\'')
                    {
                        state = ShellTokenizationState.SingleQuoted;
                        continue;
                    }

                    if (current == '"')
                    {
                        state = ShellTokenizationState.DoubleQuoted;
                        continue;
                    }

                    if (current == '\\')
                    {
                        if (index + 1 >= curlText.Length)
                        {
                            return ShellTokenizationResult.Invalid("The cURL text ends with an incomplete escape.");
                        }

                        index++;
                        var escaped = curlText[index];

                        if (escaped == '\r')
                        {
                            if (index + 1 < curlText.Length && curlText[index + 1] == '\n')
                            {
                                index++;
                            }

                            continue;
                        }

                        if (escaped == '\n')
                        {
                            continue;
                        }

                        buffer.Add(escaped);
                        continue;
                    }

                    buffer.Add(current);
                    continue;

                case ShellTokenizationState.SingleQuoted:
                    if (current == '\'')
                    {
                        state = ShellTokenizationState.None;
                        continue;
                    }

                    buffer.Add(current);
                    continue;

                case ShellTokenizationState.DoubleQuoted:
                    if (current == '"')
                    {
                        state = ShellTokenizationState.None;
                        continue;
                    }

                    if (current == '\\')
                    {
                        if (index + 1 >= curlText.Length)
                        {
                            return ShellTokenizationResult.Invalid("The cURL text ends with an incomplete escape.");
                        }

                        index++;
                        var escaped = curlText[index];

                        if (escaped == '\r')
                        {
                            if (index + 1 < curlText.Length && curlText[index + 1] == '\n')
                            {
                                index++;
                            }

                            continue;
                        }

                        if (escaped == '\n')
                        {
                            continue;
                        }

                        buffer.Add(escaped);
                        continue;
                    }

                    buffer.Add(current);
                    continue;
            }
        }

        if (state != ShellTokenizationState.None)
        {
            return ShellTokenizationResult.Invalid("The cURL text contains an unterminated quoted value.");
        }

        FlushToken(tokens, buffer);

        return ShellTokenizationResult.Valid(tokens);
    }

    private static void FlushToken(List<string> tokens, List<char> buffer)
    {
        if (buffer.Count == 0)
        {
            return;
        }

        tokens.Add(new string([.. buffer]));
        buffer.Clear();
    }

    private static bool TryAddHeader(Dictionary<string, string> headers, string headerText)
    {
        var separatorIndex = headerText.IndexOf(':');

        if (separatorIndex <= 0)
        {
            return false;
        }

        var name = headerText[..separatorIndex].Trim();
        var value = headerText[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        headers[name] = value;
        return true;
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

    private enum ShellTokenizationState
    {
        None,
        SingleQuoted,
        DoubleQuoted
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

internal sealed class ShellTokenizationResult
{
    private ShellTokenizationResult(bool success, IReadOnlyList<string> tokens, string? errorMessage)
    {
        Success = success;
        Tokens = tokens;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }

    public IReadOnlyList<string> Tokens { get; }

    public string? ErrorMessage { get; }

    public static ShellTokenizationResult Invalid(string errorMessage) =>
        new(false, Array.Empty<string>(), errorMessage);

    public static ShellTokenizationResult Valid(IReadOnlyList<string> tokens) =>
        new(true, tokens, null);
}
