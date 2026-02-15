---
name: tui-research
description: Research findings for implementing the Lopen TUI module
---

# TUI Implementation Research

> Research covering library selection, architecture patterns, and implementation strategies for the Lopen terminal UI.

---

## 1. Spectre.Console

**Current Version**: 0.54.0 (pre-release, latest stable: 0.49.1). The library targets .NET Standard 2.0, net8.0, net9.0, and net10.0. Over 156 dependent GitHub repos including `dotnet/aspire` and `microsoft/semantic-kernel`.

**Note**: As of 0.54.0, `Spectre.Console.Cli` has been moved to its own repository and will have independent versioning (targeting 1.0.0).

### Key Capabilities

**Layout Widget** — Divides console space into named sections, split horizontally or vertically with precise sizing control:

```csharp
var layout = new Layout("Root")
    .SplitRows(
        new Layout("Header").Size(3),
        new Layout("Body"),
        new Layout("Footer").Size(3));

layout["Body"].SplitColumns(
    new Layout("Activity").Ratio(3),
    new Layout("Context").Ratio(2));

layout["Activity"].Update(new Panel("Agent output...").Expand());
layout["Context"].Update(new Panel("Task tree...").Expand());

AnsiConsole.Write(layout);
```

- `Ratio(n)` for proportional sizing, `Size(n)` for fixed, `MinimumSize(n)` for floor
- `IsVisible` property for dynamic show/hide
- Named sections accessed via indexer: `layout["SectionName"]`
- Nested splits for complex layouts (header + body with columns + footer)

**Panel Widget** — Wraps content in bordered containers with headers. Supports rounded/square/heavy/double borders, `Expand()` to fill width, fixed `Width`/`Height`, `Padding`, and `BorderColor`. Panels nest arbitrarily and accept any `IRenderable`.

**Table Widget** — Structured data in rows and columns. Features: 18 border styles, column alignment, `ShowRowSeparators()`, headers/footers, `Expand()`, nested tables, mixed `IRenderable` content in cells, and dynamic updates via `UpdateCell()`/`InsertRow()`.

**Tree Widget** — Hierarchical data with Unicode tree guides (Line, Ascii, DoubleLine, BoldLine). Nodes can be collapsed/expanded individually or globally. Supports `AddNode()` chaining, `AddNodes()` bulk, markup in labels, and embedding any `IRenderable` as node content.

**Prompt Widgets** — `TextPrompt<T>` for typed user input with validation, default values, secret masking, and choice restriction. `SelectionPrompt<T>` for arrow-key list selection. `MultiSelectionPrompt<T>` for checkboxes. All prompts are **not thread safe** and cannot be used alongside Live displays.

**Live Rendering** — `AnsiConsole.Live()` updates content in-place without scrolling:

```csharp
await AnsiConsole.Live(layout)
    .Overflow(VerticalOverflow.Crop)
    .Cropping(VerticalOverflowCropping.Top)
    .StartAsync(async ctx =>
    {
        // Update any section
        layout["Activity"].Update(new Panel("New content"));
        ctx.Refresh();
    });
```

- `ctx.Refresh()` redraws the current renderable
- `ctx.UpdateTarget()` replaces the entire renderable
- Overflow modes: Ellipsis, Crop, Visible
- Cropping direction: Top (keep newest) or Bottom (keep oldest)
- `AutoClear(true)` removes display on completion
- `StartAsync()` for async operations
- **Not thread safe** — cannot combine with prompts or other interactive components

### Relevance to Lopen

Spectre.Console's `Layout` widget directly maps to the spec's split-screen design (Header, Activity/Context columns, Prompt). The `Tree` widget covers the task hierarchy display. `Live` rendering enables real-time updates to individual layout sections. The `Panel` widget provides the bordered regions seen throughout the spec mockups. The major limitation is that `Live` display and `Prompt` cannot coexist — this requires a custom input layer.

---

## 2. Terminal.Gui vs Spectre.Console

