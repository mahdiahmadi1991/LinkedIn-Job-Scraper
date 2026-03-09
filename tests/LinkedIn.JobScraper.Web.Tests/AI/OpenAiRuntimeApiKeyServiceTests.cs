using LinkedIn.JobScraper.Web.AI;
using Microsoft.Extensions.FileProviders;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class OpenAiRuntimeApiKeyServiceTests
{
    [Fact]
    public async Task GetActiveAsyncReturnsNullWhenRuntimeOverrideIsMissing()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var service = new OpenAiRuntimeApiKeyService(new FakeHostEnvironment(rootPath));

            var activeKey = await service.GetActiveAsync(CancellationToken.None);

            Assert.Null(activeKey);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsyncPersistsRuntimeOverrideAndReturnsIt()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var service = new OpenAiRuntimeApiKeyService(new FakeHostEnvironment(rootPath));

            await service.SaveAsync("sk-runtime-key-is-long-enough", CancellationToken.None);
            var activeKey = await service.GetActiveAsync(CancellationToken.None);

            Assert.Equal("sk-runtime-key-is-long-enough", activeKey);
            Assert.True(File.Exists(Path.Combine(rootPath, "App_Data", "openai-runtime-secrets.json")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsyncRejectsMissingApiKey()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var service = new OpenAiRuntimeApiKeyService(new FakeHostEnvironment(rootPath));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SaveAsync("   ", CancellationToken.None));

            Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsyncRejectsApiKeyLongerThanLimit()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var service = new OpenAiRuntimeApiKeyService(new FakeHostEnvironment(rootPath));
            var overLimitKey = new string('a', 513);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SaveAsync(overLimitKey, CancellationToken.None));

            Assert.Contains("512 characters or fewer", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ljs-openai-key-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public FakeHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "LinkedIn.JobScraper.Web.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }

}
