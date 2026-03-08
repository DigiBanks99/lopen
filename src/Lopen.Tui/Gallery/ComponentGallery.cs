using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Interactive gallery for visual testing of TUI components in isolation.
/// Renders a selection list; the user picks a component, views it, then returns.
/// </summary>
public class ComponentGallery
{
    private readonly IAnsiConsole _console;
    private readonly IReadOnlyList<IGalleryComponent> _components;

    public ComponentGallery(IAnsiConsole console, IEnumerable<IGalleryComponent> components)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _components = (components ?? throw new ArgumentNullException(nameof(components))).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the registered component names for testing.
    /// </summary>
    public IReadOnlyList<string> ComponentNames => _components.Select(c => c.DisplayName).ToList().AsReadOnly();

    /// <summary>
    /// Runs the interactive gallery loop.
    /// Returns when the user exits (selects the exit option or Ctrl+C).
    /// </summary>
    public void Run()
    {
        if (_components.Count == 0)
        {
            _console.MarkupLine(LopenTheme.Styled("No gallery components registered.", LopenTheme.Warning));
            return;
        }

        while (true)
        {
            string? selected = ShowSelectionList();
            if (selected is null)
                break;

            IGalleryComponent? component = _components.FirstOrDefault(c => c.DisplayName == selected);
            if (component is not null)
            {
                RenderComponent(component);
                WaitForReturn();
            }
        }
    }

    /// <summary>
    /// Shows the component selection list. Returns display name or null to exit.
    /// Virtual for testability.
    /// </summary>
    internal virtual string? ShowSelectionList()
    {
        const string exitOption = "(Exit gallery)";

        List<string> choices = _components.Select(c => c.DisplayName).ToList();
        choices.Add(exitOption);

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title(LopenTheme.Bold("Component Gallery", LopenTheme.Primary))
            .PageSize(15)
            .HighlightStyle(LopenTheme.AccentStyle)
            .AddChoices(choices);

        string selected;
        try
        {
            selected = _console.Prompt(prompt);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return selected == exitOption ? null : selected;
    }

    private void RenderComponent(IGalleryComponent component)
    {
        _console.WriteLine();
        _console.Write(new Rule(LopenTheme.Bold(component.DisplayName, LopenTheme.Accent))
            .RuleStyle(new Style(LopenTheme.Muted)));
        _console.WriteLine();

        component.Render(_console);

        _console.WriteLine();
        _console.Write(new Rule().RuleStyle(new Style(LopenTheme.Muted)));
    }

    /// <summary>
    /// Waits for the user to press a key to return to the selection list.
    /// Virtual for testability.
    /// </summary>
    internal virtual void WaitForReturn()
    {
        _console.MarkupLine(LopenTheme.Dim("Press any key to return to gallery...", LopenTheme.Muted));
        if (_console.Input.IsKeyAvailable())
            _console.Input.ReadKey(intercept: true);
        else
            _console.Input.ReadKey(intercept: true);
    }
}
