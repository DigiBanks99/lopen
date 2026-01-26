# TUI Implementation Research

> Comprehensive research for implementing TUI enhancements using Spectre.Console

**Research Date**: January 25, 2026  
**Status**: ✅ Complete - Ready for Implementation  
**Target Framework**: .NET 9.0  
**Spectre.Console Version**: 0.49+

---

## Executive Summary

This document provides complete research and implementation guidance for the 8 incomplete TUI requirements from `SPECIFICATION.md`. All patterns have been researched with production-ready code examples, testing approaches, and best practices.

### Quick Navigation

- **Detailed Guide**: [`../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md`](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md) - 3,800+ lines with complete implementations
- **Quick Reference**: [`../../research/TUI_QUICK_REFERENCE.md`](../../research/TUI_QUICK_REFERENCE.md) - Daily coding companion
- **Implementation Summary**: [`../../research/TUI_IMPLEMENTATION_SUMMARY.md`](../../research/TUI_IMPLEMENTATION_SUMMARY.md) - Executive overview

---

## Requirements Coverage

| ID | Requirement | Status | Research | Implementation Time |
|----|-------------|--------|----------|---------------------|
| REQ-015 | Progress Indicators & Spinners | 🟡 Partial | ✅ Complete | 2-3 days |
| REQ-016 | Error Display & Correction | 🔴 Planned | ✅ Complete | 2-3 days |
| REQ-017 | Structured Data Display | 🟡 Partial | ✅ Complete | 3-4 days |
| REQ-018 | Layout & Right-Side Panels | 🔴 Planned | ✅ Complete | 3-4 days |
| REQ-019 | AI Response Streaming | 🔴 Planned | ✅ Complete | 3-4 days |
| REQ-020 | Responsive Terminal Detection | 🔴 Planned | ✅ Complete | 1-2 days |
| REQ-021 | TUI Testing & Mocking | 🟡 Partial | ✅ Complete | 2-3 days |
| REQ-022 | Welcome Header & Banner | 🔴 Planned | ✅ Complete | 1-2 days |

**Total Estimated Implementation**: 17-25 days (2-3 weeks)

---

## Key Questions Answered

### 1. How to implement Spectre.Console spinners for async operations?

**Answer**: Use `AnsiConsole.Status()` with async/await pattern.

```csharp
// Pattern: Spinner for indeterminate operations
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("blue"))
    .StartAsync("Connecting to Copilot...", async ctx =>
    {
        // Perform async work
        var result = await copilotClient.ConnectAsync();
        
        // Update status during operation
        ctx.Status("Processing response...");
        await ProcessResponse(result);
        
        return result;
    });
```

**Best Practices**:
- Use `Dots` spinner for most operations (calm, professional)
- Use `Arc` for heavy processing
- Always update status text for multi-step operations
- For REPL mode, consider non-blocking alternatives with `Live` displays

**Testing**:
```csharp
// Mock approach for testing
public interface IProgressRenderer
{
    Task<T> ShowProgressAsync<T>(string status, Func<Task<T>> operation);
}

// Test with mock
var mock = new MockProgressRenderer();
await handler.ExecuteAsync(mock);
Assert.Contains("Connecting", mock.StatusUpdates);
```

