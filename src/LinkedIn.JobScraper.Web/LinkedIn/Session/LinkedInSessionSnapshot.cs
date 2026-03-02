namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed record LinkedInSessionSnapshot(
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset CapturedAtUtc,
    string Source);
