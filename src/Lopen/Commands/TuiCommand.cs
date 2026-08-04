using Lopen.Tui.Gallery;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.CommandLine;

namespace Lopen.Commands;

/// <summary>
/// Defines the 'tui' command group with the 'gallery' subcommand.
/// </summary>
public static class TuiCommand
{
    public static Command Create(IServiceProvider services)
    {
        Command tui = new("tui", "TUI tools and diagnostics");

        tui.Add(CreateGalleryCommand(services));

        return tui;
    }

    private static Command CreateGalleryCommand(IServiceProvider services)
    {
        Command gallery = new("gallery", "Launch interactive component gallery");
        gallery.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            IAnsiConsole console = services.GetRequiredService<IAnsiConsole>();
            IEnumerable<IGalleryComponent> components = services.GetServices<IGalleryComponent>();

            ComponentGallery componentGallery = new(console, components);
            componentGallery.Run();
            return 0;
        });
        return gallery;
    }
}