**Full Implementation**: See [REQ-015 in Implementation Guide](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md#req-015-progress-indicators--spinners)

---

### 2. How to create an IErrorRenderer interface?

**Answer**: Design interface-based abstraction with different error display patterns.

```csharp
// Core interface
public interface IErrorRenderer
{
    void RenderError(ErrorInfo error);
    void RenderCommandNotFound(string command, string[] suggestions);
    void RenderValidationError(string input, ValidationError error);
    void RenderSdkError(string operation, Exception exception);
}

// Error info model
public record ErrorInfo
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? DidYouMean { get; init; }
    public List<string> Suggestions { get; init; } = new();
    public string? TryCommand { get; init; }
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
}

// Implementation using Spectre.Console
public class SpectreErrorRenderer : IErrorRenderer
{
    private readonly IAnsiConsole _console;
    
    public void RenderError(ErrorInfo error)
    {
        var panel = new Panel(BuildErrorContent(error))
        {
            Header = new PanelHeader($"[red]{GetSymbol(error.Severity)} {error.Title}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Red)
        };
        
        _console.Write(panel);
    }
    
    private string BuildErrorContent(ErrorInfo error)
    {
        var content = new StringBuilder();
        content.AppendLine(error.Message);
        
        if (!string.IsNullOrEmpty(error.DidYouMean))
        {
            content.AppendLine();
            content.AppendLine($"[yellow]Did you mean:[/] [cyan]{error.DidYouMean}[/]");
        }
        
        if (error.Suggestions.Any())
        {
            content.AppendLine();
            content.AppendLine("[yellow]Suggestions:[/]");
            foreach (var suggestion in error.Suggestions)
                content.AppendLine($"  • {suggestion}");
        }
        
        if (!string.IsNullOrEmpty(error.TryCommand))
        {
            content.AppendLine();
            content.AppendLine($"[dim]💡 Try:[/] [blue]{error.TryCommand}[/]");
        }
        
        return content.ToString();
    }
}
```

**Display Examples**:

Simple error:
```
✗ Authentication failed
  💡 Try: lopen auth login
```

Complex error with suggestions:
```
╭─ Error: Invalid command ─────────────────╮
│ Command 'chatr' not found                │
│                                           │
│ Did you mean? chat                        │
│                                           │
│ Suggestions:                              │
│   • chat                                  │
│   • repl                                  │
│   • status                                │
│                                           │
│ 💡 Try: lopen --help                     │
╰───────────────────────────────────────────╯
```

**Testing**:
```csharp
[Fact]
public void RenderError_WithSuggestions_ShowsPanel()
{
    var console = new TestConsole();
    var renderer = new SpectreErrorRenderer(console);
    
    renderer.RenderError(new ErrorInfo
    {
        Title = "Command Not Found",
        Message = "Unknown command 'statr'",
        DidYouMean = "start",
        Suggestions = new() { "start", "stop", "status" }
    });
    
    Assert.Contains("Command Not Found", console.Output);
    Assert.Contains("Did you mean", console.Output);
    Assert.Contains("start", console.Output);
}
```

**Full Implementation**: See [REQ-016 in Implementation Guide](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md#req-016-error-display--correction-guidance)

---

### 3. How to implement responsive layouts with Spectre.Console?

**Answer**: Use `Layout` with `SplitColumns` and detect terminal width for adaptive rendering.

```csharp
public interface ILayoutRenderer
{
    void RenderSplitLayout(
        IRenderable mainContent,
        IRenderable? sidePanel = null,
        int minWidthForSplit = 120);
}

public class SpectreLayoutRenderer : ILayoutRenderer
{
    private readonly IAnsiConsole _console;
    private readonly ITerminalCapabilities _capabilities;
    
    public void RenderSplitLayout(
        IRenderable mainContent,
        IRenderable? sidePanel = null,
        int minWidthForSplit = 120)
    {
        // Check if terminal is wide enough for split
        if (_capabilities.Width < minWidthForSplit || sidePanel == null)
        {
            // Fallback: Full-width main content only
            _console.Write(mainContent);
            return;
        }
        
        // Create split layout
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("Main").Ratio(7),    // 70% width
                new Layout("Panel").Ratio(3)    // 30% width
            );
        
        layout["Main"].Update(mainContent);
        layout["Panel"].Update(sidePanel);
        
        _console.Write(layout);
    }
}
```

**Responsive Breakpoints**:

| Terminal Width | Layout Mode | Behavior |
|----------------|-------------|----------|
| < 60 chars | Minimal | Text only, no panels |
| 60-79 chars | Narrow | Simple panels, vertical stack |
| 80-119 chars | Standard | Full panels, single column |
| 120+ chars | Wide | Split layout with side panels |

**Usage Example**:

```csharp
// Main content
var mainPanel = new Panel(
    new Markup("[bold]Processing files...[/]")
);

// Side panel with task list
var taskPanel = new Panel(
    new Markup(
        "[green]✓[/] Step 1: Connect\n" +
        "[green]✓[/] Step 2: Authenticate\n" +
        "[yellow]⏳[/] Step 3: Processing...\n" +
        "[dim]○[/] Step 4: Finalize"
    )
)
{
    Header = new PanelHeader("Progress"),
    Border = BoxBorder.Rounded
};

// Renders split layout on wide terminals, single column on narrow
_layoutRenderer.RenderSplitLayout(mainPanel, taskPanel);
```

**Live Updating Layout**:

```csharp
var liveLayout = new Layout("Root")
    .SplitColumns(
        new Layout("Main"),
        new Layout("Tasks")
    );

await _console.Live(liveLayout)
    .StartAsync(async ctx =>
    {
        foreach (var task in tasks)
        {
            // Update main content
            liveLayout["Main"].Update(
                new Panel($"Processing: {task.Name}")
            );
            
            // Update task list
            liveLayout["Tasks"].Update(
                CreateTaskPanel(tasks)
            );
            
            await task.ExecuteAsync();
            ctx.Refresh();
        }
    });
```

**Full Implementation**: See [REQ-018 in Implementation Guide](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md#req-018-layout--right-side-panels)

---

### 4. How to buffer streaming tokens into paragraphs?

**Answer**: Use `StringBuilder` with timeout-based flushing and paragraph detection.

```csharp
public interface IStreamRenderer
{
    Task RenderStreamAsync(
        IAsyncEnumerable<string> tokenStream,
        CancellationToken cancellationToken = default);
}

public class SpectreStreamRenderer : IStreamRenderer
{
    private readonly IAnsiConsole _console;
    private const int FlushTimeoutMs = 500;
    private const int MaxTokensBeforeFlush = 100;
    
    public async Task RenderStreamAsync(
        IAsyncEnumerable<string> tokenStream,
        CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();
        var tokenCount = 0;
        var lastFlush = DateTime.UtcNow;
        
        // Show initial indicator
        _console.MarkupLine("[dim]⏳ Thinking...[/]");
        
        await foreach (var token in tokenStream.WithCancellation(cancellationToken))
        {
            buffer.Append(token);
            tokenCount++;
            
            var shouldFlush = 
                // Paragraph break detected
                token.Contains("\n\n") ||
                // Timeout reached
                (DateTime.UtcNow - lastFlush).TotalMilliseconds > FlushTimeoutMs ||
                // Too many tokens buffered
                tokenCount >= MaxTokensBeforeFlush;
            
            if (shouldFlush && buffer.Length > 0)
            {
                await FlushBufferAsync(buffer);
                buffer.Clear();
                tokenCount = 0;
                lastFlush = DateTime.UtcNow;
            }
        }
        
        // Final flush
        if (buffer.Length > 0)
        {
            await FlushBufferAsync(buffer);
        }
    }
    
    private async Task FlushBufferAsync(StringBuilder buffer)
    {
        var content = buffer.ToString();
        
        // Detect and format code blocks
        if (content.Contains("```"))
        {
            await RenderWithCodeBlocks(content);
        }
        else
        {
            // Render as formatted markdown
            _console.Write(FormatMarkdown(content));
        }
    }
    
    private Markup FormatMarkdown(string content)
    {
        // Basic markdown formatting
        var formatted = content
            .Replace("**", "[bold]", 1)
            .Replace("**", "[/]", 1)
            .Replace("`", "[cyan]", 1)
            .Replace("`", "[/]", 1);
        
        return new Markup(Markup.Escape(content));
    }
    
    private async Task RenderWithCodeBlocks(string content)
    {
        // Split on code block markers
        var parts = content.Split("```");
        
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 0)
            {
                // Regular text
                _console.Write(FormatMarkdown(parts[i]));
            }
            else
            {
                // Code block
                var lines = parts[i].Split('\n', 2);
                var language = lines[0].Trim();
                var code = lines.Length > 1 ? lines[1] : "";
                
                var panel = new Panel(Markup.Escape(code))
                {
                    Header = new PanelHeader(language),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Grey)
                };
                
                _console.Write(panel);
            }
        }
    }
}
```

**Key Strategies**:

1. **Buffering**: Accumulate tokens in StringBuilder
2. **Flush Triggers**: 
   - Paragraph break (`\n\n`)
   - Timeout (500ms default)
   - Token limit (100 tokens)
3. **Code Block Handling**: Wait for complete block before rendering
4. **Progress Indicator**: Show "Thinking..." until first chunk

**Testing**:

```csharp
[Fact]
public async Task RenderStreamAsync_BuffersParagraphs()
{
    var console = new TestConsole();
    var renderer = new SpectreStreamRenderer(console);
    
    async IAsyncEnumerable<string> GetTokens()
    {
        yield return "Hello";
        await Task.Delay(10);
        yield return " world";
        await Task.Delay(10);
        yield return "\n\n";  // Paragraph break triggers flush
        yield return "Next paragraph";
    }
    
    await renderer.RenderStreamAsync(GetTokens());
    
    // Should have flushed on paragraph break
    Assert.Contains("Hello world", console.Output);
}
```

**Full Implementation**: See [REQ-019 in Implementation Guide](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md#req-019-ai-response-streaming)

---

### 5. How to detect terminal capabilities?

**Answer**: Use `AnsiConsole.Profile` combined with environment variable checks.

```csharp
public interface ITerminalCapabilities
{
    int Width { get; }
    int Height { get; }
    ColorSystem ColorSystem { get; }
    bool SupportsUnicode { get; }
    bool IsInteractive { get; }
    bool IsNoColorSet { get; }
}

