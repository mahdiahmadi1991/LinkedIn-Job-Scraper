using LinkedIn.JobScraper.Web.Models;

namespace LinkedIn.JobScraper.Web.LinkedIn.Search;

public static class LinkedInSearchSettingsViewModelAdapter
{
    public static LinkedInSearchSettingsPageViewModel ToViewModel(LinkedInSearchSettings settings)
    {
        return new LinkedInSearchSettingsPageViewModel
        {
            Keywords = settings.Keywords,
            ConcurrencyToken = settings.ConcurrencyToken,
            LocationInput = settings.LocationInput,
            LocationDisplayName = settings.LocationDisplayName,
            LocationGeoId = settings.LocationGeoId,
            EasyApply = settings.EasyApply,
            WorkplaceTypeCodes = settings.WorkplaceTypeCodes.ToList(),
            JobTypeCodes = settings.JobTypeCodes.ToList()
        };
    }

    public static void NormalizeSelections(LinkedInSearchSettingsPageViewModel viewModel)
    {
        viewModel.WorkplaceTypeCodes ??= [];
        viewModel.JobTypeCodes ??= [];
    }

    public static void ResetSelectedLocation(LinkedInSearchSettingsPageViewModel viewModel)
    {
        viewModel.LocationDisplayName = null;
        viewModel.LocationGeoId = null;
    }

    public static IReadOnlyList<(string Key, string Message)> GetValidationErrors(LinkedInSearchSettingsPageViewModel viewModel)
    {
        var errors = new List<(string Key, string Message)>();

        if (!string.IsNullOrWhiteSpace(viewModel.LocationInput) &&
            string.IsNullOrWhiteSpace(viewModel.LocationGeoId))
        {
            errors.Add((nameof(viewModel.LocationInput), "Search LinkedIn locations and choose one result so a valid geoId is stored."));
        }

        if (!string.IsNullOrWhiteSpace(viewModel.LocationGeoId) &&
            string.IsNullOrWhiteSpace(viewModel.LocationDisplayName))
        {
            errors.Add((nameof(viewModel.LocationInput), "Choose a LinkedIn location result before saving."));
        }

        return errors;
    }
}
