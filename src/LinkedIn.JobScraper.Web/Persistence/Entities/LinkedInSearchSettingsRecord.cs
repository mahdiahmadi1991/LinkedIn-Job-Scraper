namespace LinkedIn.JobScraper.Web.Persistence.Entities;

public sealed class LinkedInSearchSettingsRecord
{
    public int Id { get; set; }

    public string ProfileName { get; set; } = string.Empty;

    public string Keywords { get; set; } = string.Empty;

    public string? LocationInput { get; set; }

    public string? LocationDisplayName { get; set; }

    public string? LocationGeoId { get; set; }

    public bool EasyApply { get; set; }

    public string WorkplaceTypeCodesCsv { get; set; } = string.Empty;

    public string JobTypeCodesCsv { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