public class TerminalCapabilities : ITerminalCapabilities
{
    public int Width { get; }
    public int Height { get; }
    public ColorSystem ColorSystem { get; }
    public bool SupportsUnicode { get; }
    public bool IsInteractive { get; }
    public bool IsNoColorSet { get; }
    
    private TerminalCapabilities(
        int width,
        int height,
        ColorSystem colorSystem,
        bool supportsUnicode,
        bool isInteractive,
        bool isNoColorSet)
    {
        Width = width;
        Height = height;
        ColorSystem = colorSystem;
        SupportsUnicode = supportsUnicode;
        IsInteractive = isInteractive;
        IsNoColorSet = isNoColorSet;
    }
    
    public static ITerminalCapabilities Detect()
    {
        // Priority 1: Check NO_COLOR environment variable
        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var isNoColorSet = !string.IsNullOrEmpty(noColor);
        
        // Priority 2: Detect console capabilities
        int width, height;
        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
        }
        catch
        {
            // Fallback for non-interactive or piped output
            width = 80;
            height = 24;
        }
        
        // Priority 3: Use Spectre.Console detection
        var profile = AnsiConsole.Profile;
        var colorSystem = isNoColorSet 
            ? ColorSystem.NoColors 
            : profile.Capabilities.ColorSystem;
        
