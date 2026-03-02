namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public interface ILinkedInSessionStore
{
    Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken);

    Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken);
}
