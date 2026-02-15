using Lopen.Configuration;
using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class ToolDisciplineGuardrailTests
{
    [Fact]
    public async Task EvaluateAsync_BelowThreshold_ReturnsPass()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 30);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AboveThreshold_ReturnsWarn()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 60);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Warn>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AboveDoubleThreshold_ReturnsStrongerWarn()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 110);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("Excessive", warn.Message);
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyAtThreshold_ReturnsPass()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 50);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_WarnMessage_ContainsCount()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 75);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("75", warn.Message);
    }

    [Fact]
    public void Order_Returns400()
    {
        var guardrail = new ToolDisciplineGuardrail();

        Assert.Equal(400, guardrail.Order);
    }

    [Fact]
    public void ShortCircuitOnBlock_ReturnsFalse()
    {
        var guardrail = new ToolDisciplineGuardrail();

        Assert.False(guardrail.ShortCircuitOnBlock);
    }

    [Fact]
    public void Constructor_ZeroThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolDisciplineGuardrail(toolCallThreshold: 0));
    }

    [Fact]
    public void ImplementsIGuardrail()
    {
        Assert.IsAssignableFrom<IGuardrail>(new ToolDisciplineGuardrail());
    }

    [Fact]
    public async Task EvaluateAsync_FileReadExceedsMax_ReturnsWarn()
    {
        var guardrail = new ToolDisciplineGuardrail(maxFileReads: 3);
        var fileReads = new Dictionary<string, int> { ["src/Program.cs"] = 5 };
        var context = new GuardrailContext("mod", "task", 1, 10, FileReadCounts: fileReads);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("Program.cs", warn.Message);
        Assert.Contains("5", warn.Message);
    }

    [Fact]
    public async Task EvaluateAsync_FileReadAtMax_ReturnsPass()
    {
        var guardrail = new ToolDisciplineGuardrail(maxFileReads: 3);
        var fileReads = new Dictionary<string, int> { ["src/Program.cs"] = 3 };
        var context = new GuardrailContext("mod", "task", 1, 10, FileReadCounts: fileReads);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_CommandRetryExceedsMax_ReturnsWarn()
    {
        var guardrail = new ToolDisciplineGuardrail(maxCommandRetries: 3);
        var cmdRetries = new Dictionary<string, int> { ["dotnet test"] = 5 };
        var context = new GuardrailContext("mod", "task", 1, 10, CommandRetryCounts: cmdRetries);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("dotnet test", warn.Message);
        Assert.Contains("5", warn.Message);
    }

    [Fact]
    public async Task EvaluateAsync_CommandRetryAtMax_ReturnsPass()
    {
        var guardrail = new ToolDisciplineGuardrail(maxCommandRetries: 3);
        var cmdRetries = new Dictionary<string, int> { ["dotnet test"] = 3 };
        var context = new GuardrailContext("mod", "task", 1, 10, CommandRetryCounts: cmdRetries);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleViolations_CombinesWarnings()
    {
        var guardrail = new ToolDisciplineGuardrail(
            toolCallThreshold: 10, maxFileReads: 2, maxCommandRetries: 2);
        var fileReads = new Dictionary<string, int> { ["file.cs"] = 5 };
        var cmdRetries = new Dictionary<string, int> { ["npm test"] = 4 };
        var context = new GuardrailContext("mod", "task", 1, 25, fileReads, cmdRetries);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("file.cs", warn.Message);
        Assert.Contains("npm test", warn.Message);
        Assert.Contains("25", warn.Message);
    }

    [Fact]
    public void Constructor_FromOptions_UsesConfigValues()
    {
        var options = new ToolDisciplineOptions
        {
            MaxFileReads = 5,
            MaxCommandRetries = 7,
        };
        var guardrail = new ToolDisciplineGuardrail(options);

        Assert.Equal(400, guardrail.Order);
    }

    [Fact]
    public async Task EvaluateAsync_FromOptions_RespectsConfiguredMaxFileReads()
    {
        var options = new ToolDisciplineOptions { MaxFileReads = 5 };
        var guardrail = new ToolDisciplineGuardrail(options);
        var fileReads = new Dictionary<string, int> { ["file.cs"] = 4 };
        var context = new GuardrailContext("mod", "task", 1, 10, FileReadCounts: fileReads);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_FromOptions_RespectsConfiguredMaxCommandRetries()
    {
        var options = new ToolDisciplineOptions { MaxCommandRetries = 1 };
        var guardrail = new ToolDisciplineGuardrail(options);
        var cmdRetries = new Dictionary<string, int> { ["cmd"] = 2 };
        var context = new GuardrailContext("mod", "task", 1, 10, CommandRetryCounts: cmdRetries);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Warn>(result);
    }

    [Fact]
    public void Constructor_ZeroMaxFileReads_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolDisciplineGuardrail(maxFileReads: 0));
    }

    [Fact]
    public void Constructor_ZeroMaxCommandRetries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolDisciplineGuardrail(maxCommandRetries: 0));
    }

    [Fact]
    public async Task EvaluateAsync_NullFileReadCounts_SkipsFileCheck()
    {
        var guardrail = new ToolDisciplineGuardrail(maxFileReads: 1);
        var context = new GuardrailContext("mod", "task", 1, 10, FileReadCounts: null);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_NullCommandRetryCounts_SkipsRetryCheck()
    {
        var guardrail = new ToolDisciplineGuardrail(maxCommandRetries: 1);
        var context = new GuardrailContext("mod", "task", 1, 10, CommandRetryCounts: null);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }
}
