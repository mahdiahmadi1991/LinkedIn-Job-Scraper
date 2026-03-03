using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Models;

namespace LinkedIn.JobScraper.Web.Tests.LinkedIn;

public sealed class LinkedInSearchSettingsViewModelAdapterTests
{
    [Fact]
    public void ToViewModelMapsPersistedValues()
    {
        var settings = new LinkedInSearchSettings(
            "Default",
            "C# .Net",
            "Nicosia",
            "Nicosia, Cyprus",
            "104677818",
            true,
            ["1", "2"],
            ["F", "C"],
            "token-1");

        var model = LinkedInSearchSettingsViewModelAdapter.ToViewModel(settings);

        Assert.Equal("Default", model.ProfileName);
        Assert.Equal("C# .Net", model.Keywords);
        Assert.Equal("Nicosia", model.LocationInput);
        Assert.Equal("Nicosia, Cyprus", model.LocationDisplayName);
        Assert.Equal("104677818", model.LocationGeoId);
        Assert.Equal("token-1", model.ConcurrencyToken);
        Assert.Equal(["1", "2"], model.WorkplaceTypeCodes);
        Assert.Equal(["F", "C"], model.JobTypeCodes);
    }

    [Fact]
    public void GetValidationErrorsAllowsEmptyOptionalCheckboxSelections()
    {
        var model = new LinkedInSearchSettingsPageViewModel
        {
            ProfileName = "Default",
            Keywords = "C# .Net",
            LocationInput = "Cyprus",
            LocationGeoId = "104677818"
        };

        var errors = LinkedInSearchSettingsViewModelAdapter.GetValidationErrors(model);

        Assert.DoesNotContain(errors, error => error.Key == nameof(model.WorkplaceTypeCodes));
        Assert.DoesNotContain(errors, error => error.Key == nameof(model.JobTypeCodes));
        Assert.Contains(errors, error => error.Key == nameof(model.LocationInput) &&
            error.Message.Contains("Choose a LinkedIn location result", StringComparison.Ordinal));
    }
}
