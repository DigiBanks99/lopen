using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class GuardrailContextTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var context = new GuardrailContext("auth", "implement-jwt", 3, 15);

        Assert.Equal("auth", context.ModuleName);
        Assert.Equal("implement-jwt", context.TaskName);
        Assert.Equal(3, context.IterationCount);
        Assert.Equal(15, context.ToolCallCount);
    }

    [Fact]
    public void Constructor_AllowsNullTaskName()
    {
        var context = new GuardrailContext("auth", null, 1, 0);
        Assert.Null(context.TaskName);
    }

    [Fact]
    public void Equality_WorksByValue()
    {
        var a = new GuardrailContext("auth", "task1", 1, 5);
        var b = new GuardrailContext("auth", "task1", 1, 5);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Inequality_DifferentValues()
    {
        var a = new GuardrailContext("auth", "task1", 1, 5);
        var b = new GuardrailContext("auth", "task2", 1, 5);
        Assert.NotEqual(a, b);
    }
}
