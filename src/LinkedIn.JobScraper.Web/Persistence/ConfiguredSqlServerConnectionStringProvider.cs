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
        return _sqlServerOptions.Value.GetRequiredConnectionString();
    }
}
