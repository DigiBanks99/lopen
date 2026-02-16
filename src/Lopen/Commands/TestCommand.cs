using System.CommandLine;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

/// <summary>
/// Defines the 'test' command group with the 'tui' subcommand for component gallery.
/// </summary>
public static class TestCommand
{
    public static Command Create(IServiceProvider services, TextWriter? output = null)
    {
        var stdout = output ?? Console.Out;

        var test = new Command("test", "Development and testing utilities");

        test.Add(CreateTuiCommand(services, stdout));

        return test;
    }

    private static Command CreateTuiCommand(IServiceProvider services, TextWriter stdout)
    {
        var tui = new Command("tui", "Launch the interactive TUI component gallery");
        var listOption = new Option<bool>("--list") { Description = "List components without interactive mode" };
        tui.Add(listOption);

        tui.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var gallery = services.GetService<IComponentGallery>();
            if (gallery is null)
            {
                await stdout.WriteLineAsync("Component gallery not available. Register TUI services first.");
                return ExitCodes.Failure;
            }

            var components = gallery.GetAll();
            if (components.Count == 0)
            {
                await stdout.WriteLineAsync("No components registered in the gallery.");
                return ExitCodes.Failure;
            }

            var listMode = parseResult.GetValue(listOption);

            // Interactive mode when not listing and stdin is available
            if (!listMode && !Console.IsInputRedirected)
            {
                return await RunInteractiveGalleryAsync(gallery, stdout, cancellationToken);
            }

            // Text listing mode
            await stdout.WriteLineAsync("Component Gallery:");
            await stdout.WriteLineAsync(new string('─', 50));
            foreach (var component in components)
            {
                await stdout.WriteLineAsync($"  {component.Name} — {component.Description}");
            }

            return ExitCodes.Success;
        });

        return tui;
    }

    private static async Task<int> RunInteractiveGalleryAsync(
        IComponentGallery gallery,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var galleryList = new GalleryListComponent();
        var selectedIndex = 0;
        var components = gallery.GetAll();
        var width = Math.Max(Console.WindowWidth, 40);
        var height = Math.Max(Console.WindowHeight - 2, 10);

        while (!cancellationToken.IsCancellationRequested)
        {
            // Render the gallery list
            Console.Clear();
            var data = GalleryListComponent.FromGallery(gallery, selectedIndex);
            var lines = galleryList.Render(data, new ScreenRect(0, 0, width, height));
            foreach (var line in lines)
                await stdout.WriteLineAsync(line);

            await stdout.WriteLineAsync("↑/↓ Navigate  Enter: Preview  q: Quit");

            if (!Console.KeyAvailable)
            {
                await Task.Delay(50, cancellationToken);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = Math.Max(0, selectedIndex - 1);
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = Math.Min(components.Count - 1, selectedIndex + 1);
                    break;
                case ConsoleKey.Enter:
                    var selected = components[selectedIndex];
                    if (selected is IPreviewableComponent previewable)
                    {
                        Console.Clear();
                        var preview = previewable.RenderPreview(width, height);
                        foreach (var line in preview)
                            await stdout.WriteLineAsync(line);
                        await stdout.WriteLineAsync("\nPress any key to return...");
                        Console.ReadKey(intercept: true);
                    }
                    else
                    {
                        Console.Clear();
                        await stdout.WriteLineAsync($"Component '{selected.Name}' does not support preview yet.");
                        await stdout.WriteLineAsync("Press any key to return...");
                        Console.ReadKey(intercept: true);
                    }
                    break;
                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    return ExitCodes.Success;
            }
        }

        return ExitCodes.Success;
    }
}
