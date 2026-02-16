---
name: tui-research
description: Research findings for implementing the Lopen TUI module
---

# TUI Implementation Research

> Research covering library selection, architecture patterns, and implementation strategies for the Lopen terminal UI.

---

## 1. Spectre.Console

**Current Version**: 0.54.0 (stable, released Nov 2025). Latest pre-release: 0.54.1-alpha.0.37. The library targets .NET Standard 2.0, net8.0, net9.0, and net10.0. Over 156 dependent GitHub repos including `dotnet/aspire` and `microsoft/semantic-kernel`.

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

## 2. Spectre.Tui

**Repository**: [github.com/spectreconsole/spectre.tui](https://github.com/spectreconsole/spectre.tui)
**Author**: Patrik Svensson (same author as Spectre.Console)
**Current Version**: 0.0.0-preview.0.46 (NuGet, pre-release)
**Target**: .NET 10.0 exclusively
**License**: MIT
**Depends on**: `Spectre.Console.Ansi` 0.54.1-alpha.0.37 (low-level ANSI abstraction, not Spectre.Console itself)

Spectre.Tui is a **low-level, cell-based TUI framework** inspired by [Ratatui](https://ratatui.rs/) (Rust). It provides the foundational rendering layer for building full-screen terminal applications with double-buffered, diff-based rendering.

### Architecture

**Immediate-mode rendering**: Unlike Spectre.Console's retained-mode `IRenderable` tree, Spectre.Tui uses an immediate-mode pattern where widgets draw directly into a cell buffer each frame.

```
┌──────────────────────────────────────────────────┐
│                    Game Loop                      │
│                                                   │
│  Terminal → Renderer → Buffer (front/back)        │
│                ↓                                  │
│         RenderContext                             │
│                ↓                                  │
│    IWidget / IStatefulWidget<T>                   │
│         draw into cells                           │
│                ↓                                  │
│         Buffer.Diff() → minimal ANSI writes       │
└──────────────────────────────────────────────────┘
```

**Double-buffered renderer**: The `Renderer` maintains two `Buffer` instances (front/back). Each frame, widgets render into the current buffer, the renderer diffs against the previous buffer, and only changed cells are written to the terminal. Buffers swap after each frame.

**Terminal abstraction**: Platform-specific `ITerminal` implementations (`WindowsTerminal`, `UnixTerminal`) handle raw terminal I/O. Created via `Terminal.Create()` which auto-detects the platform. Requires ANSI support.

### Core Types

**IWidget** — Stateless widget interface:
```csharp
public interface IWidget
{
    void Render(RenderContext context);
}
```

**IStatefulWidget\<TState\>** — Widget with external state (separation of concerns):
```csharp
public interface IStatefulWidget<in TState>
{
    void Render(RenderContext context, TState state);
}
```

**RenderContext** — Provides the drawing surface for a widget. Supports sub-area rendering via `context.Render(widget, area)` which creates a clipped child context. Key methods:
- `SetString(x, y, text, style, maxWidth)` — Write styled text with grapheme-aware width handling
- `SetLine(x, y, textLine, maxWidth)` — Write a `TextLine` (multiple styled spans)
- `SetSymbol(x, y, char/Rune)` — Write a single character
- `SetStyle(x, y, style)` / `SetStyle(area, style)` — Apply styling
- `SetForeground(x, y, color)` / `SetBackground(x, y, color)` — Direct color control
- `GetCell(x, y)` — Access individual buffer cells
- `Render(widget, area)` — Render a child widget into a sub-area (with viewport clipping)

**Cell** — Individual terminal cell with `Symbol` (string, Unicode-aware), `Style` (foreground, background, decoration). Supports `Bold`, `Italic`, `Underline`, `Strikethrough` decorations.

**Renderer** — Manages the render loop with configurable FPS:
```csharp
using var terminal = Terminal.Create();
var renderer = new Renderer(terminal);
renderer.SetTargetFps(60);

while (running)
{
    renderer.Draw((ctx, elapsed) =>
    {
        // Render widgets into the context
        ctx.Render(new BoxWidget(Color.Red));
        ctx.Render(contentWidget, innerArea);
    });

    // Handle input
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        // Process input...
    }
}
```

**Terminal modes**:
- `FullscreenMode` — Alternate screen buffer, hidden cursor (standard full-screen TUI)
- `InlineMode(height)` — Renders in a fixed-height region within the scrollback (like a progress bar). Supports dynamic height changes via `SetHeight()`.

### Built-in Widgets

| Widget | Description |
|---|---|
| `BoxWidget` | Bordered rectangle with configurable `Border` style (Rounded, Double, etc.) and color |
| `ClearWidget` | Fills area with a character + style (background fill) |
| `ListWidget<T>` | Scrollable, selectable list with highlight symbol, wrap-around, and keyboard navigation. Ported from Ratatui's list algorithm |

### Text System

- `TextSpan` — Styled text segment (`Text` + `Style`)
- `TextLine` — Collection of `TextSpan`s with optional line-level `Style`
- `Text` — Multi-line text content, parsed from Spectre.Console markup syntax (e.g., `[red]Hello[/]`)
- `StringBuffer` — Efficient string building for text construction

### Primitives

- `Rectangle` — Area with `X`, `Y`, `Width`, `Height` plus `Inflate()`, `Intersect()`, `Contains()`, `IsEmpty`
- `Position` — `X`, `Y` coordinate
- `Size` — `Width`, `Height`

### Input Handling

Spectre.Tui does **not** provide a built-in input/event system. Input is handled via raw `Console.ReadKey(intercept: true)` in the application's game loop (see Sandbox example). This is by design — the framework is a rendering layer, not a full application framework.

### Testing

The repo includes `Spectre.Tui.Testing` and `Spectre.Tui.Tests` projects using `Verify.Xunit` + `Spectre.Verify.Extensions` for snapshot testing. Tests verify rendered buffer output against approved snapshots.

### Relevance to Lopen

Spectre.Tui solves Spectre.Console's fundamental limitation: it provides a proper game-loop rendering architecture with double-buffered diff-based output, eliminating the `Live` + `Prompt` incompatibility. The immediate-mode, cell-level rendering gives full control over layout and composition. However, it is **early-stage** (preview, limited widgets, no layout/tree/table/progress equivalents yet) and requires building higher-level components from scratch.

---

## 3. Spectre.Tui vs Spectre.Console

| Aspect | Spectre.Console | Spectre.Tui |
|---|---|---|
| **Version** | 0.54.0 (stable) | 0.0.0-preview.0.46 |
| **Maturity** | Widely adopted (14K+ dependents) | Early preview, ~30 NuGet downloads |
| **Target** | .NET Standard 2.0, net8-10 | .NET 10.0 only |
| **Paradigm** | Retained-mode renderable tree | Immediate-mode cell buffer (Ratatui-style) |
| **Rendering** | Write `IRenderable` → ANSI output | Double-buffered `Cell[]` → diff → minimal ANSI |
| **Layout** | `Layout` with Ratio/Size/MinSize | Manual `Rectangle` calculation |
| **Widgets** | Layout, Panel, Table, Tree, Prompt, Progress, Spinner, Live | Box, Clear, List (early set) |
| **Input** | Blocking `TextPrompt` / `SelectionPrompt` | No built-in input (raw `Console.ReadKey` loop) |
| **Live + Input** | **Incompatible** — `Live` blocks prompts | **Compatible** — render loop + input polling coexist naturally |
| **Threading** | Not thread safe for interactive components | Single-threaded game loop (thread-safe by design) |
| **Focus** | Not built-in | Not built-in (app responsibility) |
| **Text Styling** | `[bold red]text[/]` markup → `IRenderable` | `TextSpan`/`TextLine` with `Style` records; `Text` parses Spectre markup |
| **Terminal Modes** | Standard console output | Fullscreen (alt screen) and Inline (scrollback region) |
| **FPS Control** | N/A (refresh on demand) | Configurable target FPS via `Renderer.SetTargetFps()` |
| **Custom Widgets** | Implement `IRenderable` | Implement `IWidget` / `IStatefulWidget<T>` |
| **Dependency** | Standalone | Depends on `Spectre.Console.Ansi` |
| **Testing** | `Spectre.Console.Testing` (`TestConsole`) | `Spectre.Tui.Testing` (snapshot verification) |

### Spectre.Console Strengths

- **Rich, mature widget library**: Layout, Panel, Table, Tree, Progress, Spinner, Prompt — all production-ready
- **Broad .NET support**: .NET Standard 2.0 through .NET 10
- **Large ecosystem**: 14K+ dependent packages, extensive community documentation
- **Markup language**: Familiar `[bold red]text[/]` syntax
- **Immediate utility**: High-level widgets can build complex UIs quickly

### Spectre.Console Limitations for Lopen

- **Live + Prompt incompatibility**: Cannot accept text input while updating the display
- **No event loop**: Must build a custom render/input loop
- **No focus management**: Tab-switching between panes requires custom implementation
- **Full-redraw model**: `ctx.Refresh()` redraws the entire layout (internal diff helps, but no selective invalidation)

### Spectre.Tui Strengths

- **Same ecosystem**: Built by the same author (Patrik Svensson), uses `Spectre.Console.Ansi` — natural evolution
- **Proper TUI architecture**: Double-buffered, diff-based rendering designed for full-screen apps
- **Concurrent input + display**: Game loop pattern naturally supports polling input while rendering
- **Full control**: Cell-level rendering gives precise control over every pixel
- **Fullscreen + Inline modes**: Alt-screen for full TUI, inline mode for embedded rendering
- **Stateful widgets**: `IStatefulWidget<TState>` separates rendering from state management
- **FPS-controlled rendering**: Built-in frame rate management
- **Auto-resize**: Renderer detects terminal size changes and re-renders automatically
- **Compatible text system**: Parses Spectre.Console markup syntax (`[red]text[/]`)

### Spectre.Tui Limitations

- **Very early stage**: Only Box, Clear, and List widgets available
- **Missing high-level widgets**: No Layout, Panel, Table, Tree, Progress, Spinner equivalents
- **No layout system**: Manual `Rectangle` calculations for positioning
- **No focus management**: Must be implemented by the application
- **No keyboard routing**: Raw `Console.ReadKey` polling only
- **.NET 10 only**: Narrow target (acceptable for Lopen which targets .NET 10)
- **No documentation**: README is minimal; only the Sandbox example demonstrates usage
- **May change at any time**: The README explicitly warns about breaking changes

### Recommendation

**Use Spectre.Tui as the rendering foundation**, building higher-level components on top of it. The rationale:

1. **Architectural fit**: Spectre.Tui's game-loop + double-buffered rendering is the correct architecture for Lopen's requirements (concurrent input + live display). This is the exact problem that Spectre.Console cannot solve.
2. **Same ecosystem**: Same author, same org, compatible markup syntax. As Spectre.Tui matures, it will likely gain first-party widgets that match Spectre.Console's quality.
3. **Lopen as early adopter**: Lopen targets .NET 10 already. Building on Spectre.Tui means contributing to the ecosystem and getting native support as the library matures.
4. **Build what's missing**: The missing widgets (layout splitting, panels, trees, progress) can be implemented as Lopen-specific `IWidget` implementations, potentially contributed upstream.

The alternative (Spectre.Console + custom input thread) requires fighting the library's design. Spectre.Tui's design aligns with what Lopen needs.

### Relevance to Lopen

Spectre.Tui eliminates the need for a hybrid approach with a third-party framework. It provides the low-level rendering architecture that Spectre.Console lacks, while remaining in the same ecosystem. The trade-off is building higher-level widgets, which is manageable given Lopen's specific UI needs.

---

## 4. Split-Screen Layout

### Spectre.Tui Approach

Calculate split regions as `Rectangle` values and render widgets into sub-areas:

```csharp
renderer.Draw((ctx, elapsed) =>
{
    var screen = ctx.Viewport;
    int headerHeight = 4;
    int promptHeight = 3;
    int bodyHeight = screen.Height - headerHeight - promptHeight;

    // Adjustable ratio: 60/40 default, range 50/50 to 80/20
    int activityPercent = Math.Clamp(state.SplitPercent, 50, 80);
    int activityWidth = (int)(screen.Width * activityPercent / 100.0);
    int contextWidth = screen.Width - activityWidth;

    var headerArea = new Rectangle(0, 0, screen.Width, headerHeight);
    var activityArea = new Rectangle(0, headerHeight, activityWidth, bodyHeight);
    var contextArea = new Rectangle(activityWidth, headerHeight, contextWidth, bodyHeight);
    var promptArea = new Rectangle(0, headerHeight + bodyHeight, screen.Width, promptHeight);

    ctx.Render(headerWidget, headerArea);
    ctx.Render(activityWidget, activityArea);
    ctx.Render(contextWidget, contextArea);
    ctx.Render(promptWidget, promptArea);
});
```

### Spectre.Console Approach

```csharp
var layout = new Layout("Root")
    .SplitRows(
        new Layout("TopPanel").Size(4),
        new Layout("Body"),
        new Layout("PromptArea").Size(3));

layout["Body"].SplitColumns(
    new Layout("Activity").Ratio(3).MinimumSize(40),
    new Layout("Context").Ratio(2).MinimumSize(20));
```

Spectre.Console's `Layout` is more declarative but requires `Live` display for updates, which conflicts with input handling.

### Relevance to Lopen

The spec requires "ratio adjustable from 50/50 to 80/20". With Spectre.Tui, layout is explicit `Rectangle` math — more verbose but fully controllable. A `LayoutHelper` utility can encapsulate the split calculations to keep widget code clean.

---

## 5. Progressive Disclosure

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

### Implementation with Spectre.Tui

```csharp
public class ActivityEntry
{
    public string Summary { get; init; }
    public string[] DetailLines { get; init; }
    public bool IsExpanded { get; set; }
    public bool IsCurrentAction { get; set; }

    public int GetHeight() => IsExpanded ? 1 + DetailLines.Length : 1;

    public void Render(RenderContext ctx, int y, int width)
    {
        var prefix = IsExpanded ? "▼" : "●";
        var style = IsCurrentAction
            ? new Style(foreground: Color.Yellow, decoration: Decoration.Bold)
            : Style.Plain;

        ctx.SetString(0, y, $"{prefix} {Summary}", style, width);

        if (IsExpanded)
        {
            for (int i = 0; i < DetailLines.Length; i++)
            {
                ctx.SetString(3, y + 1 + i, DetailLines[i], Style.Plain, width - 3);
            }
        }
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

## 6. Real-Time Updates

### Spectre.Tui Render Loop

Spectre.Tui's architecture inherently supports real-time updates. The renderer's `Draw` callback is invoked every frame:

```csharp
renderer.SetTargetFps(60);

while (running)
{
    renderer.Draw((ctx, elapsed) =>
    {
        // Always renders current state — no explicit refresh needed
        ctx.Render(headerWidget, headerArea);
        ctx.Render(activityWidget, activityArea);
        ctx.Render(contextWidget, contextArea);
        ctx.Render(promptWidget, promptArea);
    });

    // Input polling — naturally interleaved with rendering
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(intercept: true);
        ProcessKey(key, state);
    }
}
```

**Key advantage**: The double-buffered diff ensures only changed cells are written to the terminal, so rendering the full layout every frame is efficient. No explicit dirty-tracking or partial refresh is needed.

### Background Updates

For async state changes (e.g., agent actions streaming in from a background task):

```csharp
// State is updated from background tasks
// The render loop reads the latest state each frame
public class TuiState
{
    // Thread-safe observable state
    private volatile string _currentAction;
    public string CurrentAction
    {
        get => _currentAction;
        set => _currentAction = value;
    }

    // Use ConcurrentQueue for streaming activity entries
    public ConcurrentQueue<ActivityEntry> PendingEntries { get; } = new();
}
```

The render loop drains the queue and renders — no `Invoke()` or dispatcher needed since the render loop is single-threaded and reads state at a defined point each frame.

### Relevance to Lopen

The TUI must update the activity area as agent actions stream in, the context panel as tasks complete, and the top panel as token counts change — all while the prompt area remains responsive. Spectre.Tui's game-loop architecture handles this naturally: background tasks update shared state, and the render loop picks up changes on the next frame.

---

## 7. Keyboard Input Handling

### The Core Challenge

Lopen needs simultaneous:
- **Text input** in the prompt area (typing messages)
- **Keyboard shortcuts** (`Ctrl+P`, `Alt+Enter`, `Tab`, `1-9`)
- **Navigation** (scroll activity area, expand/collapse entries)

### Spectre.Tui Approach

Input is handled via `Console.ReadKey` in the game loop. A custom input handler routes keys based on focus state:

```csharp
public class InputHandler
{
    private readonly StringBuilder _inputBuffer = new();
    private FocusedPane _focusedPane = FocusedPane.Prompt;

    public void ProcessKey(ConsoleKeyInfo key, TuiState state)
    {
        // Global shortcuts (work regardless of focus)
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.P)
        {
            state.TogglePause();
            return;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            _focusedPane = _focusedPane.Next();
            return;
        }

        // Focus-specific handling
        switch (_focusedPane)
        {
            case FocusedPane.Prompt:
                HandlePromptInput(key, state);
                break;
            case FocusedPane.Activity:
                HandleActivityInput(key, state);
                break;
            case FocusedPane.Context:
                HandleContextInput(key, state);
                break;
        }
    }

    private void HandlePromptInput(ConsoleKeyInfo key, TuiState state)
    {
        if (key.Modifiers.HasFlag(ConsoleModifiers.Alt) && key.Key == ConsoleKey.Enter)
            _inputBuffer.AppendLine();
        else if (key.Key == ConsoleKey.Enter)
        {
            state.SubmitPrompt(_inputBuffer.ToString());
            _inputBuffer.Clear();
        }
        else if (key.Key == ConsoleKey.Backspace && _inputBuffer.Length > 0)
            _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
        else if (!char.IsControl(key.KeyChar))
            _inputBuffer.Append(key.KeyChar);
    }

    private void HandleActivityInput(ConsoleKeyInfo key, TuiState state)
    {
        if (key.Key == ConsoleKey.UpArrow) state.ScrollActivityUp();
        else if (key.Key == ConsoleKey.DownArrow) state.ScrollActivityDown();
        else if (key.Key == ConsoleKey.Enter) state.ToggleExpandEntry();
        else if (key.Key >= ConsoleKey.D1 && key.Key <= ConsoleKey.D9)
            state.OpenResource((int)key.Key - (int)ConsoleKey.D0);
    }
}
```

### Relevance to Lopen

The keyboard handling requirements require building a custom input routing layer on top of `Console.ReadKey`. This is more work than a full application framework's event system but gives complete control over key routing. A `InputHandler` class with focus-aware dispatch is manageable for Lopen's defined shortcut set.

---

## 8. Modal & Overlay Rendering

### The Core Challenge

The spec requires multiple modal overlays: confirmation dialogs, error dialogs, session resume prompt, landing page, module selection, and resource viewer. In Spectre.Tui's immediate-mode, cell-based rendering, modals are rendered as layers on top of the main layout within the same render frame — there is no windowing system.

### Implementation Pattern

Modals are implemented by conditionally rendering an overlay region on top of the existing layout during the `renderer.Draw` callback:

```csharp
renderer.Draw((ctx, elapsed) =>
{
    // Always render the main layout
    ctx.Render(headerWidget, regions.Header);
    ctx.Render(activityWidget, regions.Activity);
    ctx.Render(contextWidget, regions.Context);
    ctx.Render(promptWidget, regions.Prompt);

    // Render modal on top if active
    if (state.ActiveModal != null)
    {
        // Center the modal on screen
        var screen = ctx.Viewport;
        int modalWidth = Math.Min(60, screen.Width - 4);
        int modalHeight = state.ActiveModal.GetRequiredHeight();
        int x = (screen.Width - modalWidth) / 2;
        int y = (screen.Height - modalHeight) / 2;
        var modalArea = new Rectangle(x, y, modalWidth, modalHeight);

        // Dim background by overwriting visible cells with muted style
        ctx.SetStyle(screen, new Style(foreground: Color.Grey, background: Color.Black));

        // Render modal content into the centered area
        ctx.Render(state.ActiveModal.Widget, modalArea);
    }
});
```

### Modal State Management

```csharp
public class ModalState
{
    public IModalWidget? ActiveModal { get; set; }
    public Stack<IModalWidget> ModalStack { get; } = new(); // for nested modals
}

