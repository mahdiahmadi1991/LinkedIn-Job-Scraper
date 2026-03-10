using LinkedIn.JobScraper.Web.Versioning;
using Microsoft.Extensions.Hosting;

namespace LinkedIn.JobScraper.Web.Tests.Configuration;

public sealed class RepositoryVersionProviderTests
{
    [Fact]
    public void ReadsVersionFromContentRootVersionFileWhenFormatIsValid()
    {
        using var tempDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "VERSION"), "v.2.4.6");

        var hostEnvironment = new TestHostEnvironment(tempDirectory.Path);
        var sut = new RepositoryVersionProvider(hostEnvironment);

        Assert.Equal("v.2.4.6", sut.CurrentVersion);
    }

    [Fact]
    public void FallsBackWhenVersionFileIsMissing()
    {
        using var tempDirectory = CreateTempDirectory();
        var hostEnvironment = new TestHostEnvironment(tempDirectory.Path);

        var sut = new RepositoryVersionProvider(hostEnvironment);

        Assert.Equal(RepositoryVersionProvider.FallbackVersion, sut.CurrentVersion);
    }

    [Theory]
    [InlineData("v.1.0.0", true)]
    [InlineData("v.12.34.56", true)]
    [InlineData("1.0.0", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("v.1.0", false)]
    [InlineData("v.1.0.0-beta", false)]
    public void VersionFormatValidationMatchesExpectedContract(string value, bool expected)
    {
        var result = RepositoryVersionProvider.IsValidVersionFormat(value);

        Assert.Equal(expected, result);
    }

    private static TemporaryDirectory CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ljs-version-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }

    private sealed record TemporaryDirectory(string Path) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "LinkedIn.JobScraper.Web.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentRootPath);
    }
}
