namespace Lopen.Tui;

/// <summary>
/// Base interface for all renderable TUI components.
/// Components accept data/state as input and produce visual output — they do not fetch data internally.
/// </summary>
public interface ITuiComponent
{
    /// <summary>
    /// Unique name identifying this component.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of the component.
    /// </summary>
    string Description { get; }
}
