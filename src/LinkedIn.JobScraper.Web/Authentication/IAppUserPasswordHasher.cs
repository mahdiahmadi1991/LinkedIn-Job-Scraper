namespace LinkedIn.JobScraper.Web.Authentication;

public interface IAppUserPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
