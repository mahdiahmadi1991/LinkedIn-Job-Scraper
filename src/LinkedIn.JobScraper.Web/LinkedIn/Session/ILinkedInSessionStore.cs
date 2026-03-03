namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInSessionStore
{
    Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken);

    Task InvalidateCurrentAsync(CancellationToken cancellationToken);

    Task MarkCurrentValidatedAsync(DateTimeOffset validatedAtUtc, CancellationToken cancellationToken);

    Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken);
}
