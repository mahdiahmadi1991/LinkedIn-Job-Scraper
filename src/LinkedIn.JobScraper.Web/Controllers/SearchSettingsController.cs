using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.LinkedIn.Search;
using LinkedIn.JobScraper.Web.Contracts;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[Authorize(AuthenticationSchemes = AppAuthenticationDefaults.CookieScheme)]
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

    [HttpGet]
    public async Task<IActionResult> LocationSuggestions(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Json(new LinkedInLocationSuggestionsResponse([]));
        }

        var result = await _linkedInLocationLookupService.SearchAsync(query.Trim(), cancellationToken);

        if (!result.Success)
        {
            return Problem(
                title: "Location suggestion lookup failed",
                detail: result.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Json(
            new LinkedInLocationSuggestionsResponse(
                result.Suggestions
                    .Select(static suggestion => new LinkedInLocationSuggestionResponseItem(
                        suggestion.GeoId,
                        suggestion.DisplayName))
                    .ToArray()));
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
                var details = new ValidationProblemDetails(ModelState)
                {
                    Title = "Search settings validation failed",
                    Detail = viewModel.StatusMessage,
                    Status = StatusCodes.Status400BadRequest
                };

                return BadRequest(details);
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
