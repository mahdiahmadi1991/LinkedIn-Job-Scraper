using System.Security.Cryptography;
using System.Text;
using LinkedIn.JobScraper.Web.LinkedIn.Details;

namespace LinkedIn.JobScraper.Web.Jobs;

public static class JobDetailFingerprint
{
    public static string Compute(LinkedInJobDetailData detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var normalizedPayload = string.Join(
            "\n",
            Normalize(detail.Title),
            Normalize(detail.CompanyName),
            Normalize(detail.LocationName),
            Normalize(detail.EmploymentStatus),
            Normalize(detail.Description),
            Normalize(detail.CompanyApplyUrl),
            Normalize(detail.ListedAtUtc),
            Normalize(detail.LinkedInUpdatedAtUtc));

        var payloadBytes = Encoding.UTF8.GetBytes(normalizedPayload);
        var hashBytes = SHA256.HashData(payloadBytes);

        return Convert.ToHexString(hashBytes);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string Normalize(DateTimeOffset? value)
    {
        return value.HasValue
            ? value.Value.UtcDateTime.ToString("O")
            : string.Empty;
    }
}
