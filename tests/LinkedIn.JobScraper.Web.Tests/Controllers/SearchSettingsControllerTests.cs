using LinkedIn.JobScraper.Web.Controllers;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Models;
using LinkedIn.JobScraper.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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

    [Fact]
    public async Task SaveReturnsProblemDetailsForAjaxValidationFailure()
    {
        var controller = new SearchSettingsController(
            new ConcurrencyFailureLinkedInSearchSettingsService(),
            new FakeLinkedInLocationLookupService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Save(
            new LinkedInSearchSettingsPageViewModel
            {
                ProfileName = "Default",
                Keywords = "C# .Net",
                LocationInput = "Cyprus",
                WorkplaceTypeCodes = [],
                JobTypeCodes = []
            },
            CancellationToken.None);

        var problem = Assert.IsType<BadRequestObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("Search settings validation failed", details.Title);
        Assert.Equal("Review the highlighted search settings and try again.", details.Detail);
        Assert.True(details.Errors.ContainsKey(nameof(LinkedInSearchSettingsPageViewModel.LocationInput)));
    }

    [Fact]
    public async Task SaveReturnsJsonForAjaxSuccess()
    {
        var controller = new SearchSettingsController(
            new SavingLinkedInSearchSettingsService(),
            new FakeLinkedInLocationLookupService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        controller.ControllerContext.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var result = await controller.Save(
            new LinkedInSearchSettingsPageViewModel
            {
                ProfileName = "Default",
                Keywords = "C# .Net",
                WorkplaceTypeCodes = ["1"],
                JobTypeCodes = ["F"]
            },
            CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<SettingsSaveResponse>(json.Value);

        Assert.True(payload.Success);
        Assert.Equal("/SearchSettings", payload.RedirectUrl);
        Assert.Equal("token-saved", payload.ConcurrencyToken);
    }

    [Fact]
    public async Task LocationSuggestionsReturnsJsonSuggestionsWhenLookupSucceeds()
    {
        var controller = new SearchSettingsController(
            new SavingLinkedInSearchSettingsService(),
            new SuccessfulLinkedInLocationLookupService());

        var result = await controller.LocationSuggestions("Lim", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = Assert.IsType<LinkedInLocationSuggestionsResponse>(json.Value);

        Assert.Single(payload.Suggestions);
        Assert.Equal("106394980", payload.Suggestions[0].GeoId);
        Assert.Equal("Limassol, Cyprus", payload.Suggestions[0].DisplayName);
    }

    [Fact]
    public async Task LocationSuggestionsReturnsProblemDetailsWhenLookupFails()
    {
        var controller = new SearchSettingsController(
            new SavingLinkedInSearchSettingsService(),
            new FailingLinkedInLocationLookupService());

        var result = await controller.LocationSuggestions("Lim", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        var details = Assert.IsType<ProblemDetails>(problem.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("Location suggestion lookup failed", details.Title);
        Assert.Contains("LinkedIn", details.Detail, StringComparison.Ordinal);
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

    private sealed class SuccessfulLinkedInLocationLookupService : ILinkedInLocationLookupService
    {
        public Task<LinkedInLocationLookupResult> SearchAsync(string locationQuery, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LinkedInLocationLookupResult.Succeeded(
                    [new LinkedInLocationSuggestion("106394980", "Limassol, Cyprus")]));
        }
    }

    private sealed class FailingLinkedInLocationLookupService : ILinkedInLocationLookupService
    {
        public Task<LinkedInLocationLookupResult> SearchAsync(string locationQuery, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LinkedInLocationLookupResult.Failed(
                    "LinkedIn location suggestions are currently unavailable.",
                    StatusCodes.Status503ServiceUnavailable));
        }
    }

    private sealed class SavingLinkedInSearchSettingsService : ILinkedInSearchSettingsService
    {
        public Task<LinkedInSearchSettings> GetActiveAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<LinkedInSearchSettings> SaveAsync(LinkedInSearchSettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new LinkedInSearchSettings(
                    settings.ProfileName,
                    settings.Keywords,
                    settings.LocationInput,
                    settings.LocationDisplayName,
                    settings.LocationGeoId,
                    settings.EasyApply,
                    settings.WorkplaceTypeCodes,
                    settings.JobTypeCodes,
                    "token-saved"));
        }
    }
}
