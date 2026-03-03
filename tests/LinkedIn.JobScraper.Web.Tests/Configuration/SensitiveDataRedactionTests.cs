using LinkedIn.JobScraper.Web.Configuration;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class SensitiveDataRedactionTests
{
    [Fact]
    public void SanitizeForMessageRedactsSensitiveAssignments()
    {
        var sanitized = SensitiveDataRedaction.SanitizeForMessage(
            "Request failed. Authorization: Bearer abc123 Cookie=li_at=secret csrf-token: token123");

        Assert.Contains("Authorization=[redacted]", sanitized);
        Assert.Contains("Cookie=[redacted]", sanitized);
        Assert.Contains("csrf-token=[redacted]", sanitized);
        Assert.DoesNotContain("abc123", sanitized);
        Assert.DoesNotContain("token123", sanitized);
        Assert.DoesNotContain("li_at=secret", sanitized);
    }

    [Fact]
    public void SanitizeForMessageRedactsSecretLikeTokens()
    {
        var sanitized = SensitiveDataRedaction.SanitizeForMessage(
            "OpenAI returned sk-proj-123456 and JSESSIONID=\"ajax:9999\".");

        Assert.DoesNotContain("sk-proj-123456", sanitized);
        Assert.DoesNotContain("ajax:9999", sanitized);
        Assert.Contains("[redacted]", sanitized);
    }

    [Fact]
    public void SanitizeForMessageTruncatesLongMessages()
    {
        var input = new string('a', 300);

        var sanitized = SensitiveDataRedaction.SanitizeForMessage(input, 50);

        Assert.Equal(53, sanitized.Length);
        Assert.EndsWith("...", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeForMessageUsesFallbackForBlankInput()
    {
        var sanitized = SensitiveDataRedaction.SanitizeForMessage(" ");

        Assert.Equal("An internal error occurred.", sanitized);
    }
}
