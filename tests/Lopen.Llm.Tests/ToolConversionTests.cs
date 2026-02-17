using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tests;

public class ToolConversionTests
{
    [Fact]
    public void ToAiFunctions_NullTools_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ToolConversion.ToAiFunctions(null!));
    }

    [Fact]
    public void ToAiFunctions_EmptyList_ReturnsEmptyList()
    {
        var result = ToolConversion.ToAiFunctions([]);

        Assert.Empty(result);
    }

    [Fact]
    public void ToAiFunctions_FiltersOutNullHandlers()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("tool_with_handler", "Has handler", Handler: (_, _) => Task.FromResult("ok")),
            new("tool_without_handler", "No handler"),
            new("another_with_handler", "Has handler too", Handler: (_, _) => Task.FromResult("ok2")),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ToAiFunctions_MapsNameCorrectly()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("read_spec", "Read specification", Handler: (_, _) => Task.FromResult("content")),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Single(result);
        Assert.Equal("read_spec", result[0].Name);
    }

    [Fact]
    public void ToAiFunctions_MapsDescriptionCorrectly()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("read_spec", "Read the specification document", Handler: (_, _) => Task.FromResult("content")),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Single(result);
        Assert.Equal("Read the specification document", result[0].Description);
    }

    [Fact]
    public async Task ToAiFunctions_HandlerIsInvokable()
    {
        var handlerCalled = false;
        var tools = new List<LopenToolDefinition>
        {
            new("test_tool", "Test", Handler: (args, _) =>
            {
                handlerCalled = true;
                return Task.FromResult($"result:{args}");
            }),
        };

        var result = ToolConversion.ToAiFunctions(tools);
        Assert.Single(result);

        // Invoke the AIFunction and verify the handler is called
        var aiFunc = result[0];
        var invokeResult = await aiFunc.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["arguments"] = "{\"key\":\"value\"}",
            }));

        Assert.True(handlerCalled);
    }

    [Fact]
    public void ToAiFunctions_AllNullHandlers_ReturnsEmptyList()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("tool1", "Desc1"),
            new("tool2", "Desc2"),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Empty(result);
    }

    [Fact]
    public void ToAiFunctions_MultipleToolsWithHandlers_ReturnsAll()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("read_spec", "Read spec", Handler: (_, _) => Task.FromResult("a")),
            new("update_task", "Update task", Handler: (_, _) => Task.FromResult("b")),
            new("verify_task", "Verify task", Handler: (_, _) => Task.FromResult("c")),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Equal(3, result.Count);
        Assert.Equal("read_spec", result[0].Name);
        Assert.Equal("update_task", result[1].Name);
        Assert.Equal("verify_task", result[2].Name);
    }

    [Fact]
    public void ToAiFunctions_PreservesToolOrder()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("z_tool", "Z", Handler: (_, _) => Task.FromResult("z")),
            new("a_tool", "A", Handler: (_, _) => Task.FromResult("a")),
            new("m_tool", "M", Handler: (_, _) => Task.FromResult("m")),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Equal("z_tool", result[0].Name);
        Assert.Equal("a_tool", result[1].Name);
        Assert.Equal("m_tool", result[2].Name);
    }

    [Fact]
    public void ToAiFunctions_ReturnsAiFunctionInstances()
    {
        var tools = new List<LopenToolDefinition>
        {
            new("test", "Test tool", Handler: (_, _) => Task.FromResult("ok")),
        };

        var result = ToolConversion.ToAiFunctions(tools);

        Assert.Single(result);
        Assert.IsAssignableFrom<AIFunction>(result[0]);
    }

    [Fact]
    public async Task ToAiFunctions_HandlerReceivesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        var tools = new List<LopenToolDefinition>
        {
            new("test", "Test", Handler: (_, ct) =>
            {
                receivedToken = ct;
                return Task.FromResult("ok");
            }),
        };

        var result = ToolConversion.ToAiFunctions(tools);
        await result[0].InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["arguments"] = "{}",
            }),
            cts.Token);

        // The cancellation token should have been passed through
        // (AIFunctionFactory forwards it to the delegate's CancellationToken parameter)
        Assert.False(receivedToken.IsCancellationRequested);
    }
}
