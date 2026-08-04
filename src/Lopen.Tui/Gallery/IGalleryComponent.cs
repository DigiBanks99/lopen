using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Represents a TUI component that can be rendered in the component gallery.
/// Each component provides a display name and a render method that uses mock data.
/// </summary>
public interface IGalleryComponent
{
    /// <summary>Display name shown in the gallery selection list.</summary>
    string DisplayName { get; }

    /// <summary>Renders the component with mock data to the given console.</summary>
    void Render(IAnsiConsole console);
}
