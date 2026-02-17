namespace Lopen.Llm.Tests;

public class LlmExceptionTests
{
    [Fact]
    public void LlmException_MessageOnly()
    {
        var ex = new LlmException("SDK unavailable");

        Assert.Equal("SDK unavailable", ex.Message);
        Assert.Null(ex.Model);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void LlmException_WithModel()
    {
        var ex = new LlmException("Rate limited", "claude-opus-4.6");

        Assert.Equal("Rate limited", ex.Message);
        Assert.Equal("claude-opus-4.6", ex.Model);
    }

    [Fact]
    public void LlmException_WithInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new LlmException("SDK error", "gpt-5-mini", inner);

        Assert.Equal("SDK error", ex.Message);
        Assert.Equal("gpt-5-mini", ex.Model);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void LlmException_InheritsFromException()
    {
        var ex = new LlmException("test");
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void LlmException_NullModel_IsAllowed()
    {
        var ex = new LlmException("error", model: null);

        Assert.Null(ex.Model);
    }
}