public interface IModalWidget : IWidget
{
    int GetRequiredHeight();
    void HandleKey(ConsoleKeyInfo key, TuiState state);
}
```

When a modal is active, the `InputHandler` routes all keys to `ActiveModal.HandleKey()` instead of the normal focus-based routing. This prevents interaction with the underlying layout while a modal is displayed.

### Modal Types Required by Spec

| Modal | Trigger | Options |
|---|---|---|
| Landing Page | First startup (no session) | Any key to dismiss |
| Session Resume | Startup with existing session | Resume / Start New / View Details |
| Confirmation | Dangerous or multi-file actions | Yes / No / Always / Other |
| Error | Critical failures | Retry / Cancel, with details |
| Module Selection | Multiple modules available | Arrow key selection |
| Resource Viewer | Press 1-9 on active resource | Scrollable content, Esc to close |

### Relevance to Lopen

The spec defines 6+ distinct modal types. Since Spectre.Tui has no built-in overlay/modal system, implementing a `ModalWidget` base with centered rendering, background dimming, and input capture is necessary. The immediate-mode rendering makes this straightforward — modals are simply rendered last in the draw callback, overwriting the cells beneath them.

---

## 9. Component Architecture

### Design Principles (from spec)

1. Components accept data/state as input (not fetched internally)
2. All external dependencies behind interfaces that can be stubbed
3. Each component self-registers with the gallery

### Interface-Based Architecture with Spectre.Tui

```csharp
// Lopen TUI component interface built on Spectre.Tui's IWidget
public interface ITuiComponent : IWidget
{
    string Name { get; }
    string Description { get; }
    IEnumerable<StubScenario> GetStubScenarios();
}

