using LinkedIn.JobScraper.Web.AI;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class AiOutputLanguageTests
{
    [Theory]
    [InlineData("fa", "rtl", "Persian", "fa", "Persian (Farsi)")]
    [InlineData("FA", "rtl", "Persian", "fa", "Persian (Farsi)")]
    [InlineData("en", "ltr", "English", "en", "English")]
    [InlineData(null, "ltr", "English", "en", "English")]
    [InlineData("unknown", "ltr", "English", "en", "English")]
    public void HelpersReturnExpectedValues(
        string? languageCode,
        string expectedDirection,
        string expectedDisplayName,
        string expectedNormalizedCode,
        string expectedPromptLabel)
    {
        Assert.Equal(expectedDirection, AiOutputLanguage.GetDirection(languageCode));
        Assert.Equal(expectedDisplayName, AiOutputLanguage.GetDisplayName(languageCode));
        Assert.Equal(expectedNormalizedCode, AiOutputLanguage.Normalize(languageCode));
        Assert.Equal(expectedPromptLabel, AiOutputLanguage.GetPromptLabel(languageCode));
    }
}
