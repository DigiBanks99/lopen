using System.CommandLine;
using Lopen.Commands;

namespace Lopen.Cli.Tests.Commands;

public class GlobalOptionsTests
{
    [Fact]
    public async Task Version_Flag_ReturnsZero()
    {
        var root = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(root);
        root.SetAction((ParseResult _) => 0);
        var config = new CommandLineConfiguration(root);

        var exitCode = await config.InvokeAsync(["--version"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Help_Flag_ReturnsZero()
    {
        var root = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(root);
        root.SetAction((ParseResult _) => 0);
        var config = new CommandLineConfiguration(root);

        var exitCode = await config.InvokeAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Headless_Option_HasExpectedAliases()
    {
        var aliases = GlobalOptions.Headless.Aliases;
        Assert.Contains("-q", aliases);
        Assert.Contains("--quiet", aliases);
    }

    [Fact]
    public void Prompt_Option_HasExpectedAlias()
    {
        var aliases = GlobalOptions.Prompt.Aliases;
        Assert.Contains("-p", aliases);
    }

    [Fact]
    public void Headless_Option_IsRecursive()
    {
        Assert.True(GlobalOptions.Headless.Recursive);
    }

    [Fact]
    public void Prompt_Option_IsRecursive()
    {
        Assert.True(GlobalOptions.Prompt.Recursive);
    }

    // ==================== New CLI flags (CFG-08, CFG-09, CFG-11) ====================

    [Fact]
    public void Model_Option_IsRecursive()
    {
        Assert.True(GlobalOptions.Model.Recursive);
    }

    [Fact]
    public void Unattended_Option_IsRecursive()
    {
        Assert.True(GlobalOptions.Unattended.Recursive);
    }

    [Fact]
    public void MaxIterations_Option_IsRecursive()
    {
        Assert.True(GlobalOptions.MaxIterations.Recursive);
    }

    [Fact]
    public void AddTo_RegistersAllEightOptions()
    {
        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);

        var optionNames = root.Options.Select(o => o.Name).ToList();
        Assert.Contains("--headless", optionNames);
        Assert.Contains("--prompt", optionNames);
        Assert.Contains("--resume", optionNames);
        Assert.Contains("--no-resume", optionNames);
        Assert.Contains("--no-welcome", optionNames);
        Assert.Contains("--model", optionNames);
        Assert.Contains("--unattended", optionNames);
        Assert.Contains("--max-iterations", optionNames);
    }

    [Fact]
    public async Task Model_Flag_ParsesCorrectly()
    {
        string? parsedModel = null;
        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.SetAction((ParseResult pr) =>
        {
            parsedModel = pr.GetValue(GlobalOptions.Model);
            return 0;
        });

        await new CommandLineConfiguration(root).InvokeAsync(["--model", "gpt-5"]);

        Assert.Equal("gpt-5", parsedModel);
    }

    [Fact]
    public async Task Unattended_Flag_ParsesCorrectly()
    {
        bool parsedUnattended = false;
        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.SetAction((ParseResult pr) =>
        {
            parsedUnattended = pr.GetValue(GlobalOptions.Unattended);
            return 0;
        });

        await new CommandLineConfiguration(root).InvokeAsync(["--unattended"]);

        Assert.True(parsedUnattended);
    }

    [Fact]
    public async Task MaxIterations_Flag_ParsesCorrectly()
    {
        int? parsedMax = null;
        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.SetAction((ParseResult pr) =>
        {
            parsedMax = pr.GetValue(GlobalOptions.MaxIterations);
            return 0;
        });

        await new CommandLineConfiguration(root).InvokeAsync(["--max-iterations", "42"]);

        Assert.Equal(42, parsedMax);
    }
}
