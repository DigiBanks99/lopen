namespace Lopen.Tui;

/// <summary>
/// Data model for the gallery list view.
/// </summary>
public sealed record GalleryListData
{
    /// <summary>Components available for preview.</summary>
    public required IReadOnlyList<GalleryItem> Items { get; init; }

    /// <summary>Index of the currently selected item.</summary>
    public int SelectedIndex { get; init; }
}

/// <summary>
/// A gallery item representing a registered component.
/// </summary>
public sealed record GalleryItem(string Name, string Description);

/// <summary>
/// Renders a selectable list of gallery components.
/// </summary>
public sealed class GalleryListComponent : ITuiComponent
{
    public string Name => "GalleryList";
    public string Description => "Selectable list of registered TUI components for preview";

    /// <summary>
    /// Renders the gallery list with selection indicator.
    /// </summary>
    public string[] Render(GalleryListData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();

        var title = "  Component Gallery";
        lines.Add(title.Length >= region.Width ? title[..region.Width] : title.PadRight(region.Width));
        lines.Add(new string('─', region.Width));

        for (var i = 0; i < data.Items.Count && lines.Count < region.Height; i++)
        {
            var item = data.Items[i];
            var marker = i == data.SelectedIndex ? " ▶ " : "   ";
            var text = $"{marker}{item.Name} — {item.Description}";
            lines.Add(text.Length >= region.Width ? text[..region.Width] : text.PadRight(region.Width));
        }

        // Pad remaining lines
        while (lines.Count < region.Height)
            lines.Add(new string(' ', region.Width));

        return lines.ToArray();
    }

    /// <summary>
    /// Creates gallery items from a component gallery registry.
    /// </summary>
    public static GalleryListData FromGallery(IComponentGallery gallery, int selectedIndex = 0)
    {
        var items = gallery.GetAll()
            .Select(c => new GalleryItem(c.Name, c.Description))
            .ToList();
        return new GalleryListData { Items = items, SelectedIndex = selectedIndex };
    }
}