// Stateful variant
public interface IStatefulTuiComponent<in TState> : IStatefulWidget<TState>
{
    string Name { get; }
    string Description { get; }
    IEnumerable<StubScenario> GetStubScenarios();
}

// Stub scenario for gallery
public record StubScenario(string Name, object State);
```

### State Injection Pattern

```csharp
public record ContextPanelState(
    TaskInfo CurrentTask,
    IReadOnlyList<ComponentInfo> Components,
    IReadOnlyList<ResourceInfo> ActiveResources);

public class ContextPanelWidget : IStatefulTuiComponent<ContextPanelState>
{
    public string Name => "Context Panel";
    public string Description => "Task hierarchy and context display";

    public void Render(RenderContext context, ContextPanelState state)
    {
        // Render border
        context.Render(new BoxWidget(Color.Grey));
        var inner = context.Viewport.Inflate(-1, -1);

        // Render task tree into inner area
        RenderTaskTree(context, inner, state.CurrentTask);
    }

    public IEnumerable<StubScenario> GetStubScenarios() =>
    [
        new("Empty", new ContextPanelState(null, [], [])),
        new("In Progress", CreateInProgressState()),
        new("Completed", CreateCompletedState()),
        new("Error", CreateErrorState()),
    ];
}
```

### Gallery

```csharp
// Components discovered via DI
services.AddTransient<IStatefulTuiComponent<ContextPanelState>, ContextPanelWidget>();
// ... etc