        var supportsUnicode = profile.Capabilities.Unicode;
        var isInteractive = !Console.IsInputRedirected && 
                           !Console.IsOutputRedirected;
        
        return new TerminalCapabilities(
            width,
            height,
            colorSystem,
            supportsUnicode,
            isInteractive,
            isNoColorSet
        );
    }
    
    // Helper methods for common checks
    public bool SupportsColor => ColorSystem != ColorSystem.NoColors;
    public bool SupportsAnsi => ColorSystem != ColorSystem.NoColors;
    public bool IsWideTerminal => Width >= 120;
    public bool IsNarrowTerminal => Width < 60;
}
```

**Capability Matrix**:

| Property | Detection Source | Fallback |
|----------|------------------|----------|
| Width | `Console.WindowWidth` | 80 |
| Height | `Console.WindowHeight` | 24 |
| ColorSystem | `AnsiConsole.Profile.Capabilities.ColorSystem` | NoColors |
| SupportsUnicode | `AnsiConsole.Profile.Capabilities.Unicode` | false |
| IsInteractive | `!Console.IsInputRedirected` | false |
| IsNoColorSet | `NO_COLOR` env var | false |

**Adaptive Rendering Pattern**:

```csharp
public class AdaptiveRenderer
{
    private readonly ITerminalCapabilities _capabilities;
    
    public void Render(string title, string content)
    {
        if (_capabilities.IsNoColorSet)
        {
            // Plain text
            Console.WriteLine($"=== {title} ===");
            Console.WriteLine(content);
        }
        else if (_capabilities.Width >= 100)
        {
            // Full panel with colors
            var panel = new Panel(content)
            {
                Header = new PanelHeader(title),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Blue)
            };
            AnsiConsole.Write(panel);
        }
        else if (_capabilities.Width >= 60)
        {
            // Compact panel
            var panel = new Panel(content)
            {
                Header = new PanelHeader(title),
                Border = BoxBorder.Square
            };
            AnsiConsole.Write(panel);
        }
        else
        {
            // Minimal
            Console.WriteLine(title);
            Console.WriteLine(content);
        }
    }
}
```

**Testing**:

```csharp
[Theory]
[InlineData(120, true, true)]  // Wide, color
[InlineData(60, true, false)]  // Narrow, color
[InlineData(120, false, false)] // Wide, no color
public void Detect_ReturnsCorrectCapabilities(
    int width, 
    bool expectColor, 
    bool expectWide)
{
    // Set up test console
    var console = new TestConsole();
    console.Profile.Width = width;
    
    var capabilities = TerminalCapabilities.Detect();
    
    Assert.Equal(expectColor, capabilities.SupportsColor);
    Assert.Equal(expectWide, capabilities.IsWideTerminal);
}
```

**Full Implementation**: See [REQ-020 in Implementation Guide](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md#req-020-responsive-terminal-detection)

---

### 6. How to create the Wind Runner ASCII art logo?

**Answer**: Use `FigletText` for branded text and custom ASCII art for symbols.

```csharp
public interface IWelcomeRenderer
{
    void RenderWelcomeHeader(WelcomeConfig config);
}

public record WelcomeConfig
{
    public string Version { get; init; } = "1.0.0";
    public string SessionName { get; init; } = "default";
    public string? ContextInfo { get; init; }
    public bool ShowTip { get; init; } = true;
}

public class WelcomeRenderer : IWelcomeRenderer
{
    private readonly IAnsiConsole _console;
    private readonly ITerminalCapabilities _capabilities;
    
    public void RenderWelcomeHeader(WelcomeConfig config)
    {
        if (_capabilities.Width >= 100)
        {
            RenderFullHeader(config);
        }
        else if (_capabilities.Width >= 60)
        {
            RenderCompactHeader(config);
        }
        else
        {
            RenderMinimalHeader(config);
        }
        
        _console.WriteLine();
    }
    
    private void RenderFullHeader(WelcomeConfig config)
    {
        var grid = new Grid();
        grid.AddColumn();
        
        // ASCII art logo
        grid.AddRow(CreateLogoPanel());
        
        // Version and tagline using FigletText
        var figlet = new FigletText("LOPEN")
            .Centered()
            .Color(Color.Cyan1);
        grid.AddRow(figlet);
        
        grid.AddRow(new Markup(
            $"[dim]v{config.Version} - Interactive Copilot Agent Loop[/]"
        ).Centered());
        
        // Session info
        var sessionInfo = new Panel(CreateSessionInfo(config))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };
        grid.AddRow(sessionInfo);
        
