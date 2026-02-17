namespace Lopen.Llm.Tests;

public class VerificationScopeTests
{
    [Fact]
    public void VerificationScope_HasThreeValues()
    {
        var values = Enum.GetValues<VerificationScope>();
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(VerificationScope.Task, 0)]
    [InlineData(VerificationScope.Component, 1)]
    [InlineData(VerificationScope.Module, 2)]
    public void VerificationScope_HasExpectedOrdinal(VerificationScope scope, int expected)
    {
        Assert.Equal(expected, (int)scope);
    }

    [Theory]
    [InlineData("Task", VerificationScope.Task)]
    [InlineData("Component", VerificationScope.Component)]
    [InlineData("Module", VerificationScope.Module)]
    public void VerificationScope_ParsesFromString(string name, VerificationScope expected)
    {
        Assert.Equal(expected, Enum.Parse<VerificationScope>(name));
    }
}
