using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class SqlServerOptionsValidator : IValidateOptions<SqlServerOptions>
{
    public ValidateOptionsResult Validate(string? name, SqlServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.GetRequiredConnectionString();
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }
}

public sealed class OpenAiSecurityOptionsValidator : IValidateOptions<OpenAiSecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RequestTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                "OpenAI request timeout must be greater than zero. Set 'OpenAI:Security:RequestTimeoutSeconds' to a positive integer value.");
        }

        if (options.UseBackgroundMode)
        {
            if (options.BackgroundPollingIntervalMilliseconds <= 0)
            {
                return ValidateOptionsResult.Fail(
                    "OpenAI background polling interval must be greater than zero. Set 'OpenAI:Security:BackgroundPollingIntervalMilliseconds' to a positive integer value.");
            }

            if (options.BackgroundPollingTimeoutSeconds <= 0)
            {
                return ValidateOptionsResult.Fail(
                    "OpenAI background polling timeout must be greater than zero. Set 'OpenAI:Security:BackgroundPollingTimeoutSeconds' to a positive integer value.");
            }
        }

        if (options.MaxConcurrentScoringRequests <= 0)
        {
            return ValidateOptionsResult.Fail(
                "OpenAI concurrent scoring limit must be greater than zero. Set 'OpenAI:Security:MaxConcurrentScoringRequests' to a positive integer value.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class LinkedInFetchDiagnosticsOptionsValidator : IValidateOptions<LinkedInFetchDiagnosticsOptions>
{
    public ValidateOptionsResult Validate(string? name, LinkedInFetchDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.ResponseBodyMaxLength <= 0)
        {
            return ValidateOptionsResult.Fail(
                "LinkedIn fetch diagnostics response body max length must be greater than zero. Set 'LinkedIn:FetchDiagnostics:ResponseBodyMaxLength' to a positive integer value.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class LinkedInFetchLimitsOptionsValidator : IValidateOptions<LinkedInFetchLimitsOptions>
{
    public ValidateOptionsResult Validate(string? name, LinkedInFetchLimitsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.SearchPageCap.HasValue && options.SearchPageCap.Value <= 0)
        {
            return ValidateOptionsResult.Fail(
                "LinkedIn search page cap must be greater than zero when configured. Set 'LinkedIn:FetchLimits:SearchPageCap' to a positive integer value.");
        }

        if (options.SearchJobCap.HasValue && options.SearchJobCap.Value <= 0)
        {
            return ValidateOptionsResult.Fail(
                "LinkedIn search job cap must be greater than zero when configured. Set 'LinkedIn:FetchLimits:SearchJobCap' to a positive integer value.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class LinkedInRequestOptionsValidator : IValidateOptions<LinkedInRequestOptions>
{
    public ValidateOptionsResult Validate(string? name, LinkedInRequestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.GraphQlQueryId))
        {
            return ValidateOptionsResult.Fail(
                "LinkedIn GraphQL query id is not configured. Set 'LinkedIn:RequestOptions:GraphQlQueryId' to a valid persisted query id.");
        }

        if (string.IsNullOrWhiteSpace(options.GeoTypeaheadQueryId))
        {
            return ValidateOptionsResult.Fail(
                "LinkedIn geo typeahead query id is not configured. Set 'LinkedIn:RequestOptions:GeoTypeaheadQueryId' to a valid persisted query id.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class JobsWorkflowOptionsValidator : IValidateOptions<JobsWorkflowOptions>
{
    public ValidateOptionsResult Validate(string? name, JobsWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.EnrichmentBatchSize.HasValue && options.EnrichmentBatchSize.Value <= 0)
        {
            return ValidateOptionsResult.Fail(
                "Jobs workflow enrichment batch size must be greater than zero when configured. Set 'Jobs:Workflow:EnrichmentBatchSize' to a positive integer value.");
        }

        return ValidateOptionsResult.Success;
    }
}
