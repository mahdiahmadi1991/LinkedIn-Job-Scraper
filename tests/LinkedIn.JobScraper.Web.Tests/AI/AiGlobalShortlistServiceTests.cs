using LinkedIn.JobScraper.Web.AI;
using LinkedIn.JobScraper.Web.Configuration;
using LinkedIn.JobScraper.Web.Persistence;
using LinkedIn.JobScraper.Web.Persistence.Entities;
using LinkedIn.JobScraper.Web.Tests.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class AiGlobalShortlistServiceTests
{
    [Fact]
    public async Task GenerateAsyncSelectsAllEligibleJobsWhenMaxCandidateCountIsNotConfigured()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var now = DateTimeOffset.UtcNow;
        var firstJob = new JobRecord
        {
            LinkedInJobId = "job-all-1",
            LinkedInJobPostingUrn = "urn:li:jobPosting:all-1",
            Title = "First Eligible",
            Description = "First description",
            LastDetailSyncedAtUtc = now.AddMinutes(-14),
            LinkedInUpdatedAtUtc = now.AddMinutes(-10),
            FirstDiscoveredAtUtc = now.AddDays(-3),
            LastSeenAtUtc = now.AddMinutes(-10)
        };
        var secondJob = new JobRecord
        {
            LinkedInJobId = "job-all-2",
            LinkedInJobPostingUrn = "urn:li:jobPosting:all-2",
            Title = "Second Eligible",
            Description = "Second description",
            LastDetailSyncedAtUtc = now.AddMinutes(-13),
            LinkedInUpdatedAtUtc = now.AddMinutes(-9),
            FirstDiscoveredAtUtc = now.AddDays(-3),
            LastSeenAtUtc = now.AddMinutes(-9)
        };
        var thirdJob = new JobRecord
        {
            LinkedInJobId = "job-all-3",
            LinkedInJobPostingUrn = "urn:li:jobPosting:all-3",
            Title = "Third Eligible",
            Description = "Third description",
            LastDetailSyncedAtUtc = now.AddMinutes(-12),
            LinkedInUpdatedAtUtc = now.AddMinutes(-8),
            FirstDiscoveredAtUtc = now.AddDays(-3),
            LastSeenAtUtc = now.AddMinutes(-8)
        };

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.Jobs.AddRange(firstJob, secondJob, thirdJob);
            await seedContext.SaveChangesAsync();
        }

        var gateway = new SequenceGlobalShortlistGateway(
            [
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            thirdJob.Id.ToString("N"),
                            85,
                            80,
                            "Strong fit",
                            "None")
                    ],
                    "gpt-5-mini"),
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            secondJob.Id.ToString("N"),
                            70,
                            75,
                            "Moderate fit",
                            "Few gaps")
                    ],
                    "gpt-5-mini"),
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            firstJob.Id.ToString("N"),
                            55,
                            72,
                            "Some fit",
                            "Needs review")
                    ],
                    "gpt-5-mini")
            ]);

        var service = CreateService(
            dbOptions,
            gateway,
            new FixedJobScoringGateway(),
            null,
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = null,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var result = await service.GenerateAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.CandidateCount);
        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(3, gateway.SeenRequests.Count);
    }

    [Fact]
    public async Task GenerateAsyncCreatesSnapshotAndPersistsSequentialDecisions()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var now = DateTimeOffset.UtcNow;
        var acceptedJob = new JobRecord
        {
            LinkedInJobId = "job-1",
            LinkedInJobPostingUrn = "urn:li:jobPosting:1",
            Title = "Accepted Candidate",
            Description = "Senior .NET role",
            LastDetailSyncedAtUtc = now.AddMinutes(-12),
            LinkedInUpdatedAtUtc = now.AddMinutes(-5),
            FirstDiscoveredAtUtc = now.AddDays(-3),
            LastSeenAtUtc = now.AddMinutes(-6)
        };
        var rejectedJob = new JobRecord
        {
            LinkedInJobId = "job-2",
            LinkedInJobPostingUrn = "urn:li:jobPosting:2",
            Title = "Rejected Candidate",
            Description = "Junior role",
            LastDetailSyncedAtUtc = now.AddMinutes(-14),
            LinkedInUpdatedAtUtc = now.AddMinutes(-8),
            FirstDiscoveredAtUtc = now.AddDays(-4),
            LastSeenAtUtc = now.AddMinutes(-9)
        };
        var ignoredJob = new JobRecord
        {
            LinkedInJobId = "job-3",
            LinkedInJobPostingUrn = "urn:li:jobPosting:3",
            Title = "Ignored",
            Description = null,
            LastDetailSyncedAtUtc = now.AddMinutes(-20),
            FirstDiscoveredAtUtc = now.AddDays(-2),
            LastSeenAtUtc = now.AddMinutes(-21)
        };

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.Jobs.AddRange(acceptedJob, rejectedJob, ignoredJob);
            await seedContext.SaveChangesAsync();
        }

        var gateway = new SequenceGlobalShortlistGateway(
            [
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            acceptedJob.Id.ToString("N"),
                            92,
                            88,
                            "Strong fit",
                            "None")
                    ],
                    "gpt-5-mini"),
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            rejectedJob.Id.ToString("N"),
                            30,
                            75,
                            "Low fit",
                            "Skill mismatch")
                    ],
                    "gpt-5-mini")
            ]);

        var service = CreateService(
            dbOptions,
            gateway,
            new FixedJobScoringGateway(),
            null,
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = 10,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var result = await service.GenerateAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(1, result.ShortlistedCount);
        Assert.Equal(0, result.NeedsReviewCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, gateway.SeenRequests.Count);
        Assert.All(gateway.SeenRequests, static request => Assert.Single(request.Candidates));

        await using var verificationContext = new LinkedInJobScraperDbContext(dbOptions);
        var run = await verificationContext.AiGlobalShortlistRuns.SingleAsync();
        var candidates = await verificationContext.AiGlobalShortlistRunCandidates
            .OrderBy(candidate => candidate.SequenceNumber)
            .ToListAsync();
        var items = await verificationContext.AiGlobalShortlistItems
            .OrderBy(item => item.Rank)
            .ToListAsync();

        Assert.Equal("Completed", run.Status);
        Assert.Equal(2, run.CandidateCount);
        Assert.Equal(2, run.ProcessedCount);
        Assert.Equal(3, run.NextSequenceNumber);
        Assert.Equal("v-test", run.PromptVersion);
        Assert.Equal(2, candidates.Count);
        Assert.Equal("Accepted", candidates[0].Status);
        Assert.Equal("Rejected", candidates[1].Status);
        Assert.Equal(2, items.Count);
        Assert.Equal("Accepted", items[0].Decision);
        Assert.Equal("Rejected", items[1].Decision);
    }

    [Fact]
    public async Task GenerateAsyncSkipsJobsThatWereReviewedInPreviousRuns()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var now = DateTimeOffset.UtcNow;
        var alreadyReviewedJob = new JobRecord
        {
            LinkedInJobId = "job-reviewed",
            LinkedInJobPostingUrn = "urn:li:jobPosting:reviewed",
            Title = "Already Reviewed",
            Description = "Reviewed candidate",
            LastDetailSyncedAtUtc = now.AddMinutes(-12),
            FirstDiscoveredAtUtc = now.AddDays(-3),
            LastSeenAtUtc = now.AddMinutes(-10)
        };
        var newJob = new JobRecord
        {
            LinkedInJobId = "job-new",
            LinkedInJobPostingUrn = "urn:li:jobPosting:new",
            Title = "New Candidate",
            Description = "New candidate description",
            LastDetailSyncedAtUtc = now.AddMinutes(-11),
            FirstDiscoveredAtUtc = now.AddDays(-2),
            LastSeenAtUtc = now.AddMinutes(-9)
        };
        var historicalRunId = Guid.NewGuid();

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.Jobs.AddRange(alreadyReviewedJob, newJob);
            seedContext.AiGlobalShortlistRuns.Add(
                new AiGlobalShortlistRunRecord
                {
                    Id = historicalRunId,
                    CreatedAtUtc = now.AddDays(-1),
                    CompletedAtUtc = now.AddDays(-1).AddMinutes(5),
                    Status = "Completed",
                    CandidateCount = 1,
                    ProcessedCount = 1,
                    NextSequenceNumber = 2,
                    ShortlistedCount = 1
                });
            seedContext.AiGlobalShortlistItems.Add(
                new AiGlobalShortlistItemRecord
                {
                    RunId = historicalRunId,
                    JobRecordId = alreadyReviewedJob.Id,
                    Rank = 1,
                    Decision = "Accepted",
                    CreatedAtUtc = now.AddDays(-1).AddMinutes(3),
                    PromptVersion = "v-test",
                    ModelName = "gpt-5-mini",
                    Score = 90,
                    Confidence = 90,
                    RecommendationReason = "Historical review",
                    Concerns = "None"
                });

            await seedContext.SaveChangesAsync();
        }

        var gateway = new SequenceGlobalShortlistGateway(
            [
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            newJob.Id.ToString("N"),
                            82,
                            84,
                            "Good fit",
                            "None")
                    ],
                    "gpt-5-mini")
            ]);

        var service = CreateService(
            dbOptions,
            gateway,
            new FixedJobScoringGateway(),
            null,
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = 10,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var result = await service.GenerateAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Single(gateway.SeenRequests);
        Assert.Single(gateway.SeenRequests[0].Candidates);
        Assert.Equal(newJob.Id.ToString("N"), gateway.SeenRequests[0].Candidates[0].CandidateId);
    }

    [Fact]
    public async Task ResumeAsyncContinuesFromCheckpoint()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var now = DateTimeOffset.UtcNow;
        var firstJob = new JobRecord
        {
            LinkedInJobId = "job-11",
            LinkedInJobPostingUrn = "urn:li:jobPosting:11",
            Title = "First",
            Description = "First description",
            LastDetailSyncedAtUtc = now.AddMinutes(-20),
            FirstDiscoveredAtUtc = now.AddDays(-5),
            LastSeenAtUtc = now.AddMinutes(-21)
        };
        var secondJob = new JobRecord
        {
            LinkedInJobId = "job-12",
            LinkedInJobPostingUrn = "urn:li:jobPosting:12",
            Title = "Second",
            Description = "Second description",
            LastDetailSyncedAtUtc = now.AddMinutes(-18),
            FirstDiscoveredAtUtc = now.AddDays(-4),
            LastSeenAtUtc = now.AddMinutes(-19)
        };

        var runId = Guid.NewGuid();

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.Jobs.AddRange(firstJob, secondJob);

            seedContext.AiGlobalShortlistRuns.Add(
                new AiGlobalShortlistRunRecord
                {
                    Id = runId,
                    CreatedAtUtc = now.AddMinutes(-3),
                    Status = "Cancelled",
                    CandidateCount = 2,
                    ProcessedCount = 1,
                    NextSequenceNumber = 2,
                    ShortlistedCount = 1,
                    PromptVersion = "v-test",
                    ModelName = "gpt-5-mini",
                    CancellationRequestedAtUtc = now.AddMinutes(-2)
                });

            seedContext.AiGlobalShortlistRunCandidates.AddRange(
                new AiGlobalShortlistRunCandidateRecord
                {
                    RunId = runId,
                    JobRecordId = firstJob.Id,
                    SequenceNumber = 1,
                    Status = "Accepted",
                    ProcessedAtUtc = now.AddMinutes(-2)
                },
                new AiGlobalShortlistRunCandidateRecord
                {
                    RunId = runId,
                    JobRecordId = secondJob.Id,
                    SequenceNumber = 2,
                    Status = "Pending"
                });

            seedContext.AiGlobalShortlistItems.Add(
                new AiGlobalShortlistItemRecord
                {
                    RunId = runId,
                    JobRecordId = firstJob.Id,
                    Rank = 1,
                    Decision = "Accepted",
                    CreatedAtUtc = now.AddMinutes(-2),
                    PromptVersion = "v-test",
                    ModelName = "gpt-5-mini",
                    Score = 90,
                    Confidence = 85,
                    RecommendationReason = "Already processed",
                    Concerns = "None"
                });

            await seedContext.SaveChangesAsync();
        }

        var gateway = new SequenceGlobalShortlistGateway(
            [
                new AiGlobalShortlistBatchGatewayResult(
                    true,
                    "ok",
                    StatusCodes.Status200OK,
                    [
                        new AiGlobalShortlistBatchRecommendation(
                            secondJob.Id.ToString("N"),
                            87,
                            80,
                            "Resumed fit",
                            "None")
                    ],
                    "gpt-5-mini")
            ]);

        var service = CreateService(
            dbOptions,
            gateway,
            new FixedJobScoringGateway(),
            null,
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = 10,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var result = await service.ResumeAsync(runId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(2, result.ShortlistedCount);
        Assert.Single(gateway.SeenRequests);

        await using var verificationContext = new LinkedInJobScraperDbContext(dbOptions);
        var run = await verificationContext.AiGlobalShortlistRuns.SingleAsync(candidate => candidate.Id == runId);
        var candidates = await verificationContext.AiGlobalShortlistRunCandidates
            .Where(candidate => candidate.RunId == runId)
            .OrderBy(candidate => candidate.SequenceNumber)
            .ToListAsync();
        var items = await verificationContext.AiGlobalShortlistItems
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.Rank)
            .ToListAsync();

        Assert.Equal("Completed", run.Status);
        Assert.Equal(3, run.NextSequenceNumber);
        Assert.All(candidates, static candidate => Assert.NotEqual("Pending", candidate.Status));
        Assert.Equal(2, items.Count);
        Assert.Equal("Accepted", items[1].Decision);
    }

    [Fact]
    public async Task GenerateAsyncReturnsConflictWhenAnotherRunIsAlreadyActive()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var now = DateTimeOffset.UtcNow;
        var activeRunId = Guid.NewGuid();

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.AiGlobalShortlistRuns.Add(
                new AiGlobalShortlistRunRecord
                {
                    Id = activeRunId,
                    CreatedAtUtc = now.AddMinutes(-1),
                    Status = "Running",
                    CandidateCount = 200,
                    ProcessedCount = 30,
                    NextSequenceNumber = 31,
                    ShortlistedCount = 6,
                    NeedsReviewCount = 1,
                    FailedCount = 2
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(
            dbOptions,
            new SequenceGlobalShortlistGateway([]),
            new FixedJobScoringGateway(),
            new InMemoryProgressStateStore(activeRunIds: [activeRunId]),
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = 10,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var result = await service.GenerateAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(activeRunId, result.RunId);
        Assert.Contains("already running", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetLatestRunAsyncRecoversOrphanedActiveRunAfterRestart()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var runId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.AiGlobalShortlistRuns.Add(
                new AiGlobalShortlistRunRecord
                {
                    Id = runId,
                    CreatedAtUtc = createdAtUtc,
                    Status = "Running",
                    CandidateCount = 100,
                    ProcessedCount = 12,
                    NextSequenceNumber = 13,
                    Summary = "Cancellation requested. Run will stop at the next checkpoint."
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(
            dbOptions,
            new SequenceGlobalShortlistGateway([]),
            new FixedJobScoringGateway(),
            new InMemoryProgressStateStore(),
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = 10,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var snapshot = await service.GetLatestRunAsync(CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(runId, snapshot!.RunId);
        Assert.Equal("Cancelled", snapshot.Status);
        Assert.Contains("Recovered after application restart", snapshot.Summary, StringComparison.OrdinalIgnoreCase);

        await using var verificationContext = new LinkedInJobScraperDbContext(dbOptions);
        var persistedRun = await verificationContext.AiGlobalShortlistRuns.SingleAsync(candidate => candidate.Id == runId);
        Assert.Equal("Cancelled", persistedRun.Status);
        Assert.NotNull(persistedRun.CompletedAtUtc);
    }

    [Fact]
    public async Task RequestCancelAsyncReturnsCancellationRequestedMessage()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var dbOptions = new DbContextOptionsBuilder<LinkedInJobScraperDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();

        await using (var seedContext = new LinkedInJobScraperDbContext(dbOptions))
        {
            seedContext.AiGlobalShortlistRuns.Add(
                new AiGlobalShortlistRunRecord
                {
                    Id = runId,
                    CreatedAtUtc = now.AddMinutes(-1),
                    Status = "Running",
                    CandidateCount = 20,
                    ProcessedCount = 5,
                    NextSequenceNumber = 6
                });

            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(
            dbOptions,
            new SequenceGlobalShortlistGateway([]),
            new FixedJobScoringGateway(),
            null,
            new AiGlobalShortlistOptions
            {
                PromptVersion = "v-test",
                MaxCandidateCount = 10,
                InterCandidateDelayMilliseconds = 0,
                AcceptedScoreThreshold = 70,
                RejectedScoreThreshold = 40,
                FallbackPerItemCap = 0
            });

        var result = await service.RequestCancelAsync(runId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(runId, result.RunId);
        Assert.Equal(5, result.ProcessedCount);
        Assert.Contains("Cancellation requested", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AiGlobalShortlistService CreateService(
        DbContextOptions<LinkedInJobScraperDbContext> dbOptions,
        IAiGlobalShortlistGateway shortlistGateway,
        IJobScoringGateway jobScoringGateway,
        IAiGlobalShortlistProgressStateStore? progressStateStore,
        AiGlobalShortlistOptions shortlistOptions)
    {
        return new AiGlobalShortlistService(
            new TestDbContextFactory(dbOptions),
            shortlistGateway,
            new NullAiGlobalShortlistProgressNotifier(),
            progressStateStore ?? new InMemoryProgressStateStore(),
            jobScoringGateway,
            new FakeAiBehaviorSettingsService(),
            Options.Create(shortlistOptions),
            Options.Create(
                new OpenAiSecurityOptions
                {
                    ApiKey = "test-key",
                    Model = "gpt-5-mini"
                }),
            NullLogger<AiGlobalShortlistService>.Instance);
    }

    private sealed class NullAiGlobalShortlistProgressNotifier : IAiGlobalShortlistProgressNotifier
    {
        public Task PublishAsync(
            string? connectionId,
            AiGlobalShortlistProgressUpdate update,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProgressStateStore : IAiGlobalShortlistProgressStateStore
    {
        private readonly HashSet<Guid> _activeRunIds;

        public InMemoryProgressStateStore(IEnumerable<Guid>? activeRunIds = null)
        {
            _activeRunIds = activeRunIds is null
                ? []
                : [.. activeRunIds];
        }

        public AiGlobalShortlistProgressEvent Append(AiGlobalShortlistProgressUpdate update)
        {
            _activeRunIds.Add(update.RunId);
            return new AiGlobalShortlistProgressEvent(1, DateTimeOffset.UtcNow, update);
        }

        public AiGlobalShortlistProgressBatch GetBatch(Guid runId, long afterSequence)
        {
            return _activeRunIds.Contains(runId)
                ? new AiGlobalShortlistProgressBatch([], 1, true)
                : new AiGlobalShortlistProgressBatch([], 1, false);
        }
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

    private sealed class SequenceGlobalShortlistGateway : IAiGlobalShortlistGateway
    {
        private readonly Queue<AiGlobalShortlistBatchGatewayResult> _results;

        public SequenceGlobalShortlistGateway(IEnumerable<AiGlobalShortlistBatchGatewayResult> results)
        {
            _results = new Queue<AiGlobalShortlistBatchGatewayResult>(results);
        }

        public List<AiGlobalShortlistBatchGatewayRequest> SeenRequests { get; } = [];

        public Task<AiGlobalShortlistBatchGatewayResult> RankBatchAsync(
            AiGlobalShortlistBatchGatewayRequest request,
            CancellationToken cancellationToken)
        {
            SeenRequests.Add(request);
            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No prepared gateway result was available.");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FixedJobScoringGateway : IJobScoringGateway
    {
        public Task<JobScoringGatewayResult> ScoreAsync(
            JobScoringGatewayRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new JobScoringGatewayResult(
                    true,
                    "OpenAI scoring succeeded.",
                    StatusCodes.Status200OK,
                    85,
                    "Review",
                    "Fallback summary",
                    "Fallback recommendation reason",
                    "Fallback concerns"));
        }
    }
}
