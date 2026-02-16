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
}
