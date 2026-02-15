namespace Lopen.Llm.Tests;

public class OracleVerdictTests
{
    [Fact]
    public void OracleVerdict_PassingResult()
    {
        var verdict = new OracleVerdict(Passed: true, Gaps: [], Scope: VerificationScope.Task);

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Gaps);
        Assert.Equal(VerificationScope.Task, verdict.Scope);
    }

    [Fact]
    public void OracleVerdict_FailingResultWithGaps()
    {
        var gaps = new List<string> { "Missing test for edge case", "No error handling" };
        var verdict = new OracleVerdict(
            Passed: false,
            Gaps: gaps,
            Scope: VerificationScope.Component);

        Assert.False(verdict.Passed);
        Assert.Equal(2, verdict.Gaps.Count);
        Assert.Equal(VerificationScope.Component, verdict.Scope);
    }

    [Fact]
    public void OracleVerdict_ModuleScope()
    {
        var verdict = new OracleVerdict(true, [], VerificationScope.Module);
        Assert.Equal(VerificationScope.Module, verdict.Scope);
    }

    [Fact]
    public void OracleVerdict_EqualityByValue()
    {
        var a = new OracleVerdict(true, [], VerificationScope.Task);
        var b = new OracleVerdict(true, [], VerificationScope.Task);

        Assert.Equal(a, b);
    }
}
