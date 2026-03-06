using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class AiSettingsViewModelAdapterTests
{
    [Fact]
    public void ToViewModelMapsProfileAndStatus()
    {
        var profile = new AiBehaviorProfile(
            "Behavior",
            "Priority",
            "Exclusion",
            "fa",
            "token-1");

        var model = AiSettingsViewModelAdapter.ToViewModel(profile, "Saved.", true);

        Assert.Equal("Behavior", model.BehavioralInstructions);
        Assert.Equal("Priority", model.PrioritySignals);
        Assert.Equal("Exclusion", model.ExclusionSignals);
        Assert.Equal("fa", model.OutputLanguageCode);
        Assert.Equal("token-1", model.ConcurrencyToken);
        Assert.Equal("Saved.", model.StatusMessage);
        Assert.True(model.StatusSucceeded);
    }

    [Fact]
    public void CreateConnectionStateReturnsReadyWhenConfigIsComplete()
    {
        var state = AiSettingsViewModelAdapter.CreateConnectionState(
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                BaseUrl = "https://api.openai.com/v1"
            });

        Assert.True(state.ApiKeyConfigured);
        Assert.True(state.Ready);
        Assert.Equal("gpt-5-mini", state.Model);
        Assert.Equal("https://api.openai.com/v1", state.BaseUrl);
    }
}
