using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component showing a mock session list table.
/// </summary>
public sealed class SessionListGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Session List";

    public void Render(IAnsiConsole console)
    {
        console.MarkupLine(LopenTheme.Bold("Session List", LopenTheme.Accent));
        console.WriteLine();

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("ID", LopenTheme.Accent)))
            .AddColumn(new TableColumn(LopenTheme.Bold("Module", LopenTheme.Accent)))
            .AddColumn(new TableColumn(LopenTheme.Bold("Step", LopenTheme.Accent)))
            .AddColumn(new TableColumn(LopenTheme.Bold("Updated", LopenTheme.Accent)).RightAligned());

        table.AddRow("a1b2c3d4", "auth", "Build", "2 minutes ago");
        table.AddRow("e5f6g7h8", "tui", "Plan", "15 minutes ago");
        table.AddRow("i9j0k1l2", "storage", "Verify", "1 hour ago");
        table.AddRow("m3n4o5p6", "llm", "Build", "3 hours ago");
        table.AddRow("q7r8s9t0", "core", "Complete", "1 day ago");

        console.Write(table);
    }
}
