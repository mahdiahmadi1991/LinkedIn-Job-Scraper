namespace LinkedIn.JobScraper.Web.AI;

public static class AiOutputLanguage
{
    public const string English = "en";
    public const string Persian = "fa";

    public static string GetDirection(string? languageCode)
    {
        return string.Equals(languageCode, Persian, StringComparison.OrdinalIgnoreCase)
            ? "rtl"
            : "ltr";
    }

    public static string GetDisplayName(string? languageCode)
    {
        return string.Equals(languageCode, Persian, StringComparison.OrdinalIgnoreCase)
            ? "فارسی"
            : "English";
    }

    public static string Normalize(string? languageCode)
    {
        return string.Equals(languageCode, Persian, StringComparison.OrdinalIgnoreCase)
            ? Persian
            : English;
    }

    public static string GetPromptLabel(string? languageCode)
    {
        return string.Equals(languageCode, Persian, StringComparison.OrdinalIgnoreCase)
            ? "Persian (Farsi)"
            : "English";
    }
}