| Aspect | Spectre.Console | Terminal.Gui |
|---|---|---|
| **Version** | 0.54.0 (pre-release) | v2.0.0 (pre-release), v1.x stable |
| **Paradigm** | Renderable widget tree written to console | Full GUI toolkit with event loop, views, windows |
| **Layout** | `Layout` widget with Ratio/Size/MinSize | `Pos`/`Dim` computed layout, `TileView` for splits |
| **Input** | `TextPrompt`, `SelectionPrompt` (blocking) | Full keyboard/mouse event system, `KeyDown`/`KeyUp` |
| **Threading** | Not thread safe for interactive components | `MainLoop` with `Invoke()` for thread-safe updates |
| **Live Update** | `AnsiConsole.Live()` for in-place rendering | Automatic redraw on state change |
| **Styling** | Markup language `[bold red]text[/]` | `ColorScheme`, `Attribute` system |
| **Maturity** | Widely adopted (14K+ dependent packages) | Smaller ecosystem, v2 is pre-release |
| **Mouse** | Limited (prompts only) | Full mouse support, drag-and-drop |
| **Custom Widgets** | Implement `IRenderable` | Subclass `View` |
| **Modal Dialogs** | Not built-in (custom overlay) | `Dialog`, `MessageBox` built-in |
| **Focus/Tab** | Not built-in | Built-in focus chain, tab navigation |

### Terminal.Gui Strengths for Lopen