        _console.Write(new Panel(grid)
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue)
        });
    }
    
    private Panel CreateLogoPanel()
    {
        var logo = """
                    ⚡ Wind Runner Sigil ⚡
                    
                        ▄▄▄▄▄▄▄▄▄
                     ▄▀▀         ▀▀▄
                   ▄▀   ▄▄▄▄▄▄▄    ▀▄
                  █   ▄▀▀     ▀▀▄   █
                 █   █  ⚡ W ⚡  █   █
                 █    ▀▄▄     ▄▄▀    █
                  ▀▄    ▀▀▀▀▀▀▀    ▄▀
                    ▀▄▄         ▄▄▀
                       ▀▀▀▀▀▀▀▀▀
            """;
        
        return new Panel(new Markup(logo).Centered())
        {
            Border = BoxBorder.None
        };
    }
    
    private string CreateSessionInfo(WelcomeConfig config)
    {
        var info = new StringBuilder();
        
        if (config.ShowTip)
        {
            info.AppendLine("[yellow]💡 Tip:[/] Type [cyan]help[/] or [cyan]lopen --help[/] for commands");
            info.AppendLine();
        }
        
        info.Append($"[dim]📊 Session:[/] {config.SessionName}");
        
        if (!string.IsNullOrEmpty(config.ContextInfo))
        {
            info.Append($"  [dim]|[/]  [dim]Context:[/] {config.ContextInfo}");
        }
        
        info.Append("  [green]🟢[/]");
        
        return info.ToString();
    }
    
    private void RenderCompactHeader(WelcomeConfig config)
    {
        _console.Write(new FigletText("LOPEN")
            .LeftJustified()
            .Color(Color.Cyan1));
        
        _console.MarkupLine($"[dim]v{config.Version} - Interactive Agent Loop[/]");
        _console.WriteLine();
        
        if (config.ShowTip)
            _console.MarkupLine("[yellow]💡[/] Type [cyan]help[/] for commands");
        
        _console.MarkupLine($"[dim]Session:[/] {config.SessionName}");
        
        if (!string.IsNullOrEmpty(config.ContextInfo))
            _console.MarkupLine($"[dim]Context:[/] {config.ContextInfo}");
    }
    
    private void RenderMinimalHeader(WelcomeConfig config)
    {
        _console.WriteLine($"lopen v{config.Version}");
        _console.WriteLine($"Session: {config.SessionName}");
        
        if (!string.IsNullOrEmpty(config.ContextInfo))
            _console.WriteLine($"Context: {config.ContextInfo}");
        
        if (config.ShowTip)
            _console.WriteLine("Type 'help' for commands");
    }
}
```

**Wind Runner Sigil ASCII Art Design Options**:

Option 1 - Simple Symbol:
```
    ⚡
  ⚡ W ⚡
    ⚡
```

Option 2 - Bordered Symbol:
```
    ▄▄▄▄▄
  ▄▀  ⚡  ▀▄
 █  ⚡ W ⚡  █
  ▀▄  ⚡  ▄▀
    ▀▀▀▀▀
```

Option 3 - Detailed Sigil:
```
      ▄▄▄▄▄▄▄▄▄
   ▄▀▀         ▀▀▄
 ▄▀   ▄▄▄▄▄▄▄    ▀▄
█   ▄▀▀     ▀▀▄   █
█   █  ⚡ W ⚡  █   █
█    ▀▄▄     ▄▄▀    █
 ▀▄    ▀▀▀▀▀▀▀    ▄▀
   ▀▄▄         ▄▄▀
      ▀▀▀▀▀▀▀▀▀
```

**Usage**:

```csharp
// At REPL startup
var welcomeRenderer = new WelcomeRenderer(
    AnsiConsole.Console,
    capabilities
);

welcomeRenderer.RenderWelcomeHeader(new WelcomeConfig
{
    Version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "1.0.0",
    SessionName = sessionName,
    ContextInfo = "2.4K/128K tokens",
    ShowTip = !quietMode
});
```

**Testing**:

```csharp
[Fact]
public void RenderWelcomeHeader_FullWidth_ShowsLogo()
{
    var console = new TestConsole();
    console.Profile.Width = 120;
    var renderer = new WelcomeRenderer(console, capabilities);
    
    renderer.RenderWelcomeHeader(new WelcomeConfig
    {
        Version = "1.0.0-test",
        SessionName = "test-session"
    });
    
    Assert.Contains("LOPEN", console.Output);
    Assert.Contains("Wind Runner", console.Output);
    Assert.Contains("1.0.0-test", console.Output);
}
```

**Full Implementation**: See [REQ-022 in Implementation Guide](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md#req-022-welcome-header-with-ascii-art)

---

## Architecture & Design Patterns

### Interface-Based Architecture

All TUI functionality is abstracted behind interfaces for testability:

```csharp
// Core interfaces
public interface ITuiRenderer { }
public interface IProgressRenderer { }
public interface IErrorRenderer { }
public interface IDataRenderer { }
public interface ILayoutRenderer { }
public interface IStreamRenderer { }
public interface IWelcomeRenderer { }

