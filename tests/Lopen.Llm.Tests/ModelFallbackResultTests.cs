namespace Lopen.Llm.Tests;

public class ModelFallbackResultTests
{
    [Fact]
    public void ModelFallbackResult_NoFallback()
    {
        var result = new ModelFallbackResult("claude-opus-4.6", WasFallback: false);

        Assert.Equal("claude-opus-4.6", result.SelectedModel);
        Assert.False(result.WasFallback);
        Assert.Null(result.OriginalModel);
    }

    [Fact]
    public void ModelFallbackResult_WithFallback()
    {
        var result = new ModelFallbackResult(
            "claude-sonnet-4",
            WasFallback: true,
            OriginalModel: "");

        Assert.Equal("claude-sonnet-4", result.SelectedModel);
        Assert.True(result.WasFallback);
        Assert.Equal("", result.OriginalModel);
    }

    [Fact]
    public void ModelFallbackResult_EqualityByValue()
    {
        var a = new ModelFallbackResult("claude-opus-4.6", false);
        var b = new ModelFallbackResult("claude-opus-4.6", false);

        Assert.Equal(a, b);
    }
}