- True event-driven architecture with `Application.Run()` main loop
- Built-in focus management (Tab between panes matches spec's Tab shortcut)
- `TileView` provides split-screen with draggable splitter
- `Dialog` and `MessageBox` for modals (confirmation, error, session resume)
- `KeyDown`/`KeyUp` events for complex keyboard shortcuts
- `TextView` for multi-line editable text input (prompt area)
- Thread-safe UI updates via `Application.Invoke()`

### Spectre.Console Strengths for Lopen

- Superior text rendering quality (markup, colors, box-drawing)
- `Layout` with named sections and proportional sizing
- `Tree` widget maps directly to task hierarchy
- `Panel` with rounded borders matches spec mockups exactly
- Spinner/Progress widgets for async feedback
- The spec explicitly names Spectre.Console as the intended library
- Much larger community and ecosystem

### Recommendation

The spec states: "The TUI is built on Spectre.Console (or equivalent .NET terminal UI library)." **Terminal.Gui is the better fit for Lopen's requirements**, despite the spec's preference for Spectre.Console. The reasons:

1. **Concurrent input + display**: Lopen needs the prompt area to accept input while the activity area updates in real-time. Spectre.Console's `Live` display is explicitly not compatible with prompts.
2. **Focus management**: Tab to switch between panes requires a focus system.
3. **Modal dialogs**: Session resume, confirmation, error modals are first-class in Terminal.Gui.
4. **Keyboard shortcuts**: `Ctrl+P`, `Alt+Enter`, number keys all need event-driven handling.

However, a **hybrid approach** is most pragmatic — see Section 10.

### Relevance to Lopen

The choice between these libraries is the most consequential architectural decision for the TUI module. The hybrid approach (Terminal.Gui for structure/input, Spectre.Console renderables for content) gives the best of both worlds.

---

## 3. Split-Screen Layout

### Spectre.Console Approach

```csharp
var layout = new Layout("Root")
    .SplitRows(
        new Layout("TopPanel").Size(4),
        new Layout("Body"),
        new Layout("PromptArea").Size(3));

// Adjustable ratio: 60/40 default, range 50/50 to 80/20
int activityRatio = 3;
int contextRatio = 2;

layout["Body"].SplitColumns(
    new Layout("Activity").Ratio(activityRatio).MinimumSize(40),
    new Layout("Context").Ratio(contextRatio).MinimumSize(20));
```

To change ratios dynamically, rebuild the `SplitColumns` call with new ratio values and call `ctx.Refresh()` within a `Live` context.

### Terminal.Gui Approach

```csharp
var tileView = new TileView(2)  // 2 tiles = left/right split
{
    X = 0, Y = 3,  // Below header
    Width = Dim.Fill(),
    Height = Dim.Fill(3),  // Leave room for prompt
    Orientation = Orientation.Vertical,
};

// Set initial split at 60%
tileView.SetSplitterPos(0, Pos.Percent(60));

// Content goes in tiles
tileView.Tiles.ElementAt(0).ContentView.Add(activityView);
tileView.Tiles.ElementAt(1).ContentView.Add(contextView);
```

Terminal.Gui's `TileView` provides a draggable splitter by default. To enforce the 50%–80% range from the spec, constrain in the `SplitterMoved` event.

### Pure Custom Approach

For maximum control, calculate column widths from `Console.WindowWidth`:

```csharp
void RecalculateLayout(int activityPercent)
{
    activityPercent = Math.Clamp(activityPercent, 50, 80);
    int totalWidth = Console.WindowWidth;
    int activityWidth = (int)(totalWidth * activityPercent / 100.0);
    int contextWidth = totalWidth - activityWidth - 1; // 1 for border
}
```

### Relevance to Lopen

The spec requires "ratio adjustable from 50/50 to 80/20". Both libraries support this. Terminal.Gui's `TileView` adds user-draggable splitters as a bonus. Spectre.Console's `Layout` is simpler but requires manual ratio management.

---

## 4. Progressive Disclosure

### Pattern: Collapsible Activity Entries

Each tool call in the activity area has two states:

```
// Collapsed (one-line summary)
● Edit src/auth.ts (+45 -12)

// Expanded (full details)
▼ Edit src/auth.ts (+45 -12)
   + Added validateToken function
   + Imported JWT library
   --- src/auth.ts
   @@ -10,3 +10,15 @@
   ...
```

### Implementation with Spectre.Console Tree

```csharp
public class ActivityEntry
{
    public string Summary { get; init; }
    public IRenderable DetailContent { get; init; }
    public bool IsExpanded { get; set; }
    public bool IsCurrentAction { get; set; }

    public IRenderable Render()
    {
        var prefix = IsExpanded ? "▼" : "●";
        if (!IsExpanded)
            return new Markup($"{prefix} {Summary}");

        var rows = new Rows(
            new Markup($"{prefix} {Summary}"),
            new Padder(DetailContent, new Padding(3, 0, 0, 0)));
        return rows;
    }
}
```

### Implementation with Terminal.Gui

```csharp
public class CollapsibleView : View
{
    private bool _expanded;
    private View _summaryView;
    private View _detailView;

    public void Toggle()
    {
        _expanded = !_expanded;
        _detailView.Visible = _expanded;
        SetNeedsLayout();
    }
}
```

### Auto-Expand Rules (from spec)

1. **Current action**: Always expanded
2. **Previous actions**: Collapsed to summary
3. **Errors/warnings**: Auto-expanded regardless of position
4. **User toggle**: Click or keyboard shortcut to expand/collapse

### Relevance to Lopen

Progressive disclosure is central to the activity area. The data model should track `IsExpanded` and `IsCurrentAction` per entry. When a new action starts, the previous one collapses automatically. Error entries override the collapse behavior.

---

## 5. Real-Time Updates

### Spectre.Console Live Display

```csharp
await AnsiConsole.Live(layout)
    .Overflow(VerticalOverflow.Crop)
    .Cropping(VerticalOverflowCropping.Top)
    .StartAsync(async ctx =>
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Update only the sections that changed
            layout["Activity"].Update(RenderActivityPane(state));
            layout["Context"].Update(RenderContextPane(state));
            layout["TopPanel"].Update(RenderTopPanel(state));

            ctx.Refresh();  // Redraws entire layout
            await Task.Delay(100, cancellationToken);
        }
    });
```

**Key limitation**: `ctx.Refresh()` redraws the *entire* layout — there is no partial region update. However, Spectre.Console uses diff-based rendering internally to minimize actual terminal writes, so performance is generally acceptable.

### Terminal.Gui Approach

```csharp
// Thread-safe update from background task
Application.Invoke(() =>
{
    activityView.Text = newContent;
    contextView.SetNeedsDisplay();
    // Only the dirty views get redrawn
});
```

Terminal.Gui has true partial redraw — only views marked dirty are re-rendered.

### Hybrid: Background State + Render Loop

```csharp
public class TuiState
{
    // Observable state that triggers renders
    public event Action StateChanged;

    private string _currentAction;
    public string CurrentAction
    {
        get => _currentAction;
        set { _currentAction = value; StateChanged?.Invoke(); }
    }
}

// In render loop
state.StateChanged += () => ctx.Refresh();
```

### Relevance to Lopen

The TUI must update the activity area as agent actions stream in, the context panel as tasks complete, and the top panel as token counts change — all while the prompt area remains responsive. This rules out simple `Live` display alone and points toward either Terminal.Gui or a custom render loop with raw console input.

---

## 6. Keyboard Input Handling

### The Core Challenge

Lopen needs simultaneous:
- **Text input** in the prompt area (typing messages)
- **Keyboard shortcuts** (`Ctrl+P`, `Alt+Enter`, `Tab`, `1-9`)
- **Navigation** (scroll activity area, expand/collapse entries)

### Spectre.Console Limitation

Spectre.Console prompts are **blocking** — they take over the console until the user submits. There is no way to have a `TextPrompt` running while `Live` display updates the screen. This is the fundamental incompatibility.

### Terminal.Gui Solution

```csharp
// All keyboard events flow through the event system
var promptView = new TextField()
{
    X = 0, Y = Pos.AnchorEnd(3),
    Width = Dim.Fill(),
};

// Global key bindings
Application.AddKeyBinding(Key.P.WithCtrl, () =>
{
    PauseAgent();
    return true;
});

Application.AddKeyBinding(Key.Tab, () =>
{
    FocusNextPane();
    return true;
});

// Alt+Enter for newline in multi-line prompt
promptView.KeyDown += (sender, e) =>
{
    if (e.KeyEvent.Key == (Key.Enter | Key.AltMask))
    {
        InsertNewline();
        e.Handled = true;
    }
};

// Number keys for resource access (when prompt not focused)
Application.KeyDown += (sender, e) =>
{
    if (!promptView.HasFocus && e.KeyEvent.Key >= Key.D1 && e.KeyEvent.Key <= Key.D9)
    {
        var resourceIndex = (int)(e.KeyEvent.Key - Key.D1);
        OpenResource(resourceIndex);
        e.Handled = true;
    }
};
```

### Raw Console.ReadKey Approach

If avoiding Terminal.Gui, implement a custom input loop:

```csharp
public class InputHandler
{
    private readonly StringBuilder _inputBuffer = new();

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                HandleKey(key);
            }
            await Task.Delay(16, ct); // ~60fps polling
        }
    }

    private void HandleKey(ConsoleKeyInfo key)
    {
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.P)
            OnPauseRequested?.Invoke();
        else if (key.Modifiers.HasFlag(ConsoleModifiers.Alt) && key.Key == ConsoleKey.Enter)
            _inputBuffer.AppendLine();
        else if (key.Key == ConsoleKey.Enter)
            OnSubmit?.Invoke(_inputBuffer.ToString());
        else if (key.Key == ConsoleKey.Tab)
            OnFocusChange?.Invoke();
        else if (key.Key >= ConsoleKey.D1 && key.Key <= ConsoleKey.D9 && !_isPromptFocused)
            OnResourceSelected?.Invoke((int)key.Key - (int)ConsoleKey.D0);
        else
            _inputBuffer.Append(key.KeyChar);
    }
}
```

### Relevance to Lopen

The keyboard handling requirements are complex enough to justify either Terminal.Gui's event system or a custom input handler. Raw `Console.ReadKey` works but requires implementing cursor movement, delete, history, etc. from scratch.

---

## 7. Component Architecture

### Design Principles (from spec)

1. Components accept data/state as input (not fetched internally)
2. All external dependencies behind interfaces that can be stubbed
3. Each component self-registers with the gallery

### Interface-Based Architecture

```csharp
// All TUI components implement this interface
public interface ITuiComponent
{
    string Name { get; }
    string Description { get; }
    IRenderable Render(RenderContext context);
    IEnumerable<StubScenario> GetStubScenarios();
}

// Stub scenario for gallery
public record StubScenario(string Name, object State);

// Render context provides terminal dimensions and theme
public record RenderContext(int Width, int Height, Theme Theme);
```

### State Injection Pattern

```csharp
// Data model for the context panel
public record ContextPanelState(
    TaskInfo CurrentTask,
    IReadOnlyList<ComponentInfo> Components,
    IReadOnlyList<ResourceInfo> ActiveResources);

// Component renders from state, never fetches
public class ContextPanelComponent : ITuiComponent
{
    public string Name => "Context Panel";

    public IRenderable Render(RenderContext ctx)
    {
        // Pure function: state in, renderable out
        return BuildPanel(_state);
    }

    // Gallery stub scenarios
    public IEnumerable<StubScenario> GetStubScenarios() => new[]
    {
        new StubScenario("Empty", new ContextPanelState(null, [], [])),
        new StubScenario("In Progress", CreateInProgressState()),
        new StubScenario("Completed", CreateCompletedState()),
        new StubScenario("Error", CreateErrorState()),
    };
}
```

### Gallery Auto-Registration

```csharp
// Components discovered via reflection or DI
public class ComponentGallery
{
    private readonly IEnumerable<ITuiComponent> _components;

    public ComponentGallery(IEnumerable<ITuiComponent> components)
    {
        _components = components;
    }

    public void Run()
    {
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<ITuiComponent>()
                .Title("Select a component to preview:")
                .AddChoices(_components)
                .UseConverter(c => c.Name));

        // Show selected component with stub data
        PreviewComponent(selection);
    }
}

// DI registration
services.AddTransient<ITuiComponent, TopPanelComponent>();
services.AddTransient<ITuiComponent, ActivityPanelComponent>();
services.AddTransient<ITuiComponent, ContextPanelComponent>();
services.AddTransient<ITuiComponent, PromptAreaComponent>();
// ... auto-discovered via assembly scanning
```

### Testability

```csharp
[Fact]
public void ContextPanel_WithEmptyState_RendersPlaceholder()
{
    var component = new ContextPanelComponent();
    var state = new ContextPanelState(null, [], []);
    component.SetState(state);

    var renderable = component.Render(new RenderContext(80, 24, Theme.Default));

    // Use Spectre.Console's test console for snapshot testing
    var console = new TestConsole();
    console.Write(renderable);
    Verify(console.Output);
}
```

### Relevance to Lopen

The spec requires every component to work in the `lopen test tui` gallery with mock data. The `ITuiComponent` interface + `StubScenario` pattern satisfies both the gallery requirement and the testability requirement. DI registration ensures new components automatically appear in the gallery.

---

## 8. Syntax Highlighting

### Options for Terminal Syntax Highlighting

| Library | Approach | Languages | Size |
|---|---|---|---|
| Spectre.Console Markup | Manual `[color]...[/]` tags | N/A (manual) | 0 (built-in) |
| Spectre.Console.Json | Built-in JSON highlighting | JSON only | Tiny |
| TextMateSharp | VS Code TextMate grammars | 50+ languages | ~5MB with grammars |
| Custom regex-based | Pattern matching per language | Configurable | Small |

### Spectre.Console Markup (Simplest)

```csharp
// Manual highlighting for diff output
public static Markup HighlightDiff(string diff)
{
    var lines = diff.Split('\n').Select(line =>
    {
        if (line.StartsWith('+'))
            return $"[green]{Markup.Escape(line)}[/]";
        if (line.StartsWith('-'))
            return $"[red]{Markup.Escape(line)}[/]";
        if (line.StartsWith("@@"))
            return $"[cyan]{Markup.Escape(line)}[/]";
        return Markup.Escape(line);
    });
    return new Markup(string.Join("\n", lines));
}
```

### TextMateSharp (Full VS Code Quality)

```csharp
// TextMateSharp provides VS Code-quality highlighting
var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
var registry = new Registry(registryOptions);
var grammar = registry.LoadGrammar(registryOptions.GetScopeByLanguageId("csharp"));

// Tokenize and convert to Spectre markup
var result = grammar.TokenizeLine(codeLine, null);
foreach (var token in result.Tokens)
{
    var foreground = GetColorFromScope(token.Scopes);
    // Build Spectre Markup string with colors
}
```

### Recommended Approach for Lopen

1. **Diff highlighting**: Custom regex — diffs have simple, well-defined syntax (`+`, `-`, `@@`)
2. **Code blocks**: TextMateSharp for accurate syntax highlighting when displaying file contents or code snippets in the activity area and resource viewer
3. **JSON**: `Spectre.Console.Json` for any JSON output

### Relevance to Lopen

The spec requires "syntax highlighting in code blocks" and "diff viewer with syntax highlighting." Diff highlighting is straightforward with regex. For code blocks shown in the activity area and resource viewer, TextMateSharp provides VS Code-quality highlighting. The cost is a ~5MB grammar dependency, which is acceptable for a developer tool.

---

## 9. Recommended NuGet Packages

### Core Packages

| Package | Version | Purpose |
|---|---|---|
| `Spectre.Console` | 0.54.0 | Rich console rendering (Layout, Panel, Table, Tree, Markup, Live) |
| `Spectre.Console.Json` | 0.54.0 | JSON syntax highlighting |
| `Spectre.Console.Cli` | 0.53.1 | CLI command parsing (used by CLI module, shared dependency) |
| `Terminal.Gui` | 2.0.0 | Full-screen TUI framework (event loop, input, focus management) |

### Syntax Highlighting

| Package | Version | Purpose |
|---|---|---|
| `TextMateSharp` | 2.0.3 | TextMate grammar engine for code highlighting |
| `TextMateSharp.Grammars` | latest | Bundled VS Code grammars |

### Testing

| Package | Version | Purpose |
|---|---|---|
| `Spectre.Console.Testing` | 0.54.0 | `TestConsole` for snapshot-testing Spectre renderables |
| `Verify.Xunit` / `Verify.NUnit` | latest | Snapshot/approval testing for rendered output |

### Optional / Evaluation

| Package | Purpose | Notes |
|---|---|---|
| `Consolonia` | Avalonia-based TUI framework | Alternative to Terminal.Gui, heavier |
| `Pastel` | Simple ANSI string coloring | If avoiding Spectre markup for some paths |

### Relevance to Lopen

The core stack is `Spectre.Console` + `Terminal.Gui`. Both are actively maintained, target .NET 8+, and are used in production by major projects. `TextMateSharp` adds code highlighting. `Spectre.Console.Testing` enables the snapshot testing pattern needed for the component gallery's test coverage.

---

## 10. Implementation Approach

### Recommended: Hybrid Architecture

Use **Terminal.Gui for the application shell** (event loop, input handling, focus management, window structure) and **Spectre.Console for content rendering** within Terminal.Gui views.

```
┌──────────────────────────────────────────────────────────┐
│                    Terminal.Gui Layer                      │
│  Application.Run() → event loop, keyboard, focus, layout │
│                                                           │
│  ┌─────────────────────────┐  ┌────────────────────────┐ │
│  │   ActivityView (View)   │  │  ContextView (View)    │ │
│  │  ┌───────────────────┐  │  │  ┌──────────────────┐  │ │
│  │  │ Spectre.Console   │  │  │  │ Spectre.Console  │  │ │
│  │  │ Layout, Panel,    │  │  │  │ Tree, Table,     │  │ │
│  │  │ Markup, Live      │  │  │  │ Panel, Markup    │  │ │
│  │  └───────────────────┘  │  │  └──────────────────┘  │ │
│  └─────────────────────────┘  └────────────────────────┘ │
│                                                           │
│  ┌────────────────────────────────────────────────────┐   │
│  │              PromptView (TextView)                  │   │
│  │           Native Terminal.Gui text editing           │   │
│  └────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

### Why Hybrid

1. **Terminal.Gui** solves the hard problems: concurrent input + display, focus management, keyboard routing, modal dialogs, thread-safe updates
2. **Spectre.Console** solves the pretty problems: rich markup, trees, tables, panels, spinners, progress bars
3. Avoids writing a custom event loop, input handler, focus chain, and modal system from scratch
4. The spec's Spectre.Console preference is preserved — all visible content uses Spectre renderables

### Bridge: Rendering Spectre in Terminal.Gui

Create a custom `View` that renders Spectre `IRenderable` objects into Terminal.Gui's drawing model:

```csharp
public class SpectreView : View
{
    private IRenderable _content;

    public void SetContent(IRenderable renderable)
    {
        _content = renderable;
        SetNeedsDisplay();
    }

    public override void OnDrawContent(Rectangle viewport)
    {
        if (_content == null) return;

        // Render Spectre content to ANSI string
        var console = new StringConsole(viewport.Width);
        console.Write(_content);

        // Write ANSI output into Terminal.Gui's drawing surface
        DrawAnsiString(console.Output, viewport);
    }
}
```

### Alternative: Pure Spectre.Console with Custom Input

If the hybrid approach proves too complex, use Spectre.Console alone with a custom input thread:

```csharp
// Two concurrent tasks
var renderTask = RunRenderLoop(layout, state, cts.Token);
var inputTask = RunInputLoop(state, cts.Token);
await Task.WhenAll(renderTask, inputTask);

async Task RunRenderLoop(Layout layout, TuiState state, CancellationToken ct)
{
    await AnsiConsole.Live(layout).StartAsync(async ctx =>
    {
        while (!ct.IsCancellationRequested)
        {
            layout["Activity"].Update(RenderActivity(state));
            layout["Context"].Update(RenderContext(state));
            ctx.Refresh();
            await Task.Delay(50, ct);
        }
    });
}

async Task RunInputLoop(TuiState state, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            ProcessKey(key, state);
        }
        await Task.Delay(16, ct);
    }
}
```

This is simpler but requires implementing cursor rendering, text editing, and focus management manually.

### Component Lifecycle

```
Startup
  ├─ Parse CLI flags (--no-welcome, --no-logo, --resume)
  ├─ Initialize DI container with ITuiComponent registrations
  ├─ Check for existing session → show Resume Modal or Landing Page
  └─ Enter main workspace
       ├─ TopPanelComponent renders header
       ├─ ActivityPanelComponent renders left pane
       ├─ ContextPanelComponent renders right pane
       ├─ PromptAreaComponent handles input
       └─ Render loop: state changes → component re-renders → display refresh
