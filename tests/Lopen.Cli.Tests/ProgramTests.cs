using System.CommandLine;

namespace Lopen.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void RootCommand_CanBeCreated()
    {
        var rootCommand = new RootCommand("test");

        Assert.Equal("test", rootCommand.Description);
    }
}
