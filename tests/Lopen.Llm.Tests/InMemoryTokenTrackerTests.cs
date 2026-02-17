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

    // --- RestoreMetrics tests (LLM-13) ---

    [Fact]
    public void RestoreMetrics_SetsCumulativeValues()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RestoreMetrics(1000, 500, 3);

        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(1000, metrics.CumulativeInputTokens);
        Assert.Equal(500, metrics.CumulativeOutputTokens);
        Assert.Equal(3, metrics.PremiumRequestCount);
        Assert.Empty(metrics.PerIterationTokens);
    }

    [Fact]
    public void RestoreMetrics_ThenRecord_AccumulatesOnTop()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RestoreMetrics(1000, 500, 2);
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 128000, true));

        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(1100, metrics.CumulativeInputTokens);
        Assert.Equal(550, metrics.CumulativeOutputTokens);
        Assert.Equal(3, metrics.PremiumRequestCount);
        Assert.Single(metrics.PerIterationTokens);
    }

    [Fact]
    public void RoundTrip_SaveRestore_ValuesMonotonicallyIncrease()
    {
        var tracker = new InMemoryTokenTracker();

        // First session: record some usage
        tracker.RecordUsage(new TokenUsage(500, 200, 700, 128000, true));
        tracker.RecordUsage(new TokenUsage(300, 100, 400, 128000, false));
        var saved = tracker.GetSessionMetrics();

        // Simulate save → new tracker → restore
        var tracker2 = new InMemoryTokenTracker();
        tracker2.RestoreMetrics(saved.CumulativeInputTokens, saved.CumulativeOutputTokens, saved.PremiumRequestCount);

        // Record more usage in new session
        tracker2.RecordUsage(new TokenUsage(200, 50, 250, 64000, true));

        var final_ = tracker2.GetSessionMetrics();

        // Values should be strictly greater than saved
        Assert.True(final_.CumulativeInputTokens > saved.CumulativeInputTokens);
        Assert.True(final_.CumulativeOutputTokens > saved.CumulativeOutputTokens);
        Assert.True(final_.PremiumRequestCount > saved.PremiumRequestCount);

        // Exact values
        Assert.Equal(1000, final_.CumulativeInputTokens);
        Assert.Equal(350, final_.CumulativeOutputTokens);
        Assert.Equal(2, final_.PremiumRequestCount);
    }

    [Fact]
    public void RestoreMetrics_ThenReset_ClearsEverything()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RestoreMetrics(1000, 500, 3);
        tracker.ResetSession();

        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(0, metrics.CumulativeInputTokens);
        Assert.Equal(0, metrics.CumulativeOutputTokens);
        Assert.Equal(0, metrics.PremiumRequestCount);
    }

    // --- RestoreMetrics with prior iterations (STOR-03) ---

    [Fact]
    public void RestoreMetrics_WithPriorIterations_RestoresIterationHistory()
    {
        var tracker = new InMemoryTokenTracker();
        var priorIterations = new List<TokenUsage>
        {
            new(400, 200, 600, 128000, true),
            new(300, 150, 450, 128000, false),
        };

        tracker.RestoreMetrics(700, 350, 1, priorIterations);
        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(2, metrics.PerIterationTokens.Count);
        Assert.Equal(400, metrics.PerIterationTokens[0].InputTokens);
        Assert.Equal(300, metrics.PerIterationTokens[1].InputTokens);
        Assert.Equal(700, metrics.CumulativeInputTokens);
        Assert.Equal(350, metrics.CumulativeOutputTokens);
    }

    [Fact]
    public void RestoreMetrics_WithPriorIterations_ThenRecord_AppendsToHistory()
    {
        var tracker = new InMemoryTokenTracker();
        var priorIterations = new List<TokenUsage>
        {
            new(400, 200, 600, 128000, true),
        };

        tracker.RestoreMetrics(400, 200, 1, priorIterations);
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 64000, false));
        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(2, metrics.PerIterationTokens.Count);
        Assert.Equal(400, metrics.PerIterationTokens[0].InputTokens);
        Assert.Equal(100, metrics.PerIterationTokens[1].InputTokens);
        Assert.Equal(500, metrics.CumulativeInputTokens);
        Assert.Equal(250, metrics.CumulativeOutputTokens);
    }

    [Fact]
    public void RestoreMetrics_WithNullIterations_DoesNotClearExisting()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(100, 50, 150, 128000, false));

        tracker.RestoreMetrics(1000, 500, 2, null);
        var metrics = tracker.GetSessionMetrics();

        Assert.Single(metrics.PerIterationTokens);
        Assert.Equal(1000, metrics.CumulativeInputTokens);
    }

    [Fact]
    public void RoundTrip_SaveRestore_WithIterations_PreservesFullHistory()
    {
        var tracker = new InMemoryTokenTracker();
        tracker.RecordUsage(new TokenUsage(500, 200, 700, 128000, true));
        tracker.RecordUsage(new TokenUsage(300, 100, 400, 128000, false));
        var saved = tracker.GetSessionMetrics();

        var tracker2 = new InMemoryTokenTracker();
        tracker2.RestoreMetrics(
            saved.CumulativeInputTokens,
            saved.CumulativeOutputTokens,
            saved.PremiumRequestCount,
            saved.PerIterationTokens.ToList());
        tracker2.RecordUsage(new TokenUsage(200, 50, 250, 64000, true));

        var final_ = tracker2.GetSessionMetrics();

        Assert.Equal(3, final_.PerIterationTokens.Count);
        Assert.Equal(500, final_.PerIterationTokens[0].InputTokens);
        Assert.Equal(300, final_.PerIterationTokens[1].InputTokens);
        Assert.Equal(200, final_.PerIterationTokens[2].InputTokens);
        Assert.Equal(1000, final_.CumulativeInputTokens);
        Assert.Equal(350, final_.CumulativeOutputTokens);
    }
}