// Gallery runs each component with its stub scenarios using Spectre.Tui's renderer
public class ComponentGallery
{
    public void Run(ITuiComponent component, StubScenario scenario)
    {
        using var terminal = Terminal.Create();
        var renderer = new Renderer(terminal);
        renderer.SetTargetFps(30);

        var running = true;
        while (running)
        {
            renderer.Draw((ctx, elapsed) =>
            {
                ctx.Render(component);
            });

            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                running = false;
        }
    }
}
```

### Testability

```csharp
[Fact]
public async Task ContextPanel_WithEmptyState_RendersPlaceholder()
{
    // Use Spectre.Tui.Testing for snapshot verification
    var widget = new ContextPanelWidget();
    var state = new ContextPanelState(null, [], []);

    // Render into a test buffer and verify snapshot
    await Verify(RenderToString(widget, state, width: 80, height: 24));
}
```

### Relevance to Lopen

The spec requires every component to work in the `lopen test tui` gallery with mock data. The `IStatefulTuiComponent<T>` interface built on Spectre.Tui's `IStatefulWidget<T>` provides both gallery support and testability. DI registration ensures new components automatically appear in the gallery.

---

## 10. Syntax Highlighting

### Options for Terminal Syntax Highlighting

| Library | Approach | Languages | Size |
|---|---|---|---|
| Spectre.Tui TextSpan styling | Manual `Style` per span | N/A (manual) | 0 (built-in) |
| Spectre.Tui Text markup | `[red]text[/]` parsed to styled spans | N/A (manual) | 0 (built-in) |
| TextMateSharp | VS Code TextMate grammars | 50+ languages | ~5MB with grammars |
| Custom regex-based | Pattern matching per language | Configurable | Small |

### Spectre.Tui TextSpan (Simplest)

```csharp
// Manual highlighting for diff output using TextLine/TextSpan
public TextLine HighlightDiffLine(string line)
{
    if (line.StartsWith('+'))
        return new TextLine(new TextSpan(line, new Style(foreground: Color.Green)));
    if (line.StartsWith('-'))
        return new TextLine(new TextSpan(line, new Style(foreground: Color.Red)));
    if (line.StartsWith("@@"))
        return new TextLine(new TextSpan(line, new Style(foreground: Color.Cyan)));
    return new TextLine(new TextSpan(line));
}
```

### TextMateSharp → Spectre.Tui Spans

```csharp
// TextMateSharp tokenizes → convert to TextSpan list
var grammar = registry.LoadGrammar(registryOptions.GetScopeByLanguageId("csharp"));
var result = grammar.TokenizeLine(codeLine, null);

var spans = result.Tokens.Select(token =>
{
    var color = GetColorFromScope(token.Scopes);
    var text = codeLine[token.StartIndex..token.EndIndex];
    return new TextSpan(text, new Style(foreground: color));
});

return new TextLine(spans.ToArray());
```

### Recommended Approach for Lopen

1. **Diff highlighting**: Custom `TextSpan` styling — diffs have simple, well-defined syntax
2. **Code blocks**: TextMateSharp for accurate syntax highlighting, output as `TextLine` collections
3. **JSON**: Custom regex or TextMateSharp with JSON grammar

### Relevance to Lopen

The spec requires "syntax highlighting in code blocks" and "diff viewer with syntax highlighting." Spectre.Tui's `TextSpan`/`TextLine` system maps directly to tokenized syntax output. TextMateSharp provides VS Code-quality highlighting. The cost is a ~5MB grammar dependency, which is acceptable for a developer tool.

---

## 11. Recommended NuGet Packages

### Core Packages

| Package | Version | Purpose |
|---|---|---|
| `Spectre.Tui` | 0.0.0-preview.0.46 | Cell-based TUI rendering (Renderer, Widget, Buffer, Terminal) |
| `Spectre.Console` | 0.54.0 | Rich console output for non-TUI paths (CLI help, error output, etc.) |
| `Spectre.Console.Json` | 0.54.0 | JSON syntax highlighting |
| `Spectre.Console.Cli` | 0.53.1 (stable) / 1.0.0-alpha.0.12 (new independent) | CLI command parsing (used by CLI module) |

### Syntax Highlighting

| Package | Version | Purpose |
|---|---|---|
| `TextMateSharp` | 2.0.3 | TextMate grammar engine for code highlighting |
| `TextMateSharp.Grammars` | latest | Bundled VS Code grammars |

### Testing

| Package | Version | Purpose |
|---|---|---|
| `Spectre.Tui.Testing` | 0.0.0-preview.0.46 | Snapshot testing for Spectre.Tui widgets |
| `Verify.Xunit` | latest | Snapshot/approval testing framework |

### Relevance to Lopen

The core rendering stack is `Spectre.Tui` for the full-screen TUI. `Spectre.Console` is retained for non-TUI output (CLI help text, error messages, etc.) and as a fallback for rich content formatting. `TextMateSharp` adds code highlighting. `Spectre.Tui.Testing` enables snapshot testing for the component gallery.

---

## 12. Implementation Approach

### Recommended: Spectre.Tui with Custom Widget Layer

Build the full-screen TUI on **Spectre.Tui's rendering engine**, implementing Lopen-specific higher-level widgets (layout splitting, panels, tree view, prompt area) as `IWidget`/`IStatefulWidget<T>` implementations.

```
┌──────────────────────────────────────────────────────────┐
│                 Spectre.Tui Renderer                      │
│  Terminal → double-buffered cells → diff → ANSI output    │
│                                                           │
│  ┌─────────────────────────────────────────────────────┐  │
│  │              Lopen Widget Layer                      │  │
│  │                                                     │  │
│  │  ┌─────────────────────┐  ┌──────────────────────┐  │  │
│  │  │  ActivityWidget     │  │  ContextWidget       │  │  │
│  │  │  (scrollable list   │  │  (tree view,         │  │  │
│  │  │   with progressive  │  │   resource list,     │  │  │
│  │  │   disclosure)       │  │   task hierarchy)    │  │  │
│  │  └─────────────────────┘  └──────────────────────┘  │  │
│  │                                                     │  │
│  │  ┌─────────────────────────────────────────────┐    │  │
│  │  │  PromptWidget (text input with cursor)      │    │  │
│  │  └─────────────────────────────────────────────┘    │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                           │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  InputHandler (Console.ReadKey → focus-aware routing) │  │
│  └─────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

### Why Spectre.Tui

1. **Correct architecture**: Game-loop + double-buffered rendering is the right pattern for Lopen's concurrent input + display requirement
2. **Same ecosystem**: Same author as Spectre.Console, compatible markup, shared `Spectre.Console.Ansi` foundation
3. **Full control**: Cell-level rendering gives precise control over every aspect of the display
4. **No bridging needed**: Unlike a hybrid approach, there's no impedance mismatch between two libraries
5. **Future-proof**: As Spectre.Tui matures, Lopen benefits from new widgets without architectural changes

