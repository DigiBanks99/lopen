namespace Lopen.Tui;

/// <summary>
/// Renders a file picker with tree view.
/// </summary>
public sealed class FilePickerComponent : ITuiComponent
{
    public string Name => "FilePicker";
    public string Description => "File picker with tree view and navigation";

    public string[] Render(FilePickerData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        lines.Add($"📂 {data.RootPath}");

        for (int i = 0; i < data.Nodes.Count; i++)
        {
            var node = data.Nodes[i];
            var indent = new string(' ', node.Depth * 2 + 2);
            var icon = node.IsDirectory ? (node.IsExpanded ? "📂" : "📁") : "📄";
            var selected = i == data.SelectedIndex ? "▶ " : "  ";
            lines.Add($"{selected}{indent}{icon} {node.Name}");
        }

        while (lines.Count < region.Height)
            lines.Add(string.Empty);
        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}

/// <summary>
/// Renders a module/component selection modal with arrow key navigation.
/// </summary>
public sealed class SelectionModalComponent : ITuiComponent
{
    public string Name => "SelectionModal";
    public string Description => "Module/component selection modal with arrow key navigation";

    public string[] Render(ModuleSelectionData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        lines.Add($" {data.Title}");
        lines.Add(string.Empty);

        for (int i = 0; i < data.Options.Count; i++)
        {
            var marker = i == data.SelectedIndex ? " ▶ " : "   ";
            lines.Add($"{marker}{data.Options[i]}");
        }

        while (lines.Count < region.Height)
            lines.Add(string.Empty);
        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}

/// <summary>
/// Renders confirmation modals (Yes/No/Always/Other).
/// </summary>
public sealed class ConfirmationModalComponent : ITuiComponent
{
    public string Name => "ConfirmationModal";
    public string Description => "Confirmation modal with Yes/No/Always options";

    public string[] Render(ConfirmationData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        lines.Add($" {data.Title}");
        if (!string.IsNullOrEmpty(data.Message))
        {
            lines.Add(string.Empty);
            lines.Add($"  {data.Message}");
        }
        lines.Add(string.Empty);

        var optionParts = new List<string>();
        for (int i = 0; i < data.Options.Count; i++)
        {
            optionParts.Add(i == data.SelectedIndex
                ? $"[>{data.Options[i]}<]"
                : $"[{data.Options[i]}]");
        }
        lines.Add($"  {string.Join("  ", optionParts)}");

        while (lines.Count < region.Height)
            lines.Add(string.Empty);
        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}

/// <summary>
/// Renders error/failure modals with recovery options.
/// </summary>
public sealed class ErrorModalComponent : ITuiComponent
{
    public string Name => "ErrorModal";
    public string Description => "Error modal with recovery options (Retry/Skip/Abort)";

    public string[] Render(ErrorModalData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        lines.Add($" ⚠ {data.Title}");
        lines.Add(string.Empty);
        lines.Add($"  {data.Message}");
        lines.Add(string.Empty);

        var optionParts = new List<string>();
        for (int i = 0; i < data.RecoveryOptions.Count; i++)
        {
            optionParts.Add(i == data.SelectedIndex
                ? $"[>{data.RecoveryOptions[i]}<]"
                : $"[{data.RecoveryOptions[i]}]");
        }
        lines.Add($"  {string.Join("  ", optionParts)}");

        while (lines.Count < region.Height)
            lines.Add(string.Empty);
        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}
