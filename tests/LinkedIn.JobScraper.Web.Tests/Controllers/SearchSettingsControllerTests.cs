using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Tests.Controllers;

public sealed class SearchSettingsControllerTests
{
    [Fact]
    public async Task SaveReturnsViewWithFriendlyMessageOnConcurrencyConflict()
    {
        var controller = new SearchSettingsController(
            new ConcurrencyFailureLinkedInSearchSettingsService(),
            new FakeLinkedInLocationLookupService());

        var result = await controller.Save(
            new LinkedInSearchSettingsPageViewModel
            {
                ProfileName = "Default",
                Keywords = "C# .Net",
                ConcurrencyToken = "token-3",
                WorkplaceTypeCodes = ["1"],
                JobTypeCodes = ["F"]
            },
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LinkedInSearchSettingsPageViewModel>(view.Model);

        Assert.Equal("Index", view.ViewName);
        Assert.False(model.StatusSucceeded);
        Assert.Contains("updated by another operation", model.StatusMessage, StringComparison.Ordinal);
    }

    private sealed class ConcurrencyFailureLinkedInSearchSettingsService : ILinkedInSearchSettingsService
    {
        public Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInSearchSettings> SaveAsync(LinkedInSearchSettings settings, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("LinkedIn search settings were updated by another operation. Reload the page and try again.");
        }
    }

    private sealed class FakeLinkedInLocationLookupService : ILinkedInLocationLookupService
    {
        public Task<LinkedInLocationLookupResult> SearchAsync(string locationQuery, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
