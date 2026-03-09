using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Models;

namespace LinkedIn.JobScraper.Web.AI;

public static class AdminOpenAiSetupViewModelAdapter
{
    public static AdminOpenAiSetupFormViewModel ToViewModel(OpenAiRuntimeSettingsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new AdminOpenAiSetupFormViewModel
        {
            ConcurrencyToken = profile.ConcurrencyToken,
            Model = profile.Model,
            BaseUrl = profile.BaseUrl,
            RequestTimeoutSeconds = profile.RequestTimeoutSeconds,
            UseBackgroundMode = profile.UseBackgroundMode,
            BackgroundPollingIntervalMilliseconds = profile.BackgroundPollingIntervalMilliseconds,
            BackgroundPollingTimeoutSeconds = profile.BackgroundPollingTimeoutSeconds,
            MaxConcurrentScoringRequests = profile.MaxConcurrentScoringRequests
        };
    }

    public static OpenAiRuntimeSettingsProfile ToProfile(AdminOpenAiSetupFormViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return new OpenAiRuntimeSettingsProfile(
            viewModel.Model,
            viewModel.BaseUrl,
            viewModel.RequestTimeoutSeconds,
            viewModel.UseBackgroundMode,
            viewModel.BackgroundPollingIntervalMilliseconds,
            viewModel.BackgroundPollingTimeoutSeconds,
            viewModel.MaxConcurrentScoringRequests,
            viewModel.ConcurrencyToken);
    }

    public static OpenAiConnectionStateData CreateConnectionState(OpenAiSecurityOptions effectiveSecurityOptions)
    {
        ArgumentNullException.ThrowIfNull(effectiveSecurityOptions);
        return AiSettingsViewModelAdapter.CreateConnectionState(effectiveSecurityOptions);
    }

    public static void PopulateConnectionStatus(
        AdminUsersPageViewModel viewModel,
        OpenAiConnectionStateData state)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(state);

        viewModel.OpenAiApiKeyConfigured = state.ApiKeyConfigured;
        viewModel.OpenAiConnectionReady = state.Ready;
        viewModel.OpenAiConnectionStatusMessage = state.Message;
    }
}
