namespace LinkedIn.JobScraper.Web.Configuration;

public sealed class AppAuthenticationOptions
{
    public const string SectionName = "AppAuthentication";

    public IList<AppAuthenticationSeedUserOptions> SeedUsers { get; set; } = [];
}

public sealed class AppAuthenticationSeedUserOptions
{
    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
