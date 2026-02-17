namespace Lopen.Llm.Tests;

public class LlmInvocationResultTests
{
    [Fact]
    public void LlmInvocationResult_StoresAllProperties()
    {
        var usage = new TokenUsage(100, 50, 150, 128000, true);
        var result = new LlmInvocationResult(
            Output: "Generated code",
            TokenUsage: usage,
            ToolCallsMade: 3,
            IsComplete: true);

        Assert.Equal("Generated code", result.Output);
        Assert.Equal(usage, result.TokenUsage);
        Assert.Equal(3, result.ToolCallsMade);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void LlmInvocationResult_IncompleteResult()
    {
        var usage = new TokenUsage(50, 25, 75, 64000, false);
        var result = new LlmInvocationResult("Partial output", usage, 1, false);

        Assert.False(result.IsComplete);
    }

    [Fact]
    public void LlmInvocationResult_EqualityByValue()
    {
        var usage = new TokenUsage(100, 50, 150, 128000, true);
        var a = new LlmInvocationResult("output", usage, 2, true);
        var b = new LlmInvocationResult("output", usage, 2, true);

        Assert.Equal(a, b);
    }
}
