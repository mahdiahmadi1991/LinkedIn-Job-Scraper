using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Models;

namespace LinkedIn.JobScraper.Web.AI;

public static class AiSettingsViewModelAdapter
{
    public static AiSettingsPageViewModel ToViewModel(
        AiBehaviorProfile profile,
        string? statusMessage,
        bool statusSucceeded)
    {
        return new AiSettingsPageViewModel
        {
            ConcurrencyToken = profile.ConcurrencyToken,
            BehavioralInstructions = profile.BehavioralInstructions,
            PrioritySignals = profile.PrioritySignals,
            ExclusionSignals = profile.ExclusionSignals,
            OutputLanguageCode = profile.OutputLanguageCode,
            StatusMessage = statusMessage,
            StatusSucceeded = statusSucceeded
        };
    }

    public static OpenAiConnectionStateData CreateConnectionState(OpenAiSecurityOptions options)
    {
        var validationMessage = options.ValidateForScoring();

        return new OpenAiConnectionStateData(
            ApiKeyConfigured: !string.IsNullOrWhiteSpace(options.ApiKey),
            Model: string.IsNullOrWhiteSpace(options.Model) ? null : options.Model.Trim(),
            BaseUrl: string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://api.openai.com/v1"
                : options.BaseUrl.TrimEnd('/'),
            Ready: validationMessage is null,
            Message: validationMessage ?? "OpenAI connection settings are configured and ready for scoring.");
    }

    public static void PopulateConnectionStatus(
        AiSettingsPageViewModel viewModel,
        OpenAiConnectionStateData state)
    {
        viewModel.OpenAiApiKeyConfigured = state.ApiKeyConfigured;
        viewModel.OpenAiModel = state.Model;
        viewModel.OpenAiBaseUrl = state.BaseUrl;
        viewModel.OpenAiConnectionReady = state.Ready;
        viewModel.OpenAiConnectionStatusMessage = state.Message;
    }
}

public sealed record OpenAiConnectionStateData(
    bool ApiKeyConfigured,
    string? Model,
    string BaseUrl,
    bool Ready,
    string Message);
