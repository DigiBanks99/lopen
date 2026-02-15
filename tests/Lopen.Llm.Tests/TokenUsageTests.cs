namespace Lopen.Llm.Tests;

public class TokenUsageTests
{
    [Fact]
    public void TokenUsage_StoresAllProperties()
    {
        var usage = new TokenUsage(
            InputTokens: 100,
            OutputTokens: 50,
            TotalTokens: 150,
            ContextWindowSize: 128000,
            IsPremiumRequest: true);

        Assert.Equal(100, usage.InputTokens);
        Assert.Equal(50, usage.OutputTokens);
        Assert.Equal(150, usage.TotalTokens);
        Assert.Equal(128000, usage.ContextWindowSize);
        Assert.True(usage.IsPremiumRequest);
    }

    [Fact]
    public void TokenUsage_NonPremiumRequest()
    {
        var usage = new TokenUsage(200, 100, 300, 64000, false);

        Assert.False(usage.IsPremiumRequest);
    }

    [Fact]
    public void TokenUsage_EqualityByValue()
    {
        var a = new TokenUsage(100, 50, 150, 128000, true);
        var b = new TokenUsage(100, 50, 150, 128000, true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void TokenUsage_InequalityWhenDifferent()
    {
        var a = new TokenUsage(100, 50, 150, 128000, true);
        var b = new TokenUsage(200, 50, 250, 128000, true);

        Assert.NotEqual(a, b);
    }
}
