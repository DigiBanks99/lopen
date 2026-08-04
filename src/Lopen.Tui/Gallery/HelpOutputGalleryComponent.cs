using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component showing slash command help output.
/// </summary>
public sealed class HelpOutputGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Slash Command Help";

    public void Render(IAnsiConsole console)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("Command", LopenTheme.Accent)).PadRight(2))
            .AddColumn(new TableColumn(LopenTheme.Styled("Description", LopenTheme.Muted)));

        table.AddRow(LopenTheme.Styled("/help", LopenTheme.Accent), "Show available commands");
        table.AddRow(LopenTheme.Styled("/model", LopenTheme.Accent), "Show or switch the current model");
        table.AddRow(LopenTheme.Styled("/skills", LopenTheme.Accent), "List available skills");
        table.AddRow(LopenTheme.Styled("/sessions", LopenTheme.Accent), "Browse previous sessions");
        table.AddRow(LopenTheme.Styled("/resume", LopenTheme.Accent), "Resume last incomplete session");
        table.AddRow(LopenTheme.Styled("/clear", LopenTheme.Accent), "Clear the screen");
        table.AddRow(LopenTheme.Styled("/exit", LopenTheme.Accent), "Exit lopen");

        console.Write(table);
    }
}
