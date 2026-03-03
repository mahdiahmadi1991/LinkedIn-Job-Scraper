using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class AiSettingsControllerTests
{
    [Fact]
    public async Task SaveReturnsViewWithFriendlyMessageOnConcurrencyConflict()
    {
        var controller = new AiSettingsController(new ConcurrencyFailureAiBehaviorSettingsService());

        var result = await controller.Save(
            new AiSettingsPageViewModel
            {
                ProfileName = "Default",
                BehavioralInstructions = "Test",
                PrioritySignals = "Test",
                ExclusionSignals = "Test",
                OutputLanguageCode = "en"
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AiSettingsPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Contains("updated by another operation", model.StatusMessage, StringComparison.Ordinal);
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
