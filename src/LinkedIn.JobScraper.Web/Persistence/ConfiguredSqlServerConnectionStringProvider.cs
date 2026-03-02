using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Persistence;

public sealed class ConfiguredSqlServerConnectionStringProvider : ISqlServerConnectionStringProvider
{
    private readonly IOptions<SqlServerOptions> _sqlServerOptions;

    public ConfiguredSqlServerConnectionStringProvider(IOptions<SqlServerOptions> sqlServerOptions)
    {
        _sqlServerOptions = sqlServerOptions;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_sqlServerOptions.Value.ConnectionString);

    public string GetRequiredConnectionString()
    {
        var connectionString = _sqlServerOptions.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SqlServer:ConnectionString is not configured.");
        }

        return connectionString;
    }
}
