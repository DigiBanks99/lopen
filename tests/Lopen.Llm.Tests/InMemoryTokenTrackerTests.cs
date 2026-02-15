namespace Lopen.Llm.Tests;

public class InMemoryTokenTrackerTests
{
    [Fact]
    public void GetSessionMetrics_Empty_ReturnsDefaults()
    {
        var tracker = new InMemoryTokenTracker();

        var metrics = tracker.GetSessionMetrics();

        Assert.Empty(metrics.PerIterationTokens);
        Assert.Equal(0, metrics.CumulativeInputTokens);
        Assert.Equal(0, metrics.CumulativeOutputTokens);
        Assert.Equal(0, metrics.PremiumRequestCount);
    }

    [Fact]
    public void RecordUsage_SingleIteration_Tracked()
    {
        var tracker = new InMemoryTokenTracker();
        var usage = new TokenUsage(100, 50, 150, 128000, true);

        tracker.RecordUsage(usage);
        var metrics = tracker.GetSessionMetrics();

        Assert.Single(metrics.PerIterationTokens);
        Assert.Equal(100, metrics.CumulativeInputTokens);
        Assert.Equal(50, metrics.CumulativeOutputTokens);
        Assert.Equal(1, metrics.PremiumRequestCount);
    }

    [Fact]
    public void RecordUsage_MultipleIterations_Cumulative()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 128000, true));
        tracker.RecordUsage(new TokenUsage(200, 100, 300, 128000, false));
        tracker.RecordUsage(new TokenUsage(300, 150, 450, 128000, true));

        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(3, metrics.PerIterationTokens.Count);
        Assert.Equal(600, metrics.CumulativeInputTokens);
        Assert.Equal(300, metrics.CumulativeOutputTokens);
        Assert.Equal(2, metrics.PremiumRequestCount);
    }

    [Fact]
    public void RecordUsage_NonPremium_DoesNotIncrementPremiumCount()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 64000, false));

        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(0, metrics.PremiumRequestCount);
    }

    [Fact]
    public void ResetSession_ClearsAllMetrics()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 128000, true));
        tracker.RecordUsage(new TokenUsage(200, 100, 300, 128000, true));

        tracker.ResetSession();
        var metrics = tracker.GetSessionMetrics();

        Assert.Empty(metrics.PerIterationTokens);
        Assert.Equal(0, metrics.CumulativeInputTokens);
        Assert.Equal(0, metrics.CumulativeOutputTokens);
        Assert.Equal(0, metrics.PremiumRequestCount);
    }

    [Fact]
    public void RecordUsage_AfterReset_StartsClean()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 128000, true));
        tracker.ResetSession();
        tracker.RecordUsage(new TokenUsage(200, 100, 300, 128000, false));

        var metrics = tracker.GetSessionMetrics();

        Assert.Single(metrics.PerIterationTokens);
        Assert.Equal(200, metrics.CumulativeInputTokens);
        Assert.Equal(100, metrics.CumulativeOutputTokens);
        Assert.Equal(0, metrics.PremiumRequestCount);
    }

    [Fact]
    public void RecordUsage_NullUsage_ThrowsArgumentNull()
    {
        var tracker = new InMemoryTokenTracker();
        Assert.Throws<ArgumentNullException>(() => tracker.RecordUsage(null!));
    }

    [Fact]
    public void GetSessionMetrics_ReturnsSnapshot_NotReference()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 128000, true));

        var first = tracker.GetSessionMetrics();
        tracker.RecordUsage(new TokenUsage(200, 100, 300, 128000, false));
        var second = tracker.GetSessionMetrics();

        Assert.Single(first.PerIterationTokens);
        Assert.Equal(2, second.PerIterationTokens.Count);
    }
}
