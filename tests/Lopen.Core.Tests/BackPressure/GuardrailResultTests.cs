using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class GuardrailResultTests
{
    [Fact]
    public void Pass_IsGuardrailResult()
    {
        GuardrailResult result = new GuardrailResult.Pass();
        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public void Warn_ContainsMessage()
    {
        var result = new GuardrailResult.Warn("80% budget consumed");
        Assert.Equal("80% budget consumed", result.Message);
    }

    [Fact]
    public void Block_ContainsMessage()
    {
        var result = new GuardrailResult.Block("Budget exceeded");
        Assert.Equal("Budget exceeded", result.Message);
    }

    [Fact]
    public void Pass_EqualityWorks()
    {
        var a = new GuardrailResult.Pass();
        var b = new GuardrailResult.Pass();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Warn_EqualityByMessage()
    {
        var a = new GuardrailResult.Warn("msg");
        var b = new GuardrailResult.Warn("msg");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Warn_InequalityByMessage()
    {
        var a = new GuardrailResult.Warn("msg1");
        var b = new GuardrailResult.Warn("msg2");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Block_EqualityByMessage()
    {
        var a = new GuardrailResult.Block("msg");
        var b = new GuardrailResult.Block("msg");
        Assert.Equal(a, b);
    }

    [Fact]
    public void PatternMatching_WorksForAllTypes()
    {
        GuardrailResult[] results =
        [
            new GuardrailResult.Pass(),
            new GuardrailResult.Warn("warning"),
            new GuardrailResult.Block("blocked"),
        ];

        var messages = results.Select(r => r switch
        {
            GuardrailResult.Pass => "pass",
            GuardrailResult.Warn w => w.Message,
            GuardrailResult.Block b => b.Message,
            _ => "unknown",
        }).ToList();

        Assert.Equal(["pass", "warning", "blocked"], messages);
    }
}
