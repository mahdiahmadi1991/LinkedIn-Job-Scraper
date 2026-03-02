using LinkedIn.JobScraper.Web.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace LinkedIn.JobScraper.Web.LinkedIn.Session;

public sealed class PlaywrightLinkedInBrowserLoginService :
    ILinkedInBrowserLoginService,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<PlaywrightLinkedInBrowserLoginService> _logger;
    private readonly IOptions<LinkedInBrowserAutomationOptions> _options;
    private readonly ILinkedInSessionStore _sessionStore;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public PlaywrightLinkedInBrowserLoginService(
        ILinkedInSessionStore sessionStore,
        IOptions<LinkedInBrowserAutomationOptions> options,
        ILogger<PlaywrightLinkedInBrowserLoginService> logger)
    {
        _sessionStore = sessionStore;
        _options = options;
        _logger = logger;
    }

    public async Task<LinkedInBrowserLoginActionResult> CaptureAndSaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_context is null)
            {
                return new LinkedInBrowserLoginActionResult(
                    false,
                    "No controlled browser session is open. Launch login first.");
            }

            var browserOptions = _options.Value;
            var cookies = await _context.CookiesAsync([browserOptions.LinkedInBaseUrl]);

            if (cookies.Count == 0)
            {
                return new LinkedInBrowserLoginActionResult(
                    false,
                    "No LinkedIn cookies were found yet. Complete login in the browser, then capture again.");
            }

            var cookieHeader = string.Join(
                "; ",
                cookies.Select(
                    static cookie => $"{cookie.Name}={FormatCookieValue(cookie.Value)}"));

            var jsessionCookie = cookies.FirstOrDefault(
                static cookie => string.Equals(cookie.Name, "JSESSIONID", StringComparison.OrdinalIgnoreCase));

            if (jsessionCookie is null)
            {
                return new LinkedInBrowserLoginActionResult(
                    false,
                    "JSESSIONID cookie was not found. LinkedIn login may not be complete yet.");
            }

            var csrfToken = TrimWrappingQuotes(jsessionCookie.Value);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Accept"] = "application/vnd.linkedin.normalized+json+2.1",
                ["Accept-Language"] = browserOptions.AcceptLanguage,
                ["Cookie"] = cookieHeader,
                ["User-Agent"] = browserOptions.UserAgent,
                ["csrf-token"] = csrfToken,
                ["x-li-lang"] = browserOptions.LinkedInLanguage,
                ["x-restli-protocol-version"] = "2.0.0"
            };

            var snapshot = new LinkedInSessionSnapshot(
                headers,
                DateTimeOffset.UtcNow,
                "PlaywrightManualLogin");

            await _sessionStore.SaveAsync(snapshot, cancellationToken);

            return new LinkedInBrowserLoginActionResult(
                true,
                "LinkedIn session was captured and saved to the database.");
        }
        catch (Exception exception)
        {
            Log.FailedToCaptureAndSaveLinkedInBrowserSession(_logger, exception);

            return new LinkedInBrowserLoginActionResult(
                false,
                $"Failed to capture the LinkedIn session: {exception.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LinkedInBrowserLoginState> GetStateAsync(CancellationToken cancellationToken)
    {
        var storedSession = await _sessionStore.GetCurrentAsync(cancellationToken);
        var currentPageUrl = _page?.IsClosed == false ? _page.Url : null;

        return new LinkedInBrowserLoginState(
            BrowserOpen: _browser is not null && _browser.IsConnected,
            CurrentPageUrl: string.IsNullOrWhiteSpace(currentPageUrl) ? null : currentPageUrl,
            StoredSessionAvailable: storedSession is not null,
            StoredSessionCapturedAtUtc: storedSession?.CapturedAtUtc,
            StoredSessionSource: storedSession?.Source);
    }

    public async Task<LinkedInBrowserLoginActionResult> LaunchLoginAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            await EnsureBrowserAsync(cancellationToken);

            if (_context is null)
            {
                return new LinkedInBrowserLoginActionResult(false, "The browser context is not available.");
            }

            if (_page is null || _page.IsClosed)
            {
                _page = await _context.NewPageAsync();
            }

            await _page.GotoAsync(_options.Value.LoginUrl);
            await _page.BringToFrontAsync();

            return new LinkedInBrowserLoginActionResult(
                true,
                "Controlled browser launched. Complete the LinkedIn login in that window, then click Capture Session.");
        }
        catch (PlaywrightException exception)
        {
            Log.FailedToLaunchControlledPlaywrightBrowser(_logger, exception);

            return new LinkedInBrowserLoginActionResult(
                false,
                "Failed to launch the controlled browser. If Playwright browsers are not installed yet, run 'npx playwright install chromium' and try again.");
        }
        catch (Exception exception)
        {
            Log.UnexpectedErrorWhileLaunchingControlledBrowser(_logger, exception);

            return new LinkedInBrowserLoginActionResult(
                false,
                $"Failed to launch the controlled browser: {exception.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ClosePageSafelyAsync();
        await CloseContextSafelyAsync();
        await CloseBrowserSafelyAsync();

        _playwright?.Dispose();
        _playwright = null;
        _gate.Dispose();
    }

    private async Task EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_browser is not null && _browser.IsConnected && _context is not null)
        {
            return;
        }

        _playwright ??= await Microsoft.Playwright.Playwright.CreateAsync();

        var browserOptions = _options.Value;
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = browserOptions.Headless
        };

        if (!string.IsNullOrWhiteSpace(browserOptions.BrowserChannel))
        {
            launchOptions.Channel = browserOptions.BrowserChannel;
        }

        _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
        _context = await _browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                Locale = "en-US",
                UserAgent = browserOptions.UserAgent
            });
    }

    private static string FormatCookieValue(string value)
    {
        var normalized = value.Replace("\"", string.Empty, StringComparison.Ordinal);

        if (normalized.Contains(':', StringComparison.Ordinal))
        {
            return $"\"{normalized}\"";
        }

        return normalized;
    }

    private static string TrimWrappingQuotes(string value)
    {
        return value.Trim().Trim('"');
    }

    private async Task ClosePageSafelyAsync()
    {
        if (_page is null)
        {
            return;
        }

        try
        {
            if (!_page.IsClosed)
            {
                await _page.CloseAsync();
            }
        }
        catch (PlaywrightException)
        {
        }
        finally
        {
            _page = null;
        }
    }

    private async Task CloseContextSafelyAsync()
    {
        if (_context is null)
        {
            return;
        }

        try
        {
            await _context.CloseAsync();
        }
        catch (PlaywrightException)
        {
        }
        finally
        {
            _context = null;
        }
    }

    private async Task CloseBrowserSafelyAsync()
    {
        if (_browser is null)
        {
            return;
        }

        try
        {
            if (_browser.IsConnected)
            {
                await _browser.CloseAsync();
            }
        }
        catch (PlaywrightException)
        {
        }
        finally
        {
            _browser = null;
        }
    }
}

internal static partial class Log
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "Failed to capture and save the LinkedIn browser session.")]
    public static partial void FailedToCaptureAndSaveLinkedInBrowserSession(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Failed to launch the controlled Playwright browser.")]
    public static partial void FailedToLaunchControlledPlaywrightBrowser(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Unexpected error while launching the controlled browser.")]
    public static partial void UnexpectedErrorWhileLaunchingControlledBrowser(ILogger logger, Exception exception);
}
