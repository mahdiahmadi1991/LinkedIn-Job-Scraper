using LinkedIn.JobScraper.Web.AI;

namespace LinkedIn.JobScraper.Web.Tests.AI;

public sealed class AiGlobalShortlistRealtimeTests
{
    [Fact]
    public void ProgressStateIsolatedPerUserAndRun()
    {
        var store = new InMemoryAiGlobalShortlistProgressStateStore();
        var runId = Guid.NewGuid();

        store.Append(
            1,
            new AiGlobalShortlistProgressUpdate(
                runId,
                "running",
                "candidate-processed",
                "User 1 processed candidate",
                CandidateCount: 10,
                ProcessedCount: 1,
                AcceptedCount: 1));

        var userOneBatch = store.GetBatch(1, runId, 0);
        var userTwoBatch = store.GetBatch(2, runId, 0);

        Assert.True(userOneBatch.RunFound);
        Assert.Single(userOneBatch.Events);
        Assert.Equal(1, userOneBatch.Events[0].Sequence);
        Assert.False(userTwoBatch.RunFound);
        Assert.Empty(userTwoBatch.Events);
    }
}