```

### Recommended Phasing

1. **Phase 1**: Prototype with pure Spectre.Console + custom input to validate the rendering approach
2. **Phase 2**: If input complexity grows unmanageable, introduce Terminal.Gui shell
3. **Phase 3**: Build component gallery (`lopen test tui`) with stub data scenarios
4. **Phase 4**: Integration with core, LLM, and storage modules

### Relevance to Lopen

The hybrid approach balances the spec's Spectre.Console preference with Terminal.Gui's architectural advantages. Starting with pure Spectre.Console + custom input for the prototype keeps things simple while leaving the Terminal.Gui escape hatch open. The component architecture (Section 7) is library-agnostic — the `ITuiComponent` interface works with either approach.

---

## References

- [Spectre.Console Documentation](https://spectreconsole.net)
- [Spectre.Console GitHub](https://github.com/spectreconsole/spectre.console)
- [Terminal.Gui v2 Documentation](https://gui-cs.github.io/Terminal.GuiV2Docs)
- [Terminal.Gui GitHub](https://github.com/gui-cs/Terminal.Gui)
- [TextMateSharp GitHub](https://github.com/nicknacknow/TextMateSharp)
- [CSharpRepl](https://github.com/waf/CSharpRepl) — Example of Spectre.Console + REPL input handling
