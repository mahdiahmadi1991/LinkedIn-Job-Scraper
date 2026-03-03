using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

public sealed class SearchSettingsController : Controller
{
    private readonly ILinkedInLocationLookupService _linkedInLocationLookupService;
    private readonly ILinkedInSearchSettingsService _linkedInSearchSettingsService;

    public SearchSettingsController(
        ILinkedInSearchSettingsService linkedInSearchSettingsService,
        ILinkedInLocationLookupService linkedInLocationLookupService)
    {
        _linkedInSearchSettingsService = linkedInSearchSettingsService;
        _linkedInLocationLookupService = linkedInLocationLookupService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await _linkedInSearchSettingsService.GetActiveAsync(cancellationToken);
        var viewModel = LinkedInSearchSettingsViewModelAdapter.ToViewModel(settings);
        viewModel.StatusMessage = TempData["SearchSettingsStatusMessage"] as string;
        viewModel.StatusSucceeded = string.Equals(
            TempData["SearchSettingsStatusSucceeded"] as string,
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchLocation(
        LinkedInSearchSettingsPageViewModel viewModel,
        CancellationToken cancellationToken)
    {
        LinkedInSearchSettingsViewModelAdapter.NormalizeSelections(viewModel);
        LinkedInSearchSettingsViewModelAdapter.ResetSelectedLocation(viewModel);
        ModelState.Remove(nameof(viewModel.LocationDisplayName));
        ModelState.Remove(nameof(viewModel.LocationGeoId));

        if (string.IsNullOrWhiteSpace(viewModel.LocationInput))
        {
            viewModel.StatusMessage = "Enter a city, state, or zip code before searching for LinkedIn locations.";
            viewModel.StatusSucceeded = false;
            return View("Index", viewModel);
        }

        var result = await _linkedInLocationLookupService.SearchAsync(viewModel.LocationInput, cancellationToken);

        viewModel.StatusMessage = result.Success
            ? $"{result.Suggestions.Count} LinkedIn location suggestions were found. Choose one, then save."
            : result.Message;
        viewModel.StatusSucceeded = result.Success;
        viewModel.LocationSuggestions = result.Suggestions
            .Select(static suggestion => new LinkedInLocationSuggestionViewModel(suggestion.GeoId, suggestion.DisplayName))
            .ToList();

        return View("Index", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        LinkedInSearchSettingsPageViewModel viewModel,
        CancellationToken cancellationToken)
    {
        LinkedInSearchSettingsViewModelAdapter.NormalizeSelections(viewModel);

        foreach (var error in LinkedInSearchSettingsViewModelAdapter.GetValidationErrors(viewModel))
        {
            ModelState.AddModelError(error.Key, error.Message);
        }

        if (!ModelState.IsValid)
        {
            viewModel.StatusMessage = "Review the highlighted search settings and try again.";
            viewModel.StatusSucceeded = false;

            if (IsAjaxRequest())
            {
                return Problem(
                    title: "Search settings validation failed",
                    detail: viewModel.StatusMessage,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return View("Index", viewModel);
        }

        LinkedInSearchSettings savedSettings;

        try
        {
            savedSettings = await _linkedInSearchSettingsService.SaveAsync(
                new LinkedInSearchSettings(
                    viewModel.ProfileName,
                    viewModel.Keywords,
                    viewModel.LocationInput,
                    viewModel.LocationDisplayName,
                    viewModel.LocationGeoId,
                    viewModel.EasyApply,
                    viewModel.WorkplaceTypeCodes,
                    viewModel.JobTypeCodes,
                    viewModel.ConcurrencyToken),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            viewModel.StatusMessage = exception.Message;
            viewModel.StatusSucceeded = false;

            if (IsAjaxRequest())
            {
                return Problem(
                    title: "Search settings save failed",
                    detail: viewModel.StatusMessage,
                    statusCode: StatusCodes.Status409Conflict);
            }

            return View("Index", viewModel);
        }

        TempData["SearchSettingsStatusMessage"] =
            $"Saved LinkedIn fetch settings for '{savedSettings.ProfileName}'.";
        TempData["SearchSettingsStatusSucceeded"] = bool.TrueString;

        if (IsAjaxRequest())
        {
            var redirectUrl = Url is null ? "/SearchSettings" : Url.Action(nameof(Index)) ?? "/SearchSettings";

            return Json(
                new SettingsSaveResponse(
                    true,
                    TempData["SearchSettingsStatusMessage"] as string ?? "Search settings were saved.",
                    redirectUrl,
                    savedSettings.ConcurrencyToken));
        }

        return RedirectToAction(nameof(Index));
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            HttpContext?.Request.Headers.XRequestedWith.ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }
}
