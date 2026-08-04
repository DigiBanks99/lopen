using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component showing the error panel rendering.
/// </summary>
public sealed class ErrorPanelGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Error Panel";

    public void Render(IAnsiConsole console)
    {
        console.MarkupLine(LopenTheme.Bold("Error Panel Variants", LopenTheme.Accent));
        console.WriteLine();

        // Standard error
        Panel errorPanel = new Panel(Markup.Escape("Failed to connect to LLM service. Check your authentication status with 'lopen auth status'."))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(LopenTheme.Error),
            Header = new PanelHeader(LopenTheme.Bold("Error", LopenTheme.Error)),
        };
        console.Write(errorPanel);

        console.WriteLine();

        // Error with exception detail
        Panel detailedError = new Panel(Markup.Escape("Token budget exceeded for module 'tui'. Current usage: 105k / 100k tokens."))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(LopenTheme.Error),
            Header = new PanelHeader(LopenTheme.Bold("Error", LopenTheme.Error)),
        };
        console.Write(detailedError);
        console.MarkupLine(LopenTheme.Dim("  BudgetExceededException: Module token budget exceeded", LopenTheme.Muted));

        console.WriteLine();

        // Warning
        Panel warningPanel = new Panel(Markup.Escape("Token budget is at 85% for module 'tui'. Consider wrapping up current tasks."))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(LopenTheme.Warning),
            Header = new PanelHeader(LopenTheme.Bold("Warning", LopenTheme.Warning)),
        };
        console.Write(warningPanel);
    }
}
