namespace Lopen.Tui;

/// <summary>
/// Controls interactive keyboard navigation in the component gallery preview mode.
/// Handles selection scrolling, entering preview, and returning to list view (TUI-45).
/// </summary>
public sealed class GalleryPreviewController
{
    private readonly IComponentGallery _gallery;
    private int _selectedIndex;
    private bool _inPreview;
    private string _currentPreviewState = "populated";

    public GalleryPreviewController(IComponentGallery gallery)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        _gallery = gallery;
    }

    /// <summary>Current selected index in the gallery list.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Whether we are in component preview mode (vs list mode).</summary>
    public bool InPreview => _inPreview;

    /// <summary>The currently active preview state name.</summary>
    public string CurrentPreviewState => _currentPreviewState;

    /// <summary>
    /// Processes a keyboard action and returns true if the gallery state changed.
    /// </summary>
    public bool HandleAction(KeyAction action)
    {
        var components = _gallery.GetAll();
        if (components.Count == 0) return false;

        return _inPreview
            ? HandlePreviewAction(action, components)
            : HandleListAction(action, components);
    }

    /// <summary>
    /// Renders the current gallery view (list or preview) within the given region.
    /// </summary>
    public string[] Render(int width, int height)
    {
        var components = _gallery.GetAll();
        if (components.Count == 0)
            return ["  No components registered"];

        if (_inPreview)
        {
            var component = components[_selectedIndex];
            if (component is IPreviewableComponent previewable)
                return previewable.RenderPreview(_currentPreviewState, width, height);
            return [$"  {component.Name} does not support preview"];
        }

        var listComponent = new GalleryListComponent();
        var data = GalleryListComponent.FromGallery(_gallery, _selectedIndex);
        return listComponent.Render(data, new ScreenRect(0, 0, width, height));
    }

    private bool HandleListAction(KeyAction action, IReadOnlyList<ITuiComponent> components)
    {
        switch (action)
        {
            case KeyAction.ScrollDown:
                _selectedIndex = Math.Min(_selectedIndex + 1, components.Count - 1);
                return true;

            case KeyAction.ScrollUp:
                _selectedIndex = Math.Max(_selectedIndex - 1, 0);
                return true;

            case KeyAction.ToggleExpand:
                _inPreview = true;
                _currentPreviewState = "populated";
                return true;

            default:
                return false;
        }
    }

    private bool HandlePreviewAction(KeyAction action, IReadOnlyList<ITuiComponent> components)
    {
        switch (action)
        {
            case KeyAction.Cancel:
                _inPreview = false;
                return true;

            case KeyAction.ScrollDown:
            case KeyAction.ScrollUp:
                CyclePreviewState(action, components);
                return true;

            default:
                return false;
        }
    }

    private void CyclePreviewState(KeyAction action, IReadOnlyList<ITuiComponent> components)
    {
        var component = components[_selectedIndex];
        if (component is not IPreviewableComponent previewable) return;

        var states = previewable.GetPreviewStates();
        if (states.Count <= 1) return;

        var idx = -1;
        for (var i = 0; i < states.Count; i++)
        {
            if (states[i] == _currentPreviewState)
            {
                idx = i;
                break;
            }
        }
        if (action == KeyAction.ScrollDown)
            idx = (idx + 1) % states.Count;
        else
            idx = (idx - 1 + states.Count) % states.Count;

        _currentPreviewState = states[idx];
    }
}
