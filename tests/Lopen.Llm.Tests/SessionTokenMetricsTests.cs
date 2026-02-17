namespace Lopen.Llm.Tests;

public class SessionTokenMetricsTests
{
    [Fact]
    public void SessionTokenMetrics_DefaultValues()
    {
        var metrics = new SessionTokenMetrics();

        Assert.Empty(metrics.PerIterationTokens);
        Assert.Equal(0, metrics.CumulativeInputTokens);
        Assert.Equal(0, metrics.CumulativeOutputTokens);
        Assert.Equal(0, metrics.PremiumRequestCount);
    }

    [Fact]
    public void SessionTokenMetrics_WithValues()
    {
        var iterations = new List<TokenUsage>
        {
            new(100, 50, 150, 128000, true),
            new(200, 100, 300, 128000, false),
        };

        var metrics = new SessionTokenMetrics
        {
            PerIterationTokens = iterations,
            CumulativeInputTokens = 300,
            CumulativeOutputTokens = 150,
            PremiumRequestCount = 1,
        };

        Assert.Equal(2, metrics.PerIterationTokens.Count);
        Assert.Equal(300, metrics.CumulativeInputTokens);
        Assert.Equal(150, metrics.CumulativeOutputTokens);
        Assert.Equal(1, metrics.PremiumRequestCount);
    }

    [Fact]
    public void SessionTokenMetrics_EqualityByValue()
    {
        var a = new SessionTokenMetrics
        {
            CumulativeInputTokens = 100,
            CumulativeOutputTokens = 50,
            PremiumRequestCount = 1,
        };
        var b = new SessionTokenMetrics
        {
            CumulativeInputTokens = 100,
            CumulativeOutputTokens = 50,
            PremiumRequestCount = 1,
        };

        Assert.Equal(a, b);
    }
}
