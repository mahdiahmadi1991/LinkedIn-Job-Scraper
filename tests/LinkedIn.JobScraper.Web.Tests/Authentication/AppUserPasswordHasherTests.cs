using LinkedIn.JobScraper.Web.Authentication;

namespace LinkedIn.JobScraper.Web.Tests.Authentication;

public sealed class AppUserPasswordHasherTests
{
    [Fact]
    public void HashPasswordProducesHashThatVerifies()
    {
        var hasher = new AppUserPasswordHasher();

        var hash = hasher.HashPassword("Passw0rd!");

        Assert.True(hasher.VerifyPassword("Passw0rd!", hash));
        Assert.False(hasher.VerifyPassword("WrongPassw0rd!", hash));
    }

    [Fact]
    public void VerifyPasswordReturnsFalseForInvalidHashFormat()
    {
        var hasher = new AppUserPasswordHasher();

        Assert.False(hasher.VerifyPassword("Passw0rd!", "not-a-valid-hash"));
    }
}
