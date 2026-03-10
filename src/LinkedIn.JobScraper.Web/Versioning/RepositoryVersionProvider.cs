using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;

namespace LinkedIn.JobScraper.Web.Versioning;

public sealed partial class RepositoryVersionProvider : IAppVersionProvider
{
    public const string FallbackVersion = "v.0.0.0";

    public string CurrentVersion { get; }

    public RepositoryVersionProvider(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        CurrentVersion = LoadVersion(hostEnvironment.ContentRootPath);
    }

    public static bool IsValidVersionFormat(string? version)
    {
        return !string.IsNullOrWhiteSpace(version)
               && VersionPattern().IsMatch(version.Trim());
    }

    private static string LoadVersion(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            return FallbackVersion;
        }

        var versionFilePath = Path.Combine(contentRootPath, "VERSION");
        if (!File.Exists(versionFilePath))
        {
            return FallbackVersion;
        }

        var versionValue = File.ReadAllText(versionFilePath).Trim();
        return IsValidVersionFormat(versionValue)
            ? versionValue
            : FallbackVersion;
    }

    [GeneratedRegex(@"^v\.[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
