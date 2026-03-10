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

        foreach (var versionFilePath in GetVersionFileCandidates(contentRootPath))
        {
            if (!File.Exists(versionFilePath))
            {
                continue;
            }

            var versionValue = File.ReadAllText(versionFilePath).Trim();
            if (IsValidVersionFormat(versionValue))
            {
                return versionValue;
            }
        }

        return FallbackVersion;
    }

    private static IEnumerable<string> GetVersionFileCandidates(string contentRootPath)
    {
        var contentRootAbsolutePath = Path.GetFullPath(contentRootPath);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[]
                 {
                     Path.Combine(contentRootAbsolutePath, "VERSION"),
                     Path.Combine(contentRootAbsolutePath, "..", "VERSION"),
                     Path.Combine(contentRootAbsolutePath, "..", "..", "VERSION")
                 })
        {
            var fullPath = Path.GetFullPath(candidate);
            if (seenPaths.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    [GeneratedRegex(@"^v\.[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
