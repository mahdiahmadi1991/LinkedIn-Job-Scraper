namespace LinkedIn.JobScraper.Web.Persistence;

public interface ISqlServerConnectionStringProvider
{
    bool IsConfigured { get; }

    string GetRequiredConnectionString();
}
