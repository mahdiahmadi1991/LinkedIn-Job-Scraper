using LinkedIn.JobScraper.Web.Persistence;

namespace LinkedIn.JobScraper.Web.Tests.Persistence;

public sealed class ConcurrencyTokenCodecTests
{
    [Fact]
    public void EncodeAndDecodeRoundTripPreservesToken()
    {
        var original = new byte[] { 1, 2, 3, 4, 5 };

        var encoded = ConcurrencyTokenCodec.Encode(original);
        var decoded = ConcurrencyTokenCodec.Decode(encoded);

        Assert.NotNull(encoded);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void DecodeReturnsNullForBlankToken()
    {
        Assert.Null(ConcurrencyTokenCodec.Decode(null));
        Assert.Null(ConcurrencyTokenCodec.Decode(string.Empty));
        Assert.Null(ConcurrencyTokenCodec.Decode("   "));
    }

    [Fact]
    public void DecodeThrowsFriendlyExceptionForMalformedToken()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ConcurrencyTokenCodec.Decode("not-base64"));

        Assert.Contains("submitted settings state is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
