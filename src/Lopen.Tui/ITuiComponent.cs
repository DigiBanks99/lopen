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

/// <summary>
/// Optional interface for components that can render a preview with stub data.
/// Used by the component gallery.
/// </summary>
public interface IPreviewableComponent : ITuiComponent
{
    /// <summary>
    /// Renders a preview of the component with realistic stub data (populated state).
    /// </summary>
    string[] RenderPreview(int width, int height);

    /// <summary>
    /// Returns the available preview state names (e.g., "empty", "populated", "error", "loading").
    /// </summary>
    IReadOnlyList<string> GetPreviewStates() => ["populated"];

    /// <summary>
    /// Renders a preview for a specific visual state.
    /// </summary>
    string[] RenderPreview(string state, int width, int height) => RenderPreview(width, height);
}
