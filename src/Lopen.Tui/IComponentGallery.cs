namespace Lopen.Tui;

/// <summary>
/// Registry for TUI components. Supports the 'lopen test tui' component gallery
/// where each component self-registers and can be previewed with mock data.
/// </summary>
public interface IComponentGallery
{
    /// <summary>
    /// Registers a component in the gallery.
    /// </summary>
    void Register(ITuiComponent component);

    /// <summary>
    /// Returns all registered components.
    /// </summary>
    IReadOnlyList<ITuiComponent> GetAll();

    /// <summary>
    /// Gets a component by name, or null if not found.
    /// </summary>
    ITuiComponent? GetByName(string name);
}