// Real implementations use Spectre.Console
public class SpectreTuiRenderer : ITuiRenderer { }
public class SpectreProgressRenderer : IProgressRenderer { }
// ... etc

// Test implementations use mocks
public class MockTuiRenderer : ITuiRenderer { }
public class MockProgressRenderer : IProgressRenderer { }
// ... etc
```

**Benefits**:
- ✅ Testability with mocks
- ✅ Dependency injection friendly
- ✅ Easy to swap implementations
- ✅ Clear contracts for consumers

---

### Progressive Enhancement Strategy

Rendering adapts to terminal capabilities:

```
┌─────────────────────────────────────────────────────────────┐
│ Terminal Capabilities → Feature Availability                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ TrueColor (24-bit RGB)                                      │
│   ↓ Full rich TUI with gradients, brand colors            │
│                                                             │
│ 256-color (8-bit)                                           │
│   ↓ Standard TUI with extended color palette               │
│                                                             │
│ 16-color (Standard ANSI)                                    │
│   ↓ Basic TUI with standard colors                         │
│                                                             │
│ NO_COLOR / None                                             │
│   ↓ Plain text fallback, ASCII borders                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Implementation**:

```csharp
public void Render(IRenderable content)
{
    if (_capabilities.IsNoColorSet)
    {
        RenderPlainText(content);
    }
    else if (_capabilities.ColorSystem == ColorSystem.TrueColor)
    {
        RenderWithRichColors(content);
    }
    else if (_capabilities.ColorSystem == ColorSystem.EightBit)
    {
        RenderWithExtendedColors(content);
    }
    else
    {
        RenderWithBasicColors(content);
    }
}
```

---

### Responsive Design Breakpoints

Layout adapts to terminal width:

| Width Range | Layout Mode | Components Enabled |
|-------------|-------------|-------------------|
| < 60 chars | **Minimal** | Text only, no panels or tables |
| 60-79 chars | **Narrow** | Simple panels, vertical stacking |
| 80-119 chars | **Standard** | Full panels, single column layout |
| ≥ 120 chars | **Wide** | Split layouts, side panels, full features |

**Implementation**:

```csharp
public void RenderContent(IRenderable main, IRenderable? side = null)
{
    switch (_capabilities.Width)
    {
        case < 60:
            RenderMinimal(main);
            break;
        case < 80:
            RenderNarrow(main);
            break;
        case < 120:
            RenderStandard(main);
            break;
        default:
            RenderWide(main, side);
            break;
    }
}
```

---

## Testing Approaches

### 1. Unit Testing with Mocks

```csharp
public class MockTuiRenderer : ITuiRenderer
{
    public List<string> WrittenText { get; } = new();
    public List<Type> WrittenTypes { get; } = new();
    
    public void Write(IRenderable renderable)
    {
        WrittenTypes.Add(renderable.GetType());
    }
    
    public void WriteLine(string text)
    {
        WrittenText.Add(text);
    }
}

// Test usage
[Fact]
public async Task ChatCommand_ShowsProgress()
{
    var mockRenderer = new MockTuiRenderer();
    var handler = new ChatCommandHandler(mockRenderer);
    
    await handler.ExecuteAsync("Hello");
    
    Assert.Contains(mockRenderer.WrittenText, 
        t => t.Contains("Connecting"));
}
```

### 2. Integration Testing with TestConsole

```csharp
[Fact]
public void ErrorRenderer_ShowsPanel()
{
    var console = new TestConsole();
    console.Profile.Width = 120;
    var renderer = new SpectreErrorRenderer(console);
    
    renderer.RenderError(new ErrorInfo
    {
        Title = "Test Error",
        Message = "Something went wrong"
    });
    
    var output = console.Output;
    Assert.Contains("Test Error", output);
    Assert.Contains("╭", output); // Panel border
}
```

### 3. Snapshot Testing for Visual Regression

```csharp
[Fact]
public void WelcomeHeader_MatchesSnapshot()
{
    var console = new TestConsole();
    console.Profile.Width = 120;
    var renderer = new WelcomeRenderer(console, capabilities);
    
    renderer.RenderWelcomeHeader(new WelcomeConfig
    {
        Version = "1.0.0",
        SessionName = "test-session"
    });
    
    var output = console.Output;
    
    // Compare with saved snapshot
    Snapshot.Match(output, "welcome-header-full");
}
```

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1)

