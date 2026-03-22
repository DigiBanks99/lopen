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

    [Fact]
    public void LlmException_DiagnosticCategory_DefaultsToNull()
    {
        var ex = new LlmException("error");
        Assert.Null(ex.DiagnosticCategory);
    }

    [Fact]
    public void LlmException_UserHint_DefaultsToNull()
    {
        var ex = new LlmException("error");
        Assert.Null(ex.UserHint);
    }

    [Fact]
    public void LlmException_WithDiagnosticCategory_IsPreserved()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new LlmException("msg", model: null, inner)
        {
            DiagnosticCategory = CopilotFailureCategory.Auth,
            UserHint = "Run 'lopen auth login'",
        };

        Assert.Equal(CopilotFailureCategory.Auth, ex.DiagnosticCategory);
        Assert.Equal("Run 'lopen auth login'", ex.UserHint);
    }

    [Fact]
    public void LlmException_AllCategories_AreValid()
    {
        foreach (CopilotFailureCategory cat in Enum.GetValues<CopilotFailureCategory>())
        {
            var ex = new LlmException("error") { DiagnosticCategory = cat };
            Assert.Equal(cat, ex.DiagnosticCategory);
        }
    }
}
