using LinkedIn.JobScraper.Web.Jobs;

namespace LinkedIn.JobScraper.Web.Tests.Jobs;

public sealed class JobsWorkflowTrackingTests
{
    [Fact]
    public void RegisterWorkflowIsolatedPerUser()
    {
        var store = new InMemoryJobsWorkflowStateStore();

        var firstUserRegistration = store.RegisterWorkflow(1, "workflow-a", CancellationToken.None);
        var secondUserRegistration = store.RegisterWorkflow(2, "workflow-b", CancellationToken.None);
        var duplicateFirstUserRegistration = store.RegisterWorkflow(1, "workflow-c", CancellationToken.None);

        Assert.True(firstUserRegistration.Accepted);
        Assert.True(secondUserRegistration.Accepted);
        Assert.False(duplicateFirstUserRegistration.Accepted);
        Assert.Equal("workflow-a", duplicateFirstUserRegistration.ActiveWorkflowId);
    }

    [Fact]
    public void BatchAndCancellationAreScopedByUser()
    {
        var store = new InMemoryJobsWorkflowStateStore();
        _ = store.RegisterWorkflow(1, "workflow-a", CancellationToken.None);
        _ = store.RegisterWorkflow(2, "workflow-b", CancellationToken.None);

        store.Append(
            1,
            new JobsWorkflowProgressUpdate(
                "workflow-a",
                "corr-1",
                "running",
                "fetch",
                10,
                "User 1 update"));

        var ownBatch = store.GetBatch(1, "workflow-a", 0);
        var crossUserBatch = store.GetBatch(2, "workflow-a", 0);
        var secondUserOwnBatch = store.GetBatch(2, "workflow-b", 0);

        Assert.True(ownBatch.WorkflowFound);
        Assert.Single(ownBatch.Events);
        Assert.Equal("corr-1", ownBatch.Events[0].Update.CorrelationId);
        Assert.False(crossUserBatch.WorkflowFound);
        Assert.Empty(crossUserBatch.Events);
        Assert.True(secondUserOwnBatch.WorkflowFound);
        Assert.Empty(secondUserOwnBatch.Events);

        Assert.False(store.RequestCancellation(2, "workflow-missing"));
        Assert.False(store.RequestCancellation(2, "workflow-a"));
        Assert.True(store.RequestCancellation(1, "workflow-a"));
    }
}