### What Must Be Built

Since Spectre.Tui only provides Box, Clear, and List widgets, Lopen must implement:

1. **LayoutWidget** — Split-screen layout calculator (Rectangle partitioning)
2. **PanelWidget** — Bordered panel with title, wrapping BoxWidget
3. **TreeWidget** — Hierarchical display with collapse/expand
4. **ScrollableWidget** — Scrollable viewport for long content (virtual list with offset tracking)
5. **PromptWidget** — Text input with cursor, history, and multiline support
6. **ProgressWidget** — Progress indicator / spinner
7. **StatusBarWidget** — Top panel with token counts, model info, session status
8. **ModalWidget** — Centered overlay with background dimming, input capture, and dismiss handling (see Section 8)

### Application Loop

```csharp
using var terminal = Terminal.Create();
var renderer = new Renderer(terminal);
renderer.SetTargetFps(60);

var state = new TuiState();
var input = new InputHandler();
var layout = new LayoutHelper();

while (!state.ShouldExit)
{
    renderer.Draw((ctx, elapsed) =>
    {
        var regions = layout.Calculate(ctx.Viewport, state.SplitPercent);

        ctx.Render(new HeaderWidget(), regions.Header, state.Header);
        ctx.Render(new ActivityWidget(), regions.Activity, state.Activity);
        ctx.Render(new ContextWidget(), regions.Context, state.Context);
        ctx.Render(new PromptWidget(), regions.Prompt, state.Prompt);
    });

    while (Console.KeyAvailable)
    {
        input.ProcessKey(Console.ReadKey(intercept: true), state);
    }
}
```

### Component Lifecycle

```
Startup
  ├─ Parse CLI flags (--no-welcome, --no-logo, --resume)
  ├─ Initialize DI container with widget registrations
  ├─ Create Terminal + Renderer
  ├─ Check for existing session → show Resume Modal or Landing Page
  └─ Enter main loop
       ├─ renderer.Draw() each frame
       │    ├─ Calculate layout regions
       │    ├─ Render each widget with current state
       │    └─ Diff + flush (automatic)
       └─ Process input between frames
```

### Recommended Phasing

1. **Phase 1**: Core rendering — LayoutWidget, PanelWidget, BoxWidget wrappers, basic text rendering
2. **Phase 2**: Input — PromptWidget with cursor, InputHandler with focus routing, keyboard shortcuts
3. **Phase 3**: Content widgets — ActivityWidget with progressive disclosure, ContextWidget with tree view
4. **Phase 4**: Component gallery (`lopen test tui`) with stub data scenarios
5. **Phase 5**: Integration with core, LLM, and storage modules

### Relevance to Lopen

Building on Spectre.Tui provides a clean, single-library architecture with the correct rendering paradigm. The trade-off is implementing higher-level widgets, but this gives Lopen exactly the components it needs without carrying unused framework weight. The component architecture (Section 8) ensures each widget is testable and gallery-ready.

---

## 13. TUI Application Shell Architecture

### Current State

The TUI module has a complete set of building blocks but no running application:

- **`StubTuiApplication`** implements `ITuiApplication` as a no-op. `RunAsync` sets `IsRunning = true` and returns immediately. Registered via DI in `ServiceCollectionExtensions.AddLopenTui()`.
- **15 real components** exist (`TopPanelComponent`, `ActivityPanelComponent`, `ContextPanelComponent`, `PromptAreaComponent`, `GalleryListComponent`, `LandingPageComponent`, `SessionResumeModalComponent`, `DiffViewerComponent`, `PhaseTransitionComponent`, `ResearchDisplayComponent`, `FilePickerComponent`, `SelectionModalComponent`, `ConfirmationModalComponent`, `ErrorModalComponent`, `SpinnerComponent`). All render to `string[]` — pure functions of `(Data, ScreenRect) → string[]`.
- **`LayoutCalculator`** computes `LayoutRegions` (Header, Activity, Context, Prompt) from screen dimensions and split percentage. Fully implemented, returns `ScreenRect` records.
- **`KeyboardHandler`** maps `KeyInput` → `KeyAction` with focus-aware routing. Supports Tab cycling, Ctrl+P pause, Ctrl+C cancel, Enter submit, Alt+Enter newline, 1-9 resource view, Space/Enter expand. Fully implemented.
- **`Spectre.Tui` 0.0.0-preview.0.46** is referenced in the csproj but never used — no `Terminal.Create()`, no `Renderer`, no `IWidget` implementations.

### The Gap

A real `ITuiApplication` implementation must:

1. Create a `Terminal` + `Renderer` (Spectre.Tui)
2. Run a render loop that reads state, calls components, and writes output to cells
3. Poll `Console.ReadKey` and feed through `KeyboardHandler`
4. Bridge the `string[]` component output → Spectre.Tui's `RenderContext` cell buffer
5. Handle terminal resize, alt-screen, and graceful shutdown

### Application Shell Design

```csharp
internal sealed class TuiApplication : ITuiApplication
{
    private readonly IComponentGallery _gallery;
    private readonly KeyboardHandler _keyboard;
    private readonly ILogger<TuiApplication> _logger;
    private volatile bool _running;

    public bool IsRunning => _running;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _running = true;

        using var terminal = Terminal.Create(new FullscreenMode());
        var renderer = new Renderer(terminal);
        renderer.SetTargetFps(30);

        var state = new TuiState();

        while (!cancellationToken.IsCancellationRequested && _running)
        {
            // 1. Drain input
            while (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                var input = MapToKeyInput(keyInfo);
                var action = _keyboard.Handle(input, state.CurrentFocus);
                ApplyAction(action, state, keyInfo);
            }

            // 2. Render frame
            renderer.Draw((ctx, elapsed) =>
            {
                var regions = LayoutCalculator.Calculate(
                    ctx.Viewport.Width, ctx.Viewport.Height,
                    state.SplitPercent);

                RenderRegion(ctx, regions.Header, _topPanel.Render(state.TopData, ToScreenRect(regions.Header)));
                RenderRegion(ctx, regions.Activity, _activityPanel.Render(state.ActivityData, ToScreenRect(regions.Activity)));
                RenderRegion(ctx, regions.Context, _contextPanel.Render(state.ContextData, ToScreenRect(regions.Context)));
                RenderRegion(ctx, regions.Prompt, _promptArea.Render(state.PromptData, ToScreenRect(regions.Prompt)));

                if (state.ActiveModal is not null)
                    RenderModal(ctx, state);
            });

            // 3. Yield to avoid busy-waiting (Renderer.Draw handles FPS throttling internally)
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync()
    {
        _running = false;
        return Task.CompletedTask;
    }
}
```

### Bridging `string[]` Components → Spectre.Tui Cells

The existing components produce `string[]`. Each line maps to a row of cells in the Spectre.Tui buffer:

```csharp
private static void RenderRegion(RenderContext ctx, LayoutRegions.ScreenRect region, string[] lines)
{
    for (int row = 0; row < lines.Length && row < region.Height; row++)
    {
        ctx.SetString(region.X, region.Y + row, lines[row], Style.Plain, region.Width);
    }
}
```

