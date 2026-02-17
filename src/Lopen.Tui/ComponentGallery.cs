namespace Lopen.Tui;

/// <summary>
/// In-memory component registry for the TUI component gallery.
/// Components self-register during DI configuration.
/// </summary>
internal sealed class ComponentGallery : IComponentGallery
{
    private readonly List<ITuiComponent> _components = [];

    public void Register(ITuiComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (_components.Any(c => c.Name == component.Name))
            throw new InvalidOperationException($"Component '{component.Name}' is already registered.");

        _components.Add(component);
    }

    public IReadOnlyList<ITuiComponent> GetAll() => _components.AsReadOnly();

    public ITuiComponent? GetByName(string name) =>
        _components.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
