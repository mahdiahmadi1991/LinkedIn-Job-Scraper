namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";

    public string ConnectionString { get; set; } = string.Empty;

    public string GetRequiredConnectionString()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "SqlServer:ConnectionString is not configured. Set it with 'dotnet user-secrets set \"SqlServer:ConnectionString\" \"<your-sql-connection-string>\" --project src/LinkedIn.JobScraper.Web' or provide it via environment variables.");
        }

        return ConnectionString;
    }
}
