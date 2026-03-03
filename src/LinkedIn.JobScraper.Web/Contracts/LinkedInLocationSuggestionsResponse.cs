namespace LinkedIn.JobScraper.Web.Contracts;

public sealed record LinkedInLocationSuggestionsResponse(
    IReadOnlyList<LinkedInLocationSuggestionResponseItem> Suggestions);

public sealed record LinkedInLocationSuggestionResponseItem(
    string GeoId,
    string DisplayName);
