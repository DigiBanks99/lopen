using Lopen.Commands;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Lopen.Cli.Tests.Commands;

public class TuiCommandTests
{
    [Fact]
    public void Create_ReturnsTuiCommand()
    {
        ServiceCollection services = new();
        services.AddLopenTui();
        ServiceProvider provider = services.BuildServiceProvider();

        Command command = TuiCommand.Create(provider);

        Assert.Equal("tui", command.Name);
    }

    [Fact]
    public void Create_HasGallerySubcommand()
    {
        ServiceCollection services = new();
        services.AddLopenTui();
        ServiceProvider provider = services.BuildServiceProvider();

        Command command = TuiCommand.Create(provider);

        Assert.Contains(command.Subcommands, c => c.Name == "gallery");
    }
}
