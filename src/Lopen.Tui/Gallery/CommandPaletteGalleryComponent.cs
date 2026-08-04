using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component showing the command palette appearance.
/// </summary>
public sealed class CommandPaletteGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Command Palette";

    public void Render(IAnsiConsole console)
    {
        console.MarkupLine(LopenTheme.Bold("Command Palette", LopenTheme.Accent));
        console.WriteLine();

        // Mock command palette display as a table
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("Command", LopenTheme.Accent)).PadRight(2))
            .AddColumn(new TableColumn(LopenTheme.Styled("Description", LopenTheme.Muted)));

        table.AddRow(
            LopenTheme.Styled("/help", LopenTheme.Accent),
            "Show available commands");
        table.AddRow(
            LopenTheme.Styled("/model", LopenTheme.Accent),
            "Show or switch the current model");
        table.AddRow(
            LopenTheme.Styled("/skills", LopenTheme.Accent),
            "List available skills");
        table.AddRow(
            LopenTheme.Styled("/sessions", LopenTheme.Accent),
            "Browse previous sessions");
        table.AddRow(
            LopenTheme.Styled("/resume", LopenTheme.Accent),
            "Resume last incomplete session");
        table.AddRow(
            LopenTheme.Styled("/clear", LopenTheme.Accent),
            "Clear the screen");
        table.AddRow(
            LopenTheme.Styled("/exit", LopenTheme.Accent),
            "Exit lopen");

        console.Write(table);

        console.WriteLine();
        console.MarkupLine(LopenTheme.Dim("Trigger: ? on empty prompt or Ctrl+O | Filter: type to search | Navigate: \u2191/\u2193 | Select: Enter | Dismiss: Escape", LopenTheme.Muted));
    }
}
