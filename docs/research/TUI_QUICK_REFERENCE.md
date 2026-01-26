# TUI Implementation Quick Reference

> Quick lookup guide for Spectre.Console TUI patterns in Lopen

## Quick Links

- **Full Guide**: [SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md](./SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md)
- **TUI Specification**: [../requirements/tui/SPECIFICATION.md](../requirements/tui/SPECIFICATION.md)

---

## Interface Summary

### Core Interfaces

```csharp
// Base rendering
public interface ITuiRenderer
{
    void Write(IRenderable renderable);
    void WriteLine(string text);
    void WriteMarkup(string markup);
}

// Progress indication
public interface IProgressRenderer
{
    Task<T> ShowProgressAsync<T>(string status, Func<IProgressContext, Task<T>> operation);
}

// Error display
public interface IErrorRenderer
{
    void RenderError(ErrorInfo error);
    void RenderCommandNotFound(string command, string[] suggestions);
}

// Data display
public interface IDataRenderer
{
    void RenderMetadata(Dictionary<string, string> data, string title);
    void RenderHierarchy(TreeNode root, string title);
    void RenderTable<T>(IEnumerable<T> items, TableConfig<T> config);
}

// Layout
public interface ILayoutRenderer
{
    void RenderSplitLayout(IRenderable left, IRenderable right, int minWidthForSplit = 120);
    ILiveLayout CreateLiveLayout();
}

// Streaming
public interface IStreamRenderer
{
    Task RenderStreamAsync(IAsyncEnumerable<string> stream, CancellationToken ct);
}

// Terminal capabilities
public interface ITerminalCapabilities
{
    bool SupportsColor { get; }
    bool SupportsAnsi { get; }
    int Width { get; }
    ColorSystem ColorSystem { get; }
    bool IsNoColorSet { get; }
}
```

---

## Common Patterns

### Pattern: Progress with Spinner

```csharp
await _progress.ShowProgressAsync(
    "Calling GitHub Copilot...",
    async ctx =>
    {
        ctx.UpdateStatus("Sending request...");
        var result = await CopilotClient.SendAsync(request);
        
        ctx.UpdateStatus("Processing response...");
        return ProcessResponse(result);
    }
);
```

### Pattern: Error with Suggestions

```csharp
_errorRenderer.RenderError(new ErrorInfo
{
    Title = "Command Not Found",
    Message = "The command 'statr' is not recognized.",
    DidYouMean = "start",
    Suggestions = new() { "status", "stop" }
});
```

### Pattern: Metadata Panel

```csharp
_dataRenderer.RenderMetadata(
    new Dictionary<string, string>
    {
        ["Status"] = "Running",
        ["Iteration"] = "3/10",
        ["Model"] = "gpt-4"
    },
    "Loop Status"
);
```

### Pattern: Split Layout

```csharp
var mainContent = new Panel("Main area...");
var sidePanel = TaskListPanel.Create(tasks);
_layoutRenderer.RenderSplitLayout(mainContent, sidePanel);
```

### Pattern: Adaptive Rendering

```csharp
if (_capabilities.Width >= 120)
    RenderFullLayout();
else if (_capabilities.Width >= 80)
    RenderCompactLayout();
else
    RenderMinimalLayout();
```

### Pattern: Streaming AI Response

```csharp
await _streamRenderer.RenderStreamAsync(
    copilotStream,
    cancellationToken
);
```

---

## Spinner Types

| Type | Use Case | Visual |
|------|----------|--------|
| `Dots` | Network calls | Calm, steady |
| `Arc` | Processing | Active |
| `Star` | Building | Energetic |
| `Line` | File I/O | Sequential |

```csharp
var style = new SpinnerStyle
{
    Spinner = Spinner.Known.Dots,
    Style = Style.Parse("blue")
};
```

---

## Terminal Width Breakpoints