**Focus**: Core interfaces, terminal detection, basic rendering

- [ ] Create all interfaces in `src/Lopen.Core/Tui/Interfaces/`
  - `ITuiRenderer.cs`
  - `IProgressRenderer.cs`
  - `IErrorRenderer.cs`
  - `IDataRenderer.cs`
  - `ILayoutRenderer.cs`
  - `IStreamRenderer.cs`
  - `IWelcomeRenderer.cs`
  - `ITerminalCapabilities.cs`

- [ ] Implement `TerminalCapabilities` (REQ-020)
  - Width/height detection
  - Color system detection
  - NO_COLOR handling
  - Interactive mode detection

- [ ] Set up dependency injection
  - Register interfaces in DI container
  - Configure lifetime scopes
  - Add to service collection

- [ ] Implement `SpectreProgressRenderer` (REQ-015)
  - Status with spinner
  - Progress bars
  - Non-blocking REPL mode

- [ ] Implement `SpectreErrorRenderer` (REQ-016)
  - Simple errors
  - Panel errors
  - Validation errors
  - Command suggestions

- [ ] Create `MockTuiRenderer` for testing (REQ-021)
  - Record all render calls
  - Queryable history
  - Assertion helpers

**Deliverables**:
- ✅ All interfaces defined
- ✅ Terminal capabilities working
- ✅ Progress indicators functional
- ✅ Error rendering complete
- ✅ Basic test infrastructure

---

### Phase 2: Core Features (Week 2)

**Focus**: Data display, layouts, structured content

- [ ] Implement `SpectreDataRenderer` (REQ-017)
  - Panel rendering for metadata
  - Tree rendering for hierarchies
  - Enhanced table rendering
  - Responsive column widths
  - Component selection logic

- [ ] Implement `SpectreLayoutRenderer` (REQ-018)
  - Split-screen layout with `SplitColumns`
  - Live layout updates
  - `TaskListPanel` component
  - `ContextPanel` component
  - Responsive layout switching

- [ ] Add responsive behavior
  - Width-based component selection
  - Fallback rendering for narrow terminals
  - Adaptive table columns
  - Panel nesting limits

- [ ] Integration testing
  - Test all components with `TestConsole`
  - Verify ANSI output
  - Test responsive breakpoints
  - NO_COLOR validation

**Deliverables**:
- ✅ All data display components working
- ✅ Split layouts functional
- ✅ Responsive behavior implemented
- ✅ Integration tests passing

---

### Phase 3: Advanced & Polish (Week 3)

**Focus**: Streaming, welcome header, comprehensive testing

- [ ] Implement `SpectreStreamRenderer` (REQ-019)
  - Token buffering logic
  - Paragraph detection
  - Timeout-based flushing
  - Code block handling
  - Markdown formatting

- [ ] Implement `WelcomeRenderer` (REQ-022)
  - ASCII art Wind Runner logo
  - FigletText branding
  - Version display from assembly
  - Session info panel
  - Responsive header layouts

- [ ] Comprehensive testing
  - Complete unit test coverage
  - Integration tests for all components
  - Snapshot tests for visual regression
  - Performance testing
  - Edge case validation

- [ ] Documentation
  - XML documentation comments
  - Usage examples
  - README for TUI module
  - Architecture decision records

- [ ] Polish & refinement
  - Code review feedback
  - Performance optimization
  - Error handling edge cases
  - Accessibility improvements

**Deliverables**:
- ✅ Streaming renderer complete
- ✅ Welcome header polished
- ✅ Full test coverage (90%+)
- ✅ Documentation complete
- ✅ Ready for production

---

## Performance Considerations

### 1. Output Buffering

**Problem**: Excessive `AnsiConsole.Write()` calls cause flicker and performance issues.

**Solution**: Buffer output and write in batches.

```csharp
// BAD: Multiple writes
foreach (var item in items)
{
    AnsiConsole.WriteLine(item.ToString());
}

// GOOD: Buffer and write once
var buffer = new StringBuilder();
foreach (var item in items)
{
    buffer.AppendLine(item.ToString());
}
AnsiConsole.Write(buffer.ToString());
```

### 2. Live Displays

**Problem**: Frequently updating static content causes redrawing overhead.

**Solution**: Use `Live` displays for dynamic content.

```csharp
// For content that updates frequently
await AnsiConsole.Live(initialContent)
    .StartAsync(async ctx =>
    {
        while (!done)
        {
            content = UpdateContent();
            ctx.UpdateTarget(content);
            ctx.Refresh();
            await Task.Delay(100);
        }
    });
```

### 3. Terminal Capability Caching

**Problem**: Repeated capability detection is wasteful.

**Solution**: Detect once at startup, cache for session.

