namespace LinkedIn.JobScraper.Web.Persistence;

public static class ConcurrencyTokenCodec
{
    public static string? Encode(byte[]? rowVersion)
    {
        return rowVersion is null || rowVersion.Length == 0
            ? null
            : Convert.ToBase64String(rowVersion);
    }

    public static byte[]? Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(token);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "The submitted settings state is invalid. Reload the page and try again.",
                exception);
        }
    }
}
