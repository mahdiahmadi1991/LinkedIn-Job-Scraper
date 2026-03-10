namespace LinkedIn.JobScraper.Web.Versioning;

public interface IAppVersionProvider
{
    string CurrentVersion { get; }
}
