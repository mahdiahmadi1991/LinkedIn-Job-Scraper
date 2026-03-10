namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class InMemoryLinkedInSessionStore : ILinkedInSessionStore
{
    private LinkedInSessionSnapshot? _currentSnapshot;

    public Task<LinkedInSessionSnapshot?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_currentSnapshot);
    }

    public Task SaveAsync(LinkedInSessionSnapshot sessionSnapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _currentSnapshot = new LinkedInSessionSnapshot(
            LinkedInSessionHeaderSanitizer.SanitizeForStorage(sessionSnapshot.Headers),
            sessionSnapshot.CapturedAtUtc,
            sessionSnapshot.Source,
            sessionSnapshot.EstimatedExpiresAtUtc,
            sessionSnapshot.ExpirySource);
        return Task.CompletedTask;
    }

    public Task InvalidateCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _currentSnapshot = null;
        return Task.CompletedTask;
    }

    public Task MarkCurrentValidatedAsync(DateTimeOffset validatedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
