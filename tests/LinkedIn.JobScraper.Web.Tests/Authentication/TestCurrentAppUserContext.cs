using LinkedIn.JobScraper.Web.Authentication;

namespace LinkedIn.JobScraper.Web.Tests.Authentication;

internal sealed class TestCurrentAppUserContext : ICurrentAppUserContext
{
    private readonly int _userId;

    public TestCurrentAppUserContext(int userId = 1)
    {
        _userId = userId;
    }

    public int GetRequiredUserId()
    {
        return _userId;
    }

    public bool TryGetUserId(out int userId)
    {
        userId = _userId;
        return true;
    }
}
