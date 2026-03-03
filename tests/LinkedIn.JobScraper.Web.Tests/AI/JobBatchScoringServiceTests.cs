using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class JobBatchScoringServiceTests
{
    [Fact]
    public async Task ScoreReadyJobsAsyncUsesConfiguredBoundedConcurrencyAndPersistsScores()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await SeedJobsAsync(options, 3);

        var gateway = new BlockingJobScoringGateway(expectedConcurrency: 2);
        var service = CreateService(
            options,
            gateway,
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                MaxConcurrentScoringRequests = 2
            });

        var scoringTask = service.ScoreReadyJobsAsync(3, CancellationToken.None);

        await gateway.ReachedExpectedConcurrency.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, gateway.MaxObservedConcurrency);
        Assert.Equal(2, gateway.CallCount);

        gateway.ReleaseAll();

        var result = await scoringTask;

        Assert.True(result.Success);
        Assert.Equal(3, result.RequestedCount);
        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(3, result.ScoredCount);
        Assert.Equal(0, result.FailedCount);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var scoredJobs = await dbContext.Jobs.CountAsync(static job => job.AiScore == 80);
        Assert.Equal(3, scoredJobs);
    }

    [Fact]
    public async Task ScoreReadyJobsAsyncFailsFastWhenConfiguredConcurrencyIsInvalid()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await SeedJobsAsync(options, 1);

        var gateway = new CountingJobScoringGateway();
        var service = CreateService(
            options,
            gateway,
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                MaxConcurrentScoringRequests = 0
            });

        var result = await service.ScoreReadyJobsAsync(1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Contains("OpenAI:Security:MaxConcurrentScoringRequests", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, gateway.CallCount);
    }

    [Fact]
    public async Task ScoreJobAsyncPersistsScoreAndTimestamp()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await SeedJobsAsync(options, 1);

        await using var seedContext = new LinkedInJobScraperDbContext(options);
        var jobId = await seedContext.Jobs.Select(static job => job.Id).SingleAsync();

        var gateway = new CountingJobScoringGateway();
        var service = CreateService(
            options,
            gateway,
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini",
                MaxConcurrentScoringRequests = 2
            });

        var result = await service.ScoreJobAsync(jobId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(80, result.Snapshot?.AiScore);
        Assert.NotNull(result.Snapshot?.ScoredAtUtc);
        Assert.Equal(1, gateway.CallCount);

        await using var dbContext = new LinkedInJobScraperDbContext(options);
        var persistedJob = await dbContext.Jobs.SingleAsync(job => job.Id == jobId);
        Assert.Equal(80, persistedJob.AiScore);
        Assert.Equal("Review", persistedJob.AiLabel);
        Assert.NotNull(persistedJob.LastScoredAtUtc);
    }

    [Fact]
    public async Task ScoreJobAsyncReturnsConflictWhenJobWasAlreadyScored()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await SeedJobsAsync(options, 1);

        await using (var seedContext = new LinkedInJobScraperDbContext(options))
        {
            var job = await seedContext.Jobs.SingleAsync();
            job.AiScore = 77;
            job.AiLabel = "Review";
            job.LastScoredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            await seedContext.SaveChangesAsync();
        }

        await using var lookupContext = new LinkedInJobScraperDbContext(options);
        var jobId = await lookupContext.Jobs.Select(static job => job.Id).SingleAsync();

        var gateway = new CountingJobScoringGateway();
        var service = CreateService(
            options,
            gateway,
            new OpenAiSecurityOptions
            {
                ApiKey = "test-key",
                Model = "gpt-5-mini"
            });

        var result = await service.ScoreJobAsync(jobId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Contains("already scored", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.CallCount);
        Assert.Equal(77, result.Snapshot?.AiScore);
    }

    private static JobBatchScoringService CreateService(
        DbContextOptions<LinkedInJobScraperDbContext> dbContextOptions,
        IJobScoringGateway jobScoringGateway,
        OpenAiSecurityOptions options)
    {
        return new JobBatchScoringService(
            new TestDbContextFactory(dbContextOptions),
            jobScoringGateway,
            new FakeAiBehaviorSettingsService(),
            Options.Create(options));
    }

    private static async Task SeedJobsAsync(
        DbContextOptions<LinkedInJobScraperDbContext> dbContextOptions,
        int count)
    {
        await using var dbContext = new LinkedInJobScraperDbContext(dbContextOptions);

        for (var index = 0; index < count; index++)
        {
            dbContext.Jobs.Add(
                new JobRecord
                {
                    LinkedInJobId = $"job-{index}",
                    LinkedInJobPostingUrn = $"urn:li:jobPosting:{index}",
                    Title = $"Job {index}",
                    Description = $"Description {index}",
                    FirstDiscoveredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-count + index),
                    LastSeenAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index)
                });
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeAiBehaviorSettingsService : IAiBehaviorSettingsService
    {
        public Task<AiBehaviorProfile> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new AiBehaviorProfile(
                    "Default",
                    "Behavior",
                    "Priority",
                    "Exclusion",
                    "en"));
        }

        public Task<AiBehaviorProfile> SaveAsync(AiBehaviorProfile profile, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CountingJobScoringGateway : IJobScoringGateway
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<JobScoringGatewayResult> ScoreAsync(
            JobScoringGatewayRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            return Task.FromResult(
                new JobScoringGatewayResult(
                    true,
                    "OpenAI scoring succeeded.",
                    StatusCodes.Status200OK,
                    80,
                    "Review",
                    "Summary",
                    "Why matched",
                    "Concerns"));
        }
    }

    private sealed class BlockingJobScoringGateway : IJobScoringGateway
    {
        private readonly int _expectedConcurrency;
        private readonly TaskCompletionSource<bool> _reachedExpectedConcurrency = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;
        private int _inFlight;
        private int _maxObservedConcurrency;

        public BlockingJobScoringGateway(int expectedConcurrency)
        {
            _expectedConcurrency = expectedConcurrency;
        }

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);

        public Task ReachedExpectedConcurrency => _reachedExpectedConcurrency.Task;

        public void ReleaseAll()
        {
            _releaseSignal.TrySetResult(true);
        }

        public async Task<JobScoringGatewayResult> ScoreAsync(
            JobScoringGatewayRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var inFlight = Interlocked.Increment(ref _inFlight);
            UpdateMaxObservedConcurrency(inFlight);

            if (inFlight >= _expectedConcurrency)
            {
                _reachedExpectedConcurrency.TrySetResult(true);
            }

            try
            {
                await _releaseSignal.Task.WaitAsync(cancellationToken);

                return new JobScoringGatewayResult(
                    true,
                    "OpenAI scoring succeeded.",
                    StatusCodes.Status200OK,
                    80,
                    "Review",
                    "Summary",
                    "Why matched",
                    "Concerns");
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private void UpdateMaxObservedConcurrency(int observedConcurrency)
        {
            while (true)
            {
                var currentMax = Volatile.Read(ref _maxObservedConcurrency);

                if (observedConcurrency <= currentMax)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref _maxObservedConcurrency,
                        observedConcurrency,
                        currentMax) == currentMax)
                {
                    return;
                }
            }
        }
    }
}