| Width | Layout Mode | Components |
|-------|-------------|------------|
| < 60 | Minimal | Text only |
| 60-79 | Narrow | Simple panels |
| 80-119 | Standard | Panels + tables |
| 120+ | Wide | Split layouts |

---

## Testing

### Unit Test with Mock

```csharp
var renderer = new MockTuiRenderer();
component.Render(renderer);

Assert.True(renderer.ContainsText("Expected text"));
Assert.True(renderer.ContainsRenderable<Panel>());
```

### Integration Test with TestConsole

```csharp
var console = new TestConsole();
console.Profile.Width = 120;
var renderer = new SpectreTuiRenderer(console);

component.Render(renderer);

Assert.Contains("Expected output", console.Output);
```

---

## Color System

```csharp
// Adaptive colors
var color = _capabilities.ColorSystem switch
{
    ColorSystem.TrueColor => new Color(0, 153, 255),  // RGB
    ColorSystem.EightBit => Color.Blue,                // 256-color
    ColorSystem.Standard => Color.Blue,                // 16-color
    _ => Color.Default
};
```

---

## Best Practices Checklist

### ✅ DO:
- Use interfaces for all TUI components
- Test with `TestConsole` and mocks
- Respect NO_COLOR environment variable
- Provide responsive layouts
- Escape user input with `Markup.Escape()`
- Use appropriate spinner types
- Implement graceful degradation

### ❌ DON'T:
- Don't assume terminal supports colors
- Don't hardcode ANSI sequences
- Don't ignore terminal width
- Don't forget to dispose live displays
- Don't update too frequently (< 100ms)
- Don't use panels on terminals < 60 chars

---

## Dependency Injection Setup

```csharp
services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
services.AddSingleton<ITerminalCapabilities, TerminalCapabilities>();
services.AddSingleton<ITuiRenderer, SpectreTuiRenderer>();
services.AddSingleton<IProgressRenderer, SpectreProgressRenderer>();
services.AddSingleton<IErrorRenderer, SpectreErrorRenderer>();
services.AddSingleton<IDataRenderer, SpectreDataRenderer>();
services.AddSingleton<ILayoutRenderer, SpectreLayoutRenderer>();
services.AddSingleton<IStreamRenderer, SpectreStreamRenderer>();
services.AddSingleton<IWelcomeRenderer, WelcomeRenderer>();
```

---

## Key Classes Reference

### ErrorInfo
```csharp
public record ErrorInfo
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
    public List<string> Suggestions { get; init; } = new();
    public string? DidYouMean { get; init; }
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
}
```

### SpinnerStyle
```csharp
public class SpinnerStyle
{
    public Spinner Spinner { get; set; } = Spinner.Known.Dots;
    public Style Style { get; set; } = Style.Parse("blue");
}
```

### TableConfig
```csharp
public class TableConfig<T>
{
    public required string Title { get; init; }
    public List<TableColumn<T>> Columns { get; init; } = new();
    public bool Expand { get; init; } = true;
}
```

### StreamingConfig
```csharp
public class StreamingConfig
{
    public int FlushIntervalMs { get; set; } = 100;
    public int BufferSizeChars { get; set; } = 50;
    public bool EnableMarkdown { get; set; } = true;
}
```

---

## Troubleshooting

| Issue | Likely Cause | Solution |
|-------|--------------|----------|
| No colors | NO_COLOR set | Check environment variable |
| Broken layout | Terminal too narrow | Implement responsive breakpoints |
| Static spinner | Not interactive | Check `SupportsInteractive` |
| Wrong characters | Encoding issue | Ensure UTF-8 terminal |

---

## External Resources

- [Spectre.Console Docs](https://spectreconsole.net/)
- [GitHub Repo](https://github.com/spectreconsole/spectre.console)
- [Testing Package](https://www.nuget.org/packages/Spectre.Console.Testing/)

---

**See full implementation guide for complete code examples and detailed explanations.**