This is the **minimal bridge** — plain text, no styling. To add color, components would need to evolve to produce `TextLine[]` (Spectre.Tui's styled span model) instead of `string[]`. This can be done incrementally:

1. **Phase 1**: `string[]` → `SetString` with `Style.Plain`. Validates layout and composition.
2. **Phase 2**: Introduce `IStyledTuiComponent<TData>` returning `TextLine[]`. Components opt in one at a time.
3. **Phase 3**: Components use `ColorPalette` to produce `TextSpan`s with semantic styles (success=green, error=red, muted=grey).

### ScreenRect → Rectangle Mapping

The existing `ScreenRect` record is decoupled from Spectre.Tui by design. The bridge is trivial:

```csharp
private static Rectangle ToRectangle(ScreenRect rect) =>
    new(rect.X, rect.Y, rect.Width, rect.Height);
```

Alternatively, `RenderRegion` can use `ctx.SetString(x, y, ...)` directly with `ScreenRect` coordinates, avoiding Spectre.Tui `Rectangle` entirely in the component layer. This keeps components framework-agnostic.

### Main Render Loop Pattern

The loop follows the standard game-loop pattern that Spectre.Tui is designed for:

```
┌─────────────────────────────────┐
│ 1. Poll Input (non-blocking)    │
│    └─ Console.ReadKey if avail  │
│    └─ Map to KeyInput           │
│    └─ KeyboardHandler.Handle()  │
│    └─ Apply KeyAction to state  │
│                                 │
│ 2. Update State                 │
│    └─ Drain workflow events     │
│    └─ Advance spinner frame     │
│    └─ Process pending prompts   │
│                                 │
│ 3. Render Frame                 │
│    └─ LayoutCalculator regions  │
│    └─ Component.Render(data)    │
│    └─ Bridge string[] → cells   │
│    └─ Modal overlay if active   │
│    └─ Renderer diffs + flushes  │
│                                 │
│ 4. Throttle (FPS target)        │
│    └─ Renderer.Draw handles it  │
└────────────── loop ─────────────┘
```

### Terminal I/O Considerations

**Spectre.Tui `FullscreenMode`** switches to the alternate screen buffer and hides the cursor. This is the correct mode for Lopen's full-screen layout. On exit (dispose), it restores the original screen.

**Input polling**: `Console.ReadKey(intercept: true)` is used inside the loop. Spectre.Tui does not provide an input abstraction — this is by design (rendering-only framework). The existing `KeyboardHandler` already expects `KeyInput` records built from `ConsoleKeyInfo`, so the mapping is:

```csharp
private static KeyInput MapToKeyInput(ConsoleKeyInfo info) => new()
{
    Key = info.Key,
    Modifiers = info.Modifiers,
    KeyChar = info.KeyChar
};
```

**Resize handling**: Spectre.Tui's `Renderer` detects terminal size changes between frames. The `RenderContext.Viewport` reflects the current terminal size each frame, so `LayoutCalculator.Calculate` is called with fresh dimensions every frame — no explicit resize handler needed.

**Cursor for prompt**: The prompt area needs a visible cursor at the text insertion point. After rendering the prompt region, position the terminal cursor:

```csharp
// After rendering, position cursor at end of prompt text
var promptRect = regions.Prompt;
int cursorX = promptRect.X + 2 + state.PromptCursorOffset; // ">" prefix + text offset
int cursorY = promptRect.Y;
ctx.SetStyle(cursorX, cursorY, new Style(decoration: Decoration.Underline));
```

### TuiState: Central State Object

```csharp
internal sealed class TuiState
{
    // Focus
    public FocusPanel CurrentFocus { get; set; } = FocusPanel.Prompt;

    // Layout
    public int SplitPercent { get; set; } = 60;

    // Component data (updated by workflow thread, read by render loop)
    public TopPanelData TopData { get; set; } = TopPanelData.Default;
    public ActivityPanelData ActivityData { get; set; } = ActivityPanelData.Empty;
    public ContextPanelData ContextData { get; set; } = ContextPanelData.Empty;
    public PromptAreaData PromptData { get; set; } = PromptAreaData.Default;

    // Modal
    public ModalState? ActiveModal { get; set; }

    // Prompt input buffer
    public StringBuilder InputBuffer { get; } = new();
    public int PromptCursorOffset => InputBuffer.Length;

    // Spinner frame (incremented each render)
    public int SpinnerFrame { get; set; }
}
```

Data objects are replaced atomically (reference assignment is atomic in .NET). The workflow thread creates a new `TopPanelData` / `ActivityPanelData` and assigns it; the render loop reads the latest reference each frame.

### DI Registration

```csharp
public static IServiceCollection AddLopenTui(this IServiceCollection services)
{
    // Replace stub with real implementation
    services.AddSingleton<ITuiApplication, TuiApplication>();

    // Existing registrations unchanged
    services.AddSingleton<IComponentGallery>(sp => { /* ... */ });
    return services;
}
```

The `--headless` flag continues to use `StubTuiApplication` via a conditional registration or named resolution.

---

## 14. Component Gallery Launcher (`lopen test tui`)

### Purpose

An interactive gallery that lets developers browse and preview all registered TUI components with mock data. This validates component rendering without running the full workflow.

### Existing Infrastructure

- **`IComponentGallery`** / **`ComponentGallery`**: Registry with `Register()`, `GetAll()`, `GetByName()`. All 14 built-in components are registered in `AddLopenTui()`.
- **`GalleryListComponent`**: Renders a selectable list of components with `▶` selection marker. `FromGallery()` creates `GalleryListData` from the registry.
- **`IPreviewableComponent`**: Optional interface with `RenderPreview(width, height) → string[]`. Components that implement this can show preview output in the gallery.
- **`ITuiComponent`**: All components have `Name` and `Description` properties for gallery listing.

### Implementation Design

The gallery launcher is a self-contained TUI application (separate from the main workflow TUI) that uses the same Spectre.Tui renderer:

```csharp
internal sealed class ComponentGalleryApp
{
    private readonly IComponentGallery _gallery;

    public async Task RunAsync(CancellationToken ct)
    {
        using var terminal = Terminal.Create(new FullscreenMode());
        var renderer = new Renderer(terminal);
        renderer.SetTargetFps(30);

        var components = _gallery.GetAll();
        int selectedIndex = 0;
        ITuiComponent? previewComponent = null;

        while (!ct.IsCancellationRequested)
        {
            renderer.Draw((ctx, elapsed) =>
            {
                var screen = ctx.Viewport;

                // Left half: component list
                int listWidth = screen.Width / 3;
                var listRegion = new ScreenRect(0, 0, listWidth, screen.Height);
                var listData = new GalleryListData
                {
                    Items = components.Select(c => new GalleryItem(c.Name, c.Description)).ToList(),
                    SelectedIndex = selectedIndex
                };
                var listLines = new GalleryListComponent().Render(listData, listRegion);
                RenderLines(ctx, listRegion, listLines);

                // Right two-thirds: preview
                int previewX = listWidth + 1;
                int previewWidth = screen.Width - previewX;
                var previewRegion = new ScreenRect(previewX, 0, previewWidth, screen.Height);

                if (components[selectedIndex] is IPreviewableComponent previewable)
                {
                    var previewLines = previewable.RenderPreview(previewWidth, screen.Height);
                    RenderLines(ctx, previewRegion, previewLines);
                }
                else
                {
                    ctx.SetString(previewX, 1, $"  {components[selectedIndex].Name}", Style.Plain, previewWidth);
                    ctx.SetString(previewX, 2, $"  {components[selectedIndex].Description}", Style.Plain, previewWidth);
                    ctx.SetString(previewX, 4, "  (No preview available — implement IPreviewableComponent)", Style.Plain, previewWidth);
                }

                // Divider line
                for (int y = 0; y < screen.Height; y++)
                    ctx.SetString(listWidth, y, "│", Style.Plain, 1);

                // Footer
                ctx.SetString(0, screen.Height - 1, " ↑↓: Navigate  Enter: Select  Q: Quit", Style.Plain, screen.Width);
            });

            // Input
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = Math.Max(0, selectedIndex - 1);
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = Math.Min(components.Count - 1, selectedIndex + 1);
                        break;
                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        return;
                }
            }

            await Task.Delay(1, ct).ConfigureAwait(false);
        }
    }
}
```

### Making Components Previewable

Components that want gallery previews implement `IPreviewableComponent`:

```csharp
public sealed class TopPanelComponent : ITuiComponent, IPreviewableComponent
{
    // ... existing Render method ...

    public string[] RenderPreview(int width, int height)
    {
        var stubData = new TopPanelData
        {
            Version = "v0.1.0",
            ModelName = "Claude Sonnet 4",
            ContextUsedTokens = 24_000,
            ContextMaxTokens = 128_000,
            GitBranch = "feature/tui",
            IsAuthenticated = true,
            PhaseName = "Building",
            CurrentStep = 3,
            TotalSteps = 7,
            StepLabel = "Iterate through tasks",
            ShowLogo = true,
        };
        return Render(stubData, new ScreenRect(0, 0, width, height));
    }
}
```

### CLI Integration

The gallery is launched via a CLI subcommand:

```csharp
// In command registration:
var testTuiCommand = new Command("tui", "Launch TUI component gallery");
testCommand.AddCommand(testTuiCommand);

testTuiCommand.SetAction(async (ParseResult _, CancellationToken ct) =>
{
    var gallery = services.GetRequiredService<IComponentGallery>();
    var app = new ComponentGalleryApp(gallery);
    await app.RunAsync(ct);
    return ExitCodes.Success;
});
```

### Gallery Features (Incremental)

1. **v1**: List + preview pane with `IPreviewableComponent`. Arrow key navigation, Q to quit.
2. **v2**: Multiple stub scenarios per component (e.g., "empty state", "in progress", "error state"). Use a scenarios list below the preview.
3. **v3**: Live resize testing — gallery re-renders as terminal is resized, validating responsive layout.
4. **v4**: Side-by-side comparison — show two components simultaneously to verify visual consistency.

---

## 15. Workflow Integration

### Architecture Overview

The TUI runs on the main thread (render loop). The workflow engine runs on a background thread. Communication is via shared state with atomic reference updates.

```
┌──────────────────────┐       ┌──────────────────────┐
│   TUI Thread (main)  │       │  Workflow Thread      │
│                      │       │                       │
│  Render loop:        │       │  WorkflowEngine:      │
│  ┌─ poll input       │       │  ┌─ InitializeAsync   │
│  ├─ read TuiState    │◄──────┤  ├─ Fire(trigger)     │
│  ├─ render frame     │       │  ├─ Execute step      │
│  └─ throttle         │       │  └─ Update TuiState   │
│                      │       │                       │
│  User actions:       │───────►  Commands:            │
│  ├─ SubmitPrompt     │       │  ├─ process prompt    │
│  ├─ TogglePause      │       │  ├─ pause/resume      │
│  └─ Cancel           │       │  └─ cancel            │
└──────────────────────┘       └──────────────────────┘
```

### Event-Driven Updates (Workflow → TUI)

The workflow thread produces events that the TUI consumes. Rather than a complex event bus, use a `ConcurrentQueue` of typed events:

```csharp
/// <summary>
/// Events produced by the workflow engine for TUI consumption.
/// </summary>
public abstract record TuiEvent;

public sealed record ActivityEntryEvent(ActivityEntry Entry) : TuiEvent;
public sealed record PhaseChangedEvent(string PhaseName, int CurrentStep, int TotalSteps) : TuiEvent;
public sealed record TaskProgressEvent(string TaskName, TaskState State) : TuiEvent;
public sealed record ContextUpdatedEvent(ContextPanelData NewContext) : TuiEvent;
public sealed record TokenUsageEvent(long UsedTokens, long MaxTokens) : TuiEvent;
public sealed record ModalRequestEvent(ModalState Modal) : TuiEvent;
public sealed record SpinnerEvent(string Message, int ProgressPercent) : TuiEvent;

internal sealed class TuiEventBus
{
    private readonly ConcurrentQueue<TuiEvent> _events = new();

    /// <summary>Called by workflow thread to post events.</summary>
    public void Post(TuiEvent evt) => _events.Enqueue(evt);

    /// <summary>Called by TUI thread each frame to drain events.</summary>
    public void DrainInto(TuiState state)
    {
        while (_events.TryDequeue(out var evt))
        {
            switch (evt)
            {
                case ActivityEntryEvent e:
                    state.ActivityData = state.ActivityData.WithEntry(e.Entry);
                    break;
                case PhaseChangedEvent e:
                    state.TopData = state.TopData with
                    {
                        PhaseName = e.PhaseName,
                        CurrentStep = e.CurrentStep,
                        TotalSteps = e.TotalSteps
                    };
                    break;
                case ContextUpdatedEvent e:
                    state.ContextData = e.NewContext;
                    break;
                case TokenUsageEvent e:
                    state.TopData = state.TopData with
                    {
                        ContextUsedTokens = e.UsedTokens,
                        ContextMaxTokens = e.MaxTokens
                    };
                    break;
                case ModalRequestEvent e:
                    state.ActiveModal = e.Modal;
                    break;
                // ... other events
            }
        }
    }
}
```

### User Input → Orchestration Commands (TUI → Workflow)

When the user submits a prompt or triggers an action, the TUI posts a command to the workflow thread:

```csharp
/// <summary>
/// Commands from TUI to workflow engine.
/// </summary>
public abstract record TuiCommand;

public sealed record SubmitPromptCommand(string Text) : TuiCommand;
public sealed record TogglePauseCommand : TuiCommand;
public sealed record CancelCommand : TuiCommand;
public sealed record SlashCommand(string Command, string? Arguments) : TuiCommand;

internal sealed class TuiCommandBus
{
    private readonly ConcurrentQueue<TuiCommand> _commands = new();

    /// <summary>Called by TUI thread when user acts.</summary>
    public void Post(TuiCommand cmd) => _commands.Enqueue(cmd);

    /// <summary>Called by workflow thread to drain commands.</summary>
    public bool TryDequeue(out TuiCommand? command) => _commands.TryDequeue(out command);
}
```

### Applying KeyActions in the Render Loop

```csharp
private void ApplyAction(KeyAction action, TuiState state, ConsoleKeyInfo keyInfo)
{
    switch (action)
    {
        case KeyAction.SubmitPrompt:
            var text = state.InputBuffer.ToString().Trim();
            if (!string.IsNullOrEmpty(text))
            {
                var slashCmd = _slashRegistry.TryParse(text);
                if (slashCmd is not null)
                    _commandBus.Post(new SlashCommand(slashCmd.Command, text));
                else
                    _commandBus.Post(new SubmitPromptCommand(text));
                state.InputBuffer.Clear();
            }
            break;

        case KeyAction.InsertNewline:
            state.InputBuffer.AppendLine();
            break;

        case KeyAction.TogglePause:
            _commandBus.Post(new TogglePauseCommand());
            state.PromptData = state.PromptData with { IsPaused = !state.PromptData.IsPaused };
            break;

        case KeyAction.Cancel:
            _commandBus.Post(new CancelCommand());
            break;

        case KeyAction.CycleFocusForward:
            state.CurrentFocus = KeyboardHandler.CycleFocus(state.CurrentFocus);
            state.PromptData = state.PromptData with
            {
                CustomHints = KeyboardHandler.GetHints(state.CurrentFocus, state.PromptData.IsPaused)
                    .ToArray()
            };
            break;

        case KeyAction.None:
            // Character input (only when prompt focused)
            if (state.CurrentFocus == FocusPanel.Prompt && !char.IsControl(keyInfo.KeyChar))
            {
                state.InputBuffer.Append(keyInfo.KeyChar);
                state.PromptData = state.PromptData with { Text = state.InputBuffer.ToString() };
            }
            else if (state.CurrentFocus == FocusPanel.Prompt && keyInfo.Key == ConsoleKey.Backspace
                     && state.InputBuffer.Length > 0)
            {
                state.InputBuffer.Remove(state.InputBuffer.Length - 1, 1);
                state.PromptData = state.PromptData with { Text = state.InputBuffer.ToString() };
            }
            break;

        // ViewResource1-9, ToggleExpand handled similarly
    }
}
```

### Thread Safety Strategy

The design avoids locks by using:

1. **Immutable data records**: `TopPanelData`, `ActivityPanelData`, `ContextPanelData`, `PromptAreaData` are all records. The workflow thread creates a new instance; the render loop reads the latest reference. Reference assignment is atomic in .NET.
2. **`ConcurrentQueue<T>`** for event/command buses: Lock-free, thread-safe FIFO.
3. **Single-writer principle**: Each queue has one producer and one consumer. `TuiEventBus` is written by the workflow thread, read by the TUI thread. `TuiCommandBus` is the reverse.
4. **No shared mutable state**: The `TuiState` object is only mutated by the TUI thread (in `DrainInto` and `ApplyAction`). The workflow thread never reads it directly.

### Workflow Integration in the Render Loop

```csharp
public async Task RunAsync(CancellationToken ct)
{
    // ... terminal/renderer setup ...

    // Start workflow on background thread
    var workflowTask = Task.Run(() => RunWorkflowAsync(ct), ct);

    while (!ct.IsCancellationRequested && _running)
    {
        // 1. Drain workflow events into TUI state
        _eventBus.DrainInto(state);

        // 2. Advance spinner
        state.SpinnerFrame++;

        // 3. Poll input
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            var input = MapToKeyInput(keyInfo);
            var action = _keyboard.Handle(input, state.CurrentFocus);
            ApplyAction(action, state, keyInfo);
        }

        // 4. Render
        renderer.Draw((ctx, elapsed) =>
        {
            var regions = LayoutCalculator.Calculate(
                ctx.Viewport.Width, ctx.Viewport.Height,
                state.SplitPercent);
            // ... render components ...
        });

        await Task.Delay(1, ct).ConfigureAwait(false);
    }

    await workflowTask.ConfigureAwait(false);
}
```

### Modal Interaction Flow

Modals require bidirectional communication. The workflow requests a modal (e.g., confirmation for multi-file edit), and the TUI collects the user's response:

```
Workflow Thread                    TUI Thread
     │                                  │
     ├─ Post(ModalRequestEvent)  ──────►│
     │                                  ├─ state.ActiveModal = modal
     │                                  ├─ Render modal overlay
     │                                  ├─ Route input to modal
     │                                  ├─ User selects option
     │◄────── Post(ModalResponseCmd) ───┤
     ├─ Read response                   ├─ state.ActiveModal = null
     ├─ Continue workflow               │
```

For blocking modal responses, use a `TaskCompletionSource<T>`:

```csharp
public sealed record ModalRequestEvent(ModalState Modal, TaskCompletionSource<string> Response) : TuiEvent;

// Workflow thread:
var tcs = new TaskCompletionSource<string>();
_eventBus.Post(new ModalRequestEvent(modal, tcs));
var userChoice = await tcs.Task; // blocks workflow until user responds

// TUI thread (when user selects an option):
state.ActiveModal.ResponseTcs.SetResult(selectedOption);
state.ActiveModal = null;
```

### Startup Sequence

```
lopen (root command)
  │
  ├─ Parse CLI flags (--headless, --no-welcome, --resume)
  ├─ Build DI container
  │    ├─ If headless: register StubTuiApplication
  │    └─ If interactive: register TuiApplication
  ├─ Resolve ITuiApplication
  ├─ app.RunAsync(ct)
  │    ├─ Create Terminal (FullscreenMode)
  │    ├─ Create Renderer (30 FPS)
  │    ├─ Check for existing session
  │    │    ├─ If found: show SessionResumeModal
  │    │    └─ If not found && !--no-welcome: show LandingPageComponent
  │    ├─ Initialize WorkflowEngine on background thread
  │    └─ Enter main render loop
  └─ On Ctrl+C: app.StopAsync() → restore terminal
```

---

## 16. Alternative Rendering Approaches

### Alternative A: Spectre.Console Live (Without Spectre.Tui)

If Spectre.Tui proves too immature, the application shell could be built on Spectre.Console's `Live` display with a custom input thread:

```csharp
// Dedicated input thread reads keys while Live display runs
var inputThread = new Thread(() =>
{
    while (running)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            inputQueue.Enqueue(key);
        }
        Thread.Sleep(10);
    }
});
inputThread.IsBackground = true;
inputThread.Start();

await AnsiConsole.Live(layout)
    .Overflow(VerticalOverflow.Crop)
    .StartAsync(async ctx =>
    {
        while (running)
        {
            // Drain input
            while (inputQueue.TryDequeue(out var key))
                ProcessKey(key, state);

            // Update layout sections
            layout["Header"].Update(BuildHeaderPanel(state));
            layout["Activity"].Update(BuildActivityPanel(state));
            layout["Context"].Update(BuildContextPanel(state));
            layout["PromptArea"].Update(BuildPromptPanel(state));

            ctx.Refresh();
            await Task.Delay(33, ct); // ~30 FPS
        }
    });
```

**Pros**: Mature widget library (Panel, Table, Tree, Layout), rich styling, broad .NET support.

**Cons**: `Live` + input thread is fighting the library's design. `Live` is not thread-safe — all `Refresh()` calls must happen on the same thread. No alt-screen management. Modal overlays require manual composition of `IRenderable` objects over the layout. The `Layout` widget does not support rendering over/on-top-of other sections.

### Alternative B: Direct ANSI with `System.Console`

Skip frameworks entirely and write ANSI escape codes directly:

```csharp
Console.Write("\x1b[?1049h"); // Alt screen
Console.Write("\x1b[?25l");   // Hide cursor

// Render loop
Console.SetCursorPosition(x, y);
Console.Write("\x1b[32m" + text + "\x1b[0m"); // Green text

Console.Write("\x1b[?25h"); // Show cursor
Console.Write("\x1b[?1049l"); // Restore screen
```

**Pros**: Zero dependencies, full control, works everywhere.

**Cons**: Must implement double-buffering, diff, resize detection, Unicode width calculation, color system detection. Essentially rebuilding what Spectre.Tui provides.

### Recommendation

Stick with **Spectre.Tui** as recommended in Section 12. The existing `string[]` component architecture makes the framework choice less critical — if Spectre.Tui breaks or stalls, swapping the renderer (the `RenderRegion` bridge function) is a contained change. The components themselves are framework-agnostic.

---

## References

- [Spectre.Tui GitHub](https://github.com/spectreconsole/spectre.tui)
- [Spectre.Tui NuGet](https://www.nuget.org/packages/Spectre.Tui)
- [Spectre.Console Documentation](https://spectreconsole.net)
- [Spectre.Console GitHub](https://github.com/spectreconsole/spectre.console)
- [Ratatui](https://ratatui.rs/) — Rust TUI framework that inspired Spectre.Tui's architecture
- [TextMateSharp GitHub](https://github.com/danipen/TextMateSharp)
- [CSharpRepl](https://github.com/waf/CSharpRepl) — Example of Spectre.Console + REPL input handling
