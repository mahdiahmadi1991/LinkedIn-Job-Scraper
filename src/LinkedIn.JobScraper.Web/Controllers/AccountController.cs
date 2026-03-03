using LinkedIn.JobScraper.Web.Authentication;
using LinkedIn.JobScraper.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedIn.JobScraper.Web.Controllers;

[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly IAppUserAuthenticationService _authenticationService;

    public AccountController(IAppUserAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetSafeReturnUrl(returnUrl));
        }

        return View(
            new LoginPageViewModel
            {
                ReturnUrl = returnUrl
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginPageViewModel model, CancellationToken cancellationToken)
    {
        model.ReturnUrl = string.IsNullOrWhiteSpace(model.ReturnUrl) ? null : model.ReturnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var authenticationResult = await _authenticationService.AuthenticateAsync(
            model.UserName,
            model.Password,
            cancellationToken);

        if (!authenticationResult.Success || authenticationResult.User is null)
        {
            ModelState.AddModelError(string.Empty, authenticationResult.Message);
            model.Password = string.Empty;
            return View(model);
        }

        var authenticationProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe
        };

        if (model.RememberMe)
        {
            authenticationProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);
        }

        await HttpContext.SignInAsync(
            AppAuthenticationDefaults.CookieScheme,
            _authenticationService.CreatePrincipal(authenticationResult.User),
            authenticationProperties);

        return Redirect(GetSafeReturnUrl(model.ReturnUrl));
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/Jobs";
        }

        if (!returnUrl.StartsWith('/') ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/Jobs";
        }

        return returnUrl;
    }
}