```csharp
// Singleton or scoped service
services.AddSingleton<ITerminalCapabilities>(
    sp => TerminalCapabilities.Detect()
);
```

### 4. Conditional Rendering

**Problem**: Rendering complex layouts on narrow terminals wastes CPU.

**Solution**: Early return for unsupported layouts.

```csharp
public void RenderSplitLayout(IRenderable main, IRenderable side)
{
    if (_capabilities.Width < 120)
    {
        // Early return - just render main
        _console.Write(main);
        return;
    }
    
    // Complex split layout only for wide terminals
    var layout = new Layout("Root").SplitColumns(...);
    // ...
}
```

---

## Best Practices Summary

### ✅ Do's

1. **Use interfaces for all TUI operations** - Enables testing and DI
2. **Detect capabilities once at startup** - Cache for performance
3. **Buffer output for batch operations** - Reduce flicker
4. **Provide fallbacks for limited terminals** - Accessibility
5. **Test with TestConsole** - Verify ANSI output
6. **Respect NO_COLOR** - Environment variable standard
7. **Use Live displays for frequently updating content** - Performance
8. **Update status during long operations** - User feedback
9. **Flush streaming buffers on paragraph breaks** - Readability
10. **Document terminal width requirements** - User expectations

### ❌ Don'ts

1. **Don't call AnsiConsole.Write() in tight loops** - Buffer instead
2. **Don't assume terminal supports color** - Check capabilities
3. **Don't hardcode widths** - Use responsive breakpoints
4. **Don't nest panels more than 2 levels deep** - Visual clutter
5. **Don't ignore NO_COLOR** - Accessibility requirement
6. **Don't show progress for fast operations (< 1s)** - Flicker
7. **Don't use bright colors for borders** - Visual fatigue
8. **Don't stream char-by-char** - Buffer into chunks
9. **Don't render complex layouts on narrow terminals** - Poor UX
10. **Don't skip testing with mocks** - Slow test suite

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Spectre.Console | ^0.49.0 | Core TUI framework |
| Spectre.Console.Testing | ^0.49.0 | Test infrastructure |
| System.CommandLine | ^2.0.2 | CLI integration |

**Installation**:

```bash
dotnet add package Spectre.Console
dotnet add package Spectre.Console.Testing
dotnet add package System.CommandLine
```

---

## References

### Official Documentation

- **Spectre.Console Documentation**: https://spectreconsole.net/
- **API Reference**: https://spectreconsole.net/api/
- **Live Examples**: https://spectreconsole.net/live/

### Key Spectre.Console Components

| Component | Documentation | Use Case |
|-----------|---------------|----------|
| `Status` | https://spectreconsole.net/widgets/status | Progress spinners |
| `Progress` | https://spectreconsole.net/widgets/progress | Progress bars |
| `Panel` | https://spectreconsole.net/widgets/panel | Bordered content |
| `Table` | https://spectreconsole.net/widgets/table | Tabular data |
| `Tree` | https://spectreconsole.net/widgets/tree | Hierarchies |
| `Layout` | https://spectreconsole.net/widgets/layout | Split layouts |
| `Live` | https://spectreconsole.net/live-display | Dynamic updates |
| `Markup` | https://spectreconsole.net/markup | Styled text |
| `FigletText` | https://spectreconsole.net/widgets/figlet | ASCII art text |

### Testing Resources

- **TestConsole Guide**: https://spectreconsole.net/testing/test-console
- **Snapshot Testing**: https://github.com/spectreconsole/spectre.console/tree/main/examples/Testing

### Community Examples

- **Spectre.Console GitHub**: https://github.com/spectreconsole/spectre.console
- **Example Gallery**: https://github.com/spectreconsole/spectre.console/tree/main/examples

---

## Next Steps

1. **Review Implementation Guide**: Read [`SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md`](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md) for complete code examples

2. **Start with Foundation**: Begin Phase 1 implementation
   - Create interfaces
   - Implement terminal detection
   - Set up basic progress and error rendering

3. **Iterate and Test**: Build incrementally with tests
   - Add one renderer at a time
   - Write tests immediately
   - Verify with TestConsole

4. **Integrate with Commands**: Wire up to existing command handlers
   - Replace `Console.WriteLine` with `ITuiRenderer`
   - Add progress indicators to async operations
   - Use error renderer for exceptions

5. **Polish and Document**: Final refinements
   - Add XML documentation
   - Create usage examples
   - Performance tuning

---

## Questions or Issues?

- **Review Full Guide**: [`SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md`](../../research/SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md)
- **Quick Reference**: [`TUI_QUICK_REFERENCE.md`](../../research/TUI_QUICK_REFERENCE.md)
- **Implementation Summary**: [`TUI_IMPLEMENTATION_SUMMARY.md`](../../research/TUI_IMPLEMENTATION_SUMMARY.md)

**Research Status**: ✅ Complete and ready for implementation

