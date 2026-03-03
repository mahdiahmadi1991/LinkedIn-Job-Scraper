using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AiSettingsControllerTests
{
    [Fact]
    public async Task IndexPopulatesOpenAiConnectionStatus()
    {
        var controller = new AiSettingsController(
            new FakeAiBehaviorSettingsService(),
            Options.Create(new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                BaseUrl = "https://api.openai.com/v1"
            }))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AiSettingsPageViewModel>(view.Model);

        Assert.True(model.OpenAiApiKeyConfigured);
        Assert.True(model.OpenAiConnectionReady);
        Assert.Equal("gpt-5-mini", model.OpenAiModel);
        Assert.Equal("https://api.openai.com/v1", model.OpenAiBaseUrl);
        Assert.Equal("token-1", model.ConcurrencyToken);
    }

    [Fact]
    public async Task SaveReturnsViewWithFriendlyMessageOnConcurrencyConflict()
    {
        var controller = new AiSettingsController(
            new ConcurrencyFailureAiBehaviorSettingsService(),
            Options.Create(new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini"
            }))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var result = await controller.Save(
            new AiSettingsPageViewModel
            {
                ProfileName = "Default",
                BehavioralInstructions = "Test",
                PrioritySignals = "Test",
                ExclusionSignals = "Test",
                OutputLanguageCode = "en",
                ConcurrencyToken = "token-2"
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AiSettingsPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Contains("updated by another operation", model.StatusMessage, StringComparison.Ordinal);
        Assert.True(model.OpenAiApiKeyConfigured);
    }

    [Fact]
    public void ConnectionStatusReturnsProblemDetailsWhenConfigurationIsIncomplete()
    {
        var controller = new AiSettingsController(
            new FakeAiBehaviorSettingsService(),
            Options.Create(new OpenAiSecurityOptions
            {
                Model = "gpt-5-mini"
            }));

        var result = controller.ConnectionStatus();

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("AI connection is not ready", details.Title);
        Assert.Contains("OpenAI:Security:ApiKey", details.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionStatusReturnsJsonPayloadWhenConfigurationIsReady()
    {
        var controller = new AiSettingsController(
            new FakeAiBehaviorSettingsService(),
            Options.Create(new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                BaseUrl = "https://api.openai.com/v1"
            }));

        var result = controller.ConnectionStatus();

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<AiConnectionStatusResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.True(payload.State.Ready);
        Assert.Equal("gpt-5-mini", payload.State.Model);
    }

    private sealed class FakeAiBehaviorSettingsService : IAiBehaviorSettingsService
    {
        public Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new AiBehaviorProfile(
                    "Default",
                    "Behavior",
                    "Priority",
                    "Exclusion",
                    "en",
                    "token-1"));
        }

        public Task<AiBehaviorProfile> SaveAsync(AiBehaviorProfile profile, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ConcurrencyFailureAiBehaviorSettingsService : IAiBehaviorSettingsService
    {
        public Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AiBehaviorProfile> SaveAsync(AiBehaviorProfile profile, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("AI behavior settings were updated by another operation. Reload the page and try again.");
        }
    }
}
