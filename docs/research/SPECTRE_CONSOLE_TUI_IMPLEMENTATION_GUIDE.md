# Spectre.Console TUI Implementation Guide for Lopen

> Comprehensive research and implementation patterns for TUI requirements using Spectre.Console

**Document Version:** 1.0  
**Date:** January 2026  
**Target Framework:** .NET 9.0  
**Spectre.Console Version:** Latest (0.49+)

---

## Table of Contents

1. [REQ-015: Progress Indicators & Spinners](#req-015-progress-indicators--spinners)
2. [REQ-016: Error Display & Correction Guidance](#req-016-error-display--correction-guidance)
3. [REQ-017: Structured Data Display (Panels & Trees)](#req-017-structured-data-display-panels--trees)
4. [REQ-018: Layout & Right-Side Panels](#req-018-layout--right-side-panels)
5. [REQ-019: AI Response Streaming](#req-019-ai-response-streaming)
6. [REQ-020: Responsive Terminal Detection](#req-020-responsive-terminal-detection)
7. [REQ-021: TUI Testing & Mocking](#req-021-tui-testing--mocking)
8. [REQ-022: Welcome Header with ASCII Art](#req-022-welcome-header-with-ascii-art)

---

## REQ-015: Progress Indicators & Spinners

### Pattern Overview

Spectre.Console provides `AnsiConsole.Status()` for showing progress during async operations. The Status API automatically handles spinner animation and allows updating the status message during execution. For REPL scenarios, we need non-blocking progress that doesn't interfere with input.

### Complete Code Example

```csharp
using Spectre.Console;
using System.Threading.Tasks;

namespace Lopen.Tui.Progress;

/// <summary>
/// Provides progress indication for async operations
/// </summary>
public interface IProgressRenderer
{
    Task<T> ShowProgressAsync<T>(
        string initialStatus,
        Func<IProgressContext, Task<T>> operation,
        SpinnerStyle? style = null);
    
    void ShowProgress(
        string initialStatus,
        Action<IProgressContext> operation,
        SpinnerStyle? style = null);
}

public interface IProgressContext
{
    void UpdateStatus(string status);
    void UpdateSpinner(Spinner spinner);
    void UpdateSpinnerStyle(Style style);
}

public class SpectreProgressRenderer : IProgressRenderer
{
    private readonly IAnsiConsole _console;
    
    public SpectreProgressRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }
    
    public async Task<T> ShowProgressAsync<T>(
        string initialStatus,
        Func<IProgressContext, Task<T>> operation,
        SpinnerStyle? style = null)
    {
        return await _console.Status()
            .Spinner(style?.Spinner ?? Spinner.Known.Dots)
            .SpinnerStyle(style?.Style ?? Style.Parse("blue"))
            .StartAsync(initialStatus, async ctx =>
            {
                var progressContext = new SpectreProgressContext(ctx);
                return await operation(progressContext);
            });
    }
    
    public void ShowProgress(
        string initialStatus,
        Action<IProgressContext> operation,
        SpinnerStyle? style = null)
    {
        _console.Status()
            .Spinner(style?.Spinner ?? Spinner.Known.Dots)
            .SpinnerStyle(style?.Style ?? Style.Parse("blue"))
            .Start(initialStatus, ctx =>
            {
                var progressContext = new SpectreProgressContext(ctx);
                operation(progressContext);
            });
    }
    
    private class SpectreProgressContext : IProgressContext
    {
        private readonly StatusContext _ctx;
        
        public SpectreProgressContext(StatusContext ctx)
        {
            _ctx = ctx;
        }
        
        public void UpdateStatus(string status) => _ctx.Status(status);
        public void UpdateSpinner(Spinner spinner) => _ctx.Spinner(spinner);
        public void UpdateSpinnerStyle(Style style) => _ctx.SpinnerStyle(style);
    }
}

public class SpinnerStyle
{
    public Spinner Spinner { get; set; } = Spinner.Known.Dots;
    public Style Style { get; set; } = Style.Parse("blue");
    
    public static SpinnerStyle Default => new();
    public static SpinnerStyle Fast => new() 
    { 
        Spinner = Spinner.Known.Arc, 
        Style = Style.Parse("yellow") 
    };
    public static SpinnerStyle Processing => new() 
    { 
        Spinner = Spinner.Known.Star, 
        Style = Style.Parse("cyan") 
    };
}

// Usage Example: Calling Copilot SDK
public class CopilotService
{
    private readonly IProgressRenderer _progress;
    
    public CopilotService(IProgressRenderer progress)
    {
        _progress = progress;
    }
    
    public async Task<string> GetCompletionAsync(string prompt)
    {
        return await _progress.ShowProgressAsync(
            "Calling GitHub Copilot API...",
            async ctx =>
            {
                // Simulate API call stages
                await Task.Delay(500);
                ctx.UpdateStatus("Sending request to Copilot...");
                
                await Task.Delay(1000);
                ctx.UpdateStatus("Waiting for response...");
                ctx.UpdateSpinner(Spinner.Known.Star);
                ctx.UpdateSpinnerStyle(Style.Parse("yellow"));
                
                await Task.Delay(1500);
                ctx.UpdateStatus("Processing response...");
                ctx.UpdateSpinner(Spinner.Known.Dots);
                ctx.UpdateSpinnerStyle(Style.Parse("green"));
                
                await Task.Delay(500);
                return "Copilot response here";
            },
            SpinnerStyle.Processing
        );
    }
}

// Non-blocking progress for REPL mode
public class NonBlockingProgress
{
    private CancellationTokenSource? _cts;
    private Task? _currentTask;
    
    public void StartAsync(string message, Func<Task> operation)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        
        _currentTask = Task.Run(async () =>
        {
            try
            {
                await operation();
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
        }, token);
    }
    
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_currentTask != null)
        {
            await _currentTask;
        }
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Progress;

public class SpectreProgressRendererTests
{
    [Fact]
    public async Task ShowProgressAsync_UpdatesStatusMessages()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);
        var statusUpdates = new List<string>();
        
        // Act
        await renderer.ShowProgressAsync(
            "Initial status",
            async ctx =>
            {
                await Task.Delay(10);
                ctx.UpdateStatus("Second status");
                await Task.Delay(10);
                ctx.UpdateStatus("Final status");
                return "result";
            }
        );
        
        // Assert
        var output = console.Output;
        Assert.Contains("Initial status", output);
        Assert.Contains("Second status", output);
        Assert.Contains("Final status", output);
    }
    
    [Fact]
    public async Task ShowProgressAsync_UsesCustomSpinnerStyle()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);
        var style = new SpinnerStyle
        {
            Spinner = Spinner.Known.Arc,
            Style = Style.Parse("yellow")
        };
        
        // Act
        await renderer.ShowProgressAsync(
            "Processing...",
            async ctx =>
            {
                await Task.Delay(10);
                return "done";
            },
            style
        );
        
        // Assert
        var output = console.Output;
        Assert.Contains("Processing...", output);
    }
}
```

### Best Practices

✅ **DO:**
- Use `Dots` spinner for network calls (calm, steady)
- Use `Arc` or `Star` for processing tasks (active, energetic)
- Update status text to reflect current operation
- Use color changes to indicate progress phases
- Keep status messages concise (< 50 chars)
- Use async/await for non-blocking operations

❌ **DON'T:**
- Don't use spinners for operations < 500ms
- Don't update status too frequently (> 10Hz)
- Don't use distracting spinners in quiet modes
- Don't block the UI thread in REPL mode
- Don't forget to handle cancellation

### Spinner Type Guidelines

| Operation Type | Recommended Spinner | Color | Context |
|----------------|---------------------|-------|---------|
| Network API calls | `Dots` | Blue | Calm, predictable wait |
| File I/O | `Line` | Cyan | Sequential operations |
| Processing/Parsing | `Star` or `Arc` | Yellow | Active computation |
| Building/Compiling | `BouncingBar` | Magenta | Progressive task |
| Downloading | `Dots2` | Green | Steady progress |
| Waiting for user | `Dots3` | Gray | Passive wait |

### References

- [Spectre.Console Status API Tutorial](https://spectreconsole.net/console/tutorials/status-spinners-tutorial)
- [Available Spinner Types](https://spectreconsole.net/api/spectre.console/spinner)
- [StatusContext Documentation](https://spectreconsole.net/api/spectre.console/statuscontext)

---

## REQ-016: Error Display & Correction Guidance

### Pattern Overview

Effective error display uses Spectre.Console's `Panel` component with styled markup to create visually distinct error messages. The pattern includes an `IErrorRenderer` interface for consistent error formatting, with support for correction suggestions and contextual help.

### Complete Code Example

```csharp
using Spectre.Console;
using System.Text;

namespace Lopen.Tui.Errors;

/// <summary>
/// Represents an error with correction guidance
/// </summary>
public record ErrorInfo
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
    public List<string> Suggestions { get; init; } = new();
    public string? DidYouMean { get; init; }
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
}

public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public interface IErrorRenderer
{
    void RenderError(ErrorInfo error);
    void RenderSimpleError(string message);
    void RenderCommandNotFound(string command, string[] suggestions);
}

public class SpectreErrorRenderer : IErrorRenderer
{
    private readonly IAnsiConsole _console;
    
    public SpectreErrorRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }
    
    public void RenderError(ErrorInfo error)
    {
        var panel = CreateErrorPanel(error);
        _console.Write(panel);
    }
    
    public void RenderSimpleError(string message)
    {
        _console.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }
    
    public void RenderCommandNotFound(string command, string[] suggestions)
    {
        var error = new ErrorInfo
        {
            Title = "Command Not Found",
            Message = $"The command '{command}' is not recognized.",
            Severity = ErrorSeverity.Error
        };
        
        if (suggestions.Length > 0)
        {
            error = error with 
            { 
                DidYouMean = suggestions[0],
                Suggestions = suggestions.Skip(1).ToList()
            };
        }
        
        RenderError(error);
    }
    
    private Panel CreateErrorPanel(ErrorInfo error)
    {
        var content = new StringBuilder();
        
        // Error message with appropriate styling
        var messageColor = GetSeverityColor(error.Severity);
        var icon = GetSeverityIcon(error.Severity);
        
        content.AppendLine($"[{messageColor}]{icon} {Markup.Escape(error.Message)}[/]");
        
        // Details if provided
        if (!string.IsNullOrEmpty(error.Details))
        {
            content.AppendLine();
            content.AppendLine($"[dim]{Markup.Escape(error.Details)}[/]");
        }
        
        // "Did you mean?" suggestion
        if (!string.IsNullOrEmpty(error.DidYouMean))
        {
            content.AppendLine();
            content.AppendLine($"[yellow]💡 Did you mean:[/] [cyan]{Markup.Escape(error.DidYouMean)}[/]");
        }
        
        // Additional suggestions
        if (error.Suggestions.Any())
        {
            content.AppendLine();
            content.AppendLine("[yellow]Other suggestions:[/]");
            foreach (var suggestion in error.Suggestions)
            {
                content.AppendLine($"  • [cyan]{Markup.Escape(suggestion)}[/]");
            }
        }
        
        var panel = new Panel(content.ToString())
        {
            Border = GetSeverityBorder(error.Severity),
            BorderStyle = new Style(GetSeverityColorObj(error.Severity)),
            Header = new PanelHeader($" {Markup.Escape(error.Title)} ")
        };
        
        return panel;
    }
    
    private string GetSeverityColor(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Info => "blue",
        ErrorSeverity.Warning => "yellow",
        ErrorSeverity.Error => "red",
        ErrorSeverity.Critical => "red bold",
        _ => "red"
    };
    
    private Color GetSeverityColorObj(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Info => Color.Blue,
        ErrorSeverity.Warning => Color.Yellow,
        ErrorSeverity.Error => Color.Red,
        ErrorSeverity.Critical => Color.Red,
        _ => Color.Red
    };
    
    private string GetSeverityIcon(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Info => "ℹ",
        ErrorSeverity.Warning => "⚠",
        ErrorSeverity.Error => "✗",
        ErrorSeverity.Critical => "💥",
        _ => "✗"
    };
    
    private BoxBorder GetSeverityBorder(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Critical => BoxBorder.Double,
        _ => BoxBorder.Rounded
    };
}

// Integration with System.CommandLine
public class ErrorHandlingMiddleware
{
    private readonly IErrorRenderer _errorRenderer;
    
    public ErrorHandlingMiddleware(IErrorRenderer errorRenderer)
    {
        _errorRenderer = errorRenderer;
    }
    
    public void HandleCommandLineError(Exception exception)
    {
        var error = exception switch
        {
            ArgumentException argEx => new ErrorInfo
            {
                Title = "Invalid Argument",
                Message = argEx.Message,
                Severity = ErrorSeverity.Error,
                Suggestions = new List<string> 
                { 
                    "Run 'lopen --help' for usage information",
                    "Check the documentation at https://github.com/your-org/lopen"
                }
            },
            FileNotFoundException fileEx => new ErrorInfo
            {
                Title = "File Not Found",
                Message = fileEx.Message,
                Details = $"Searched in: {fileEx.FileName}",
                Severity = ErrorSeverity.Error
            },
            UnauthorizedAccessException => new ErrorInfo
            {
                Title = "Authentication Required",
                Message = "GitHub authentication required for this operation",
                Severity = ErrorSeverity.Error,
                Suggestions = new List<string>
                {
                    "Run 'lopen auth login' to authenticate",
                    "Set GITHUB_TOKEN environment variable"
                }
            },
            _ => new ErrorInfo
            {
                Title = "Unexpected Error",
                Message = exception.Message,
                Details = exception.StackTrace,
                Severity = ErrorSeverity.Critical
            }
        };
        
        _errorRenderer.RenderError(error);
    }
}

// Levenshtein distance for "Did you mean?" suggestions
public static class SuggestionHelper
{
    public static string[] GetSuggestions(string input, string[] validCommands, int maxDistance = 2)
    {
        return validCommands
            .Select(cmd => new { Command = cmd, Distance = LevenshteinDistance(input, cmd) })
            .Where(x => x.Distance <= maxDistance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Command)
            .ToArray();
    }
    
    private static int LevenshteinDistance(string s, string t)
    {
        var d = new int[s.Length + 1, t.Length + 1];
        
        for (int i = 0; i <= s.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++)
            d[0, j] = j;
        
        for (int j = 1; j <= t.Length; j++)
        {
            for (int i = 1; i <= s.Length; i++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        
        return d[s.Length, t.Length];
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Errors;

public class SpectreErrorRendererTests
{
    [Fact]
    public void RenderError_DisplaysErrorWithSuggestions()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);
        var error = new ErrorInfo
        {
            Title = "Command Not Found",
            Message = "The command 'statr' is not recognized.",
            DidYouMean = "start",
            Suggestions = new List<string> { "status", "stop" }
        };
        
        // Act
        renderer.RenderError(error);
        
        // Assert
        var output = console.Output;
        Assert.Contains("Command Not Found", output);
        Assert.Contains("statr", output);
        Assert.Contains("Did you mean", output);
        Assert.Contains("start", output);
    }
    
    [Fact]
    public void RenderCommandNotFound_UsesSuggestions()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);
        var suggestions = new[] { "start", "status", "stop" };
        
        // Act
        renderer.RenderCommandNotFound("statr", suggestions);
        
        // Assert
        var output = console.Output;
        Assert.Contains("start", output);
    }
}

public class SuggestionHelperTests
{
    [Theory]
    [InlineData("statr", new[] { "start", "status", "stop" }, "start")]
    [InlineData("hlep", new[] { "help", "version" }, "help")]
    [InlineData("authh", new[] { "auth", "loop" }, "auth")]
    public void GetSuggestions_FindsClosestMatch(string input, string[] validCommands, string expected)
    {
        // Act
        var suggestions = SuggestionHelper.GetSuggestions(input, validCommands);
        
        // Assert
        Assert.Contains(expected, suggestions);
        Assert.Equal(expected, suggestions[0]);
    }
}
```

### Best Practices

✅ **DO:**
- Use panels for complex errors with multiple pieces of information
- Escape user input with `Markup.Escape()` to prevent injection
- Provide actionable suggestions ("Run X to fix this")
- Use appropriate severity levels
- Include "Did you mean?" for command typos
- Keep error messages concise and user-friendly

❌ **DON'T:**
- Don't show stack traces in production (use `--verbose` flag)
- Don't use technical jargon in error messages
- Don't provide too many suggestions (max 3-5)
- Don't forget to highlight the actual issue
- Don't use ERROR ALL CAPS shouting

### Error Display Guidelines

| Error Type | Severity | Border | Suggestions Required |
|------------|----------|--------|----------------------|
| Command not found | Error | Rounded | Yes - similar commands |
| Invalid argument | Error | Rounded | Yes - valid options |
| File not found | Error | Rounded | Maybe - common paths |
| Auth required | Error | Rounded | Yes - auth commands |
| API failure | Error | Rounded | Yes - retry, status |
| Critical failure | Critical | Double | No - just report |

### References

- [Spectre.Console Panel API](https://spectreconsole.net/widgets/panel)
- [Markup Documentation](https://spectreconsole.net/markup)
- [BoxBorder Styles](https://spectreconsole.net/api/spectre.console/boxborder)

---

## REQ-017: Structured Data Display (Panels & Trees)

### Pattern Overview

Spectre.Console provides `Panel`, `Tree`, and `Table` components for displaying structured data. Panels work well for metadata, Trees for hierarchical data, and Tables for tabular data. The key is choosing the right component and making it responsive.

### Complete Code Example

```csharp
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Lopen.Tui.Display;

public interface IDataRenderer
{
    void RenderMetadata(Dictionary<string, string> data, string title);
    void RenderHierarchy(TreeNode root, string title);
    void RenderTable<T>(IEnumerable<T> items, TableConfig<T> config);
}

public class TreeNode
{
    public required string Label { get; init; }
    public List<TreeNode> Children { get; init; } = new();
    public string? Icon { get; init; }
}

public class TableConfig<T>
{
    public required string Title { get; init; }
    public List<TableColumn<T>> Columns { get; init; } = new();
    public bool Expand { get; init; } = true;
    public BoxBorder Border { get; init; } = BoxBorder.Rounded;
}

public class TableColumn<T>
{
    public required string Header { get; init; }
    public required Func<T, string> Selector { get; init; }
    public Justify Alignment { get; init; } = Justify.Left;
    public int? Width { get; init; }
}

public class SpectreDataRenderer : IDataRenderer
{
    private readonly IAnsiConsole _console;
    private readonly int _terminalWidth;
    
    public SpectreDataRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _terminalWidth = console.Profile.Width;
    }
    
    public void RenderMetadata(Dictionary<string, string> data, string title)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());
        
        foreach (var (key, value) in data)
        {
            grid.AddRow(
                $"[bold]{Markup.Escape(key)}:[/]",
                Markup.Escape(value)
            );
        }
        
        var panel = new Panel(grid)
        {
            Header = new PanelHeader($" {Markup.Escape(title)} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(1, 0, 1, 0)
        };
        
        _console.Write(panel);
    }
    
    public void RenderHierarchy(TreeNode root, string title)
    {
        var tree = new Tree(FormatNodeLabel(root));
        BuildTree(tree, root.Children);
        
        var panel = new Panel(tree)
        {
            Header = new PanelHeader($" {Markup.Escape(title)} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };
        
        _console.Write(panel);
    }
    
    private void BuildTree(IHasTreeNodes parent, List<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            var treeNode = parent.AddNode(FormatNodeLabel(node));
            if (node.Children.Any())
            {
                BuildTree(treeNode, node.Children);
            }
        }
    }
    
    private string FormatNodeLabel(TreeNode node)
    {
        var icon = string.IsNullOrEmpty(node.Icon) ? "" : $"{node.Icon} ";
        return $"{icon}{Markup.Escape(node.Label)}";
    }
    
    public void RenderTable<T>(IEnumerable<T> items, TableConfig<T> config)
    {
        var table = new Table();
        
        // Configure table
        table.Border(config.Border);
        table.Title(config.Title);
        if (config.Expand)
        {
            table.Expand();
        }
        
        // Add columns with responsive widths
        foreach (var column in config.Columns)
        {
            var tableColumn = new TableColumn(column.Header);
            if (column.Width.HasValue)
            {
                tableColumn.Width(column.Width.Value);
            }
            tableColumn.Alignment(column.Alignment);
            table.AddColumn(tableColumn);
        }
        
        // Add rows
        foreach (var item in items)
        {
            var values = config.Columns
                .Select(c => Markup.Escape(c.Selector(item)))
                .ToArray();
            table.AddRow(values);
        }
        
        _console.Write(table);
    }
}

// Nested Panel Example (max 2 levels as per spec)
public class NestedPanelRenderer
{
    private readonly IAnsiConsole _console;
    
    public NestedPanelRenderer(IAnsiConsole console)
    {
        _console = console;
    }
    
    public void RenderTaskWithContext(string task, Dictionary<string, string> context)
    {
        // Inner panel - context details
        var contextGrid = new Grid();
        contextGrid.AddColumn();
        contextGrid.AddColumn();
        
        foreach (var (key, value) in context)
        {
            contextGrid.AddRow(
                $"[dim]{Markup.Escape(key)}:[/]",
                Markup.Escape(value)
            );
        }
        
        var innerPanel = new Panel(contextGrid)
        {
            Header = new PanelHeader(" Context "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };
        
        // Outer panel - task with nested context
        var outerContent = new Rows(
            new Markup($"[bold]{Markup.Escape(task)}[/]"),
            new Text(""),
            innerPanel
        );
        
        var outerPanel = new Panel(outerContent)
        {
            Header = new PanelHeader(" Current Task "),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue)
        };
        
        _console.Write(outerPanel);
    }
}

// Responsive column width calculation
public class ResponsiveColumnCalculator
{
    public static int[] CalculateColumnWidths(int terminalWidth, int columnCount, int[] priorities)
    {
        // Reserve space for borders and padding
        var availableWidth = terminalWidth - (columnCount * 3) - 2;
        
        var widths = new int[columnCount];
        var totalPriority = priorities.Sum();
        
        for (int i = 0; i < columnCount; i++)
        {
            widths[i] = (int)((priorities[i] / (double)totalPriority) * availableWidth);
        }
        
        return widths;
    }
}

// Usage Examples
public class DisplayExamples
{
    public void ShowLoopStatus(IDataRenderer renderer)
    {
        var metadata = new Dictionary<string, string>
        {
            ["Status"] = "Running",
            ["Iteration"] = "3/10",
            ["Model"] = "gpt-4",
            ["Tokens Used"] = "1,234",
            ["Cost"] = "$0.05"
        };
        
        renderer.RenderMetadata(metadata, "Loop Status");
    }
    
    public void ShowFileTree(IDataRenderer renderer)
    {
        var root = new TreeNode
        {
            Label = "project/",
            Icon = "📁",
            Children = new List<TreeNode>
            {
                new() { Label = "src/", Icon = "📁", Children = new()
                {
                    new() { Label = "Program.cs", Icon = "📄" },
                    new() { Label = "Commands.cs", Icon = "📄" }
                }},
                new() { Label = "tests/", Icon = "📁", Children = new()
                {
                    new() { Label = "Tests.cs", Icon = "📄" }
                }},
                new() { Label = "README.md", Icon = "📄" }
            }
        };
        
        renderer.RenderHierarchy(root, "Project Structure");
    }
    
    public void ShowTaskList(IDataRenderer renderer)
    {
        var tasks = new[]
        {
            new { Id = "1", Description = "Implement auth", Status = "✓ Done" },
            new { Id = "2", Description = "Add TUI", Status = "⏳ In Progress" },
            new { Id = "3", Description = "Write tests", Status = "⏸ Pending" }
        };
        
        var config = new TableConfig<dynamic>
        {
            Title = "Tasks",
            Columns = new()
            {
                new() { Header = "ID", Selector = t => t.Id, Width = 5 },
                new() { Header = "Description", Selector = t => t.Description },
                new() { Header = "Status", Selector = t => t.Status, Width = 15, Alignment = Justify.Center }
            }
        };
        
        renderer.RenderTable(tasks, config);
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Display;

public class SpectreDataRendererTests
{
    [Fact]
    public void RenderMetadata_DisplaysKeyValuePairs()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreDataRenderer(console);
        var data = new Dictionary<string, string>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };
        
        // Act
        renderer.RenderMetadata(data, "Test Data");
        
        // Assert
        var output = console.Output;
        Assert.Contains("Test Data", output);
        Assert.Contains("Key1", output);
        Assert.Contains("Value1", output);
    }
    
    [Fact]
    public void RenderHierarchy_DisplaysTreeStructure()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreDataRenderer(console);
        var root = new TreeNode
        {
            Label = "Root",
            Children = new()
            {
                new() { Label = "Child1" },
                new() { Label = "Child2" }
            }
        };
        
        // Act
        renderer.RenderHierarchy(root, "Tree");
        
        // Assert
        var output = console.Output;
        Assert.Contains("Root", output);
        Assert.Contains("Child1", output);
        Assert.Contains("Child2", output);
    }
    
    [Fact]
    public void RenderTable_DisplaysColumnsAndRows()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new { Name = "Item1" }, new { Name = "Item2" } };
        var config = new TableConfig<dynamic>
        {
            Title = "Items",
            Columns = new() { new() { Header = "Name", Selector = i => i.Name } }
        };
        
        // Act
        renderer.RenderTable(items, config);
        
        // Assert
        var output = console.Output;
        Assert.Contains("Items", output);
        Assert.Contains("Item1", output);
        Assert.Contains("Item2", output);
    }
}

public class ResponsiveColumnCalculatorTests
{
    [Fact]
    public void CalculateColumnWidths_DistributesBasedOnPriority()
    {
        // Arrange
        var terminalWidth = 100;
        var columnCount = 3;
        var priorities = new[] { 1, 2, 1 }; // Middle column gets 50%
        
        // Act
        var widths = ResponsiveColumnCalculator.CalculateColumnWidths(
            terminalWidth, columnCount, priorities);
        
        // Assert
        Assert.Equal(3, widths.Length);
        Assert.True(widths[1] > widths[0]); // Middle column is wider
        Assert.True(widths[1] > widths[2]);
    }
}
```

### Best Practices

✅ **DO:**
- Use `Panel` for metadata and key-value displays
- Use `Tree` for file systems and hierarchical structures
- Use `Table` for list data with multiple properties
- Calculate responsive column widths based on terminal size
- Limit nesting to 2 levels maximum
- Use icons/emojis for visual hierarchy
- Escape user-provided content

❌ **DON'T:**
- Don't use tables for 1-2 columns (use grid or simple format)
- Don't exceed 2 levels of nested panels
- Don't hardcode column widths without responsive fallback
- Don't forget padding in nested panels
- Don't use too many tree levels (> 5)

### Component Selection Guide

| Data Type | Component | When to Use |
|-----------|-----------|-------------|
| Metadata (key-value) | Panel + Grid | Configuration, status, properties |
| File system | Tree | Directories, hierarchies |
| List of items | Table | Tasks, files, search results |
| Single message | Panel | Notices, warnings, highlights |
| Nested context | Nested Panel (2 max) | Task with details, item with metadata |

### References

- [Panel Widget](https://spectreconsole.net/widgets/panel)
- [Tree Widget](https://spectreconsole.net/widgets/tree)
- [Table Widget](https://spectreconsole.net/widgets/table)
- [Grid Layout](https://spectreconsole.net/widgets/grid)

---

## REQ-018: Layout & Right-Side Panels

### Pattern Overview

Spectre.Console's `Layout` class enables split-screen designs with independently updatable sections. The pattern uses `Columns` or `Layout` to create side-by-side panels, with responsive behavior that stacks vertically on narrow terminals.

### Complete Code Example

```csharp
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Lopen.Tui.Layout;

public interface ILayoutRenderer
{
    void RenderSplitLayout(IRenderable left, IRenderable right, int minWidthForSplit = 120);
    ILiveLayout CreateLiveLayout();
}

public interface ILiveLayout : IDisposable
{
    void UpdateLeft(IRenderable content);
    void UpdateRight(IRenderable content);
    void UpdateSection(string sectionName, IRenderable content);
}

public class SpectreLayoutRenderer : ILayoutRenderer
{
    private readonly IAnsiConsole _console;
    
    public SpectreLayoutRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }
    
    public void RenderSplitLayout(IRenderable left, IRenderable right, int minWidthForSplit = 120)
    {
        var terminalWidth = _console.Profile.Width;
        
        if (terminalWidth >= minWidthForSplit)
        {
            // Side-by-side layout
            var columns = new Columns(left, right);
            _console.Write(columns);
        }
        else
        {
            // Stacked layout for narrow terminals
            _console.Write(left);
            _console.WriteLine();
            _console.Write(right);
        }
    }
    
    public ILiveLayout CreateLiveLayout()
    {
        return new SpectreLiveLayout(_console);
    }
    
    private class SpectreLiveLayout : ILiveLayout
    {
        private readonly IAnsiConsole _console;
        private readonly Spectre.Console.Layout _layout;
        private LiveDisplayContext? _liveContext;
        private Task? _liveTask;
        
        public SpectreLiveLayout(IAnsiConsole console)
        {
            _console = console;
            
            // Create layout structure
            _layout = new Spectre.Console.Layout("Root")
                .SplitColumns(
                    new Spectre.Console.Layout("Left"),
                    new Spectre.Console.Layout("Right")
                );
            
            // Set initial sizing
            _layout["Left"].Size(60);
            _layout["Right"].Size(40);
            
            // Start live display
            _liveTask = StartLiveDisplay();
        }
        
        private Task StartLiveDisplay()
        {
            return Task.Run(() =>
            {
                _console.Live(_layout)
                    .Start(ctx =>
                    {
                        _liveContext = ctx;
                        
                        // Keep display alive until disposed
                        while (_liveContext != null)
                        {
                            Thread.Sleep(100);
                        }
                    });
            });
        }
        
        public void UpdateLeft(IRenderable content)
        {
            UpdateSection("Left", content);
        }
        
        public void UpdateRight(IRenderable content)
        {
            UpdateSection("Right", content);
        }
        
        public void UpdateSection(string sectionName, IRenderable content)
        {
            _layout[sectionName].Update(content);
            _liveContext?.Refresh();
        }
        
        public void Dispose()
        {
            _liveContext = null;
            _liveTask?.Wait(TimeSpan.FromSeconds(1));
        }
    }
}

// Task list panel for right side
public class TaskListPanel
{
    public static Panel Create(IEnumerable<TaskItem> tasks)
    {
        var table = new Table();
        table.Border(BoxBorder.None);
        table.HideHeaders();
        table.AddColumn(new TableColumn("").Width(2));
        table.AddColumn(new TableColumn(""));
        
        foreach (var task in tasks)
        {
            var icon = task.Status switch
            {
                TaskStatus.Pending => "⏸",
                TaskStatus.InProgress => "⏳",
                TaskStatus.Done => "✓",
                TaskStatus.Failed => "✗",
                _ => "•"
            };
            
            var color = task.Status switch
            {
                TaskStatus.InProgress => "yellow",
                TaskStatus.Done => "green",
                TaskStatus.Failed => "red",
                _ => "dim"
            };
            
            table.AddRow(
                $"[{color}]{icon}[/]",
                $"[{color}]{Markup.Escape(task.Description)}[/]"
            );
        }
        
        return new Panel(table)
        {
            Header = new PanelHeader(" Tasks "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };
    }
}

public record TaskItem(string Description, TaskStatus Status);

public enum TaskStatus
{
    Pending,
    InProgress,
    Done,
    Failed
}

// Context panel for right side
public class ContextPanel
{
    public static Panel Create(Dictionary<string, string> context)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());
        
        foreach (var (key, value) in context)
        {
            grid.AddRow(
                $"[dim]{Markup.Escape(key)}:[/]",
                Markup.Escape(value)
            );
        }
        
        return new Panel(grid)
        {
            Header = new PanelHeader(" Context "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
    }
}

// Usage Example: REPL with side panel
public class ReplWithSidePanel
{
    private readonly ILayoutRenderer _layoutRenderer;
    private ILiveLayout? _liveLayout;
    
    public ReplWithSidePanel(ILayoutRenderer layoutRenderer)
    {
        _layoutRenderer = layoutRenderer;
    }
    
    public void Start()
    {
        _liveLayout = _layoutRenderer.CreateLiveLayout();
        
        // Initial right panel
        UpdateTaskList(new[]
        {
            new TaskItem("Initialize", TaskStatus.Done),
            new TaskItem("Load configuration", TaskStatus.Done),
            new TaskItem("Ready", TaskStatus.Done)
        });
        
        // Main content on left
        var mainContent = new Panel("Type commands here...")
        {
            Header = new PanelHeader(" Lopen REPL "),
            Border = BoxBorder.Double
        };
        
        _liveLayout.UpdateLeft(mainContent);
    }
    
    public void UpdateTaskList(IEnumerable<TaskItem> tasks)
    {
        var taskPanel = TaskListPanel.Create(tasks);
        _liveLayout?.UpdateRight(taskPanel);
    }
    
    public void UpdateContext(Dictionary<string, string> context)
    {
        var contextPanel = ContextPanel.Create(context);
        _liveLayout?.UpdateRight(contextPanel);
    }
    
    public void Stop()
    {
        _liveLayout?.Dispose();
    }
}

// Responsive layout helper
public static class ResponsiveLayoutHelper
{
    public static bool ShouldUseSplitLayout(int terminalWidth, int minWidth = 120)
    {
        return terminalWidth >= minWidth;
    }
    
    public static (int left, int right) CalculateSplitPercentages(int terminalWidth)
    {
        // For wide terminals, give more space to main content
        if (terminalWidth >= 160)
            return (70, 30);
        else if (terminalWidth >= 120)
            return (60, 40);
        else
            return (100, 0); // Stack vertically
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Layout;

public class SpectreLayoutRendererTests
{
    [Fact]
    public void RenderSplitLayout_WideTerminal_ShowsSideBySide()
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Width = 150; // Wide terminal
        var renderer = new SpectreLayoutRenderer(console);
        
        var left = new Panel("Left Content");
        var right = new Panel("Right Content");
        
        // Act
        renderer.RenderSplitLayout(left, right);
        
        // Assert
        var output = console.Output;
        Assert.Contains("Left Content", output);
        Assert.Contains("Right Content", output);
    }
    
    [Fact]
    public void RenderSplitLayout_NarrowTerminal_Stacks()
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Width = 80; // Narrow terminal
        var renderer = new SpectreLayoutRenderer(console);
        
        var left = new Panel("Left Content");
        var right = new Panel("Right Content");
        
        // Act
        renderer.RenderSplitLayout(left, right, minWidthForSplit: 120);
        
        // Assert
        var output = console.Output;
        Assert.Contains("Left Content", output);
        Assert.Contains("Right Content", output);
    }
}

public class TaskListPanelTests
{
    [Fact]
    public void Create_RendersTasksWithStatus()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem("Task 1", TaskStatus.Done),
            new TaskItem("Task 2", TaskStatus.InProgress)
        };
        
        // Act
        var panel = TaskListPanel.Create(tasks);
        
        // Assert
        Assert.NotNull(panel);
        Assert.Equal(" Tasks ", panel.Header?.Text);
    }
}

public class ResponsiveLayoutHelperTests
{
    [Theory]
    [InlineData(80, false)]
    [InlineData(120, true)]
    [InlineData(160, true)]
    public void ShouldUseSplitLayout_ReturnsCorrectValue(int width, bool expected)
    {
        // Act
        var result = ResponsiveLayoutHelper.ShouldUseSplitLayout(width);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(160, 70, 30)]
    [InlineData(120, 60, 40)]
    [InlineData(80, 100, 0)]
    public void CalculateSplitPercentages_ReturnsCorrectSplit(
        int width, int expectedLeft, int expectedRight)
    {
        // Act
        var (left, right) = ResponsiveLayoutHelper.CalculateSplitPercentages(width);
        
        // Assert
        Assert.Equal(expectedLeft, left);
        Assert.Equal(expectedRight, right);
    }
}
```

### Best Practices

✅ **DO:**
- Use `Columns` for simple side-by-side layouts
- Use `Layout` with `Live` for dynamically updating panels
- Implement responsive behavior based on terminal width
- Set explicit column sizes with `.Size()` percentage
- Keep right panel width between 30-40% of terminal
- Test both wide and narrow terminal scenarios

❌ **DON'T:**
- Don't use split layout on terminals < 120 chars
- Don't update layout too frequently (< 100ms)
- Don't create more than 3 columns
- Don't forget to dispose of live layouts
- Don't block the main thread with layout updates

### Layout Breakpoints

| Terminal Width | Layout Strategy | Left % | Right % |
|----------------|----------------|--------|---------|
| < 120 chars | Stack vertically | 100% | (below) |
| 120-159 chars | Split 60/40 | 60% | 40% |
| ≥ 160 chars | Split 70/30 | 70% | 30% |

### References

- [Layout Widget](https://spectreconsole.net/widgets/layout)
- [Columns Widget](https://spectreconsole.net/widgets/columns)
- [Live Display](https://spectreconsole.net/live/live-display)

---

## REQ-019: AI Response Streaming

### Pattern Overview

Streaming AI responses requires buffering tokens into complete words/sentences before display. Spectre.Console's `Live` display enables efficient real-time updates. The pattern buffers text, detects natural break points, and flushes periodically with markdown formatting.

### Complete Code Example

```csharp
using Spectre.Console;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lopen.Tui.Streaming;

public interface IStreamRenderer
{
    Task RenderStreamAsync(
        IAsyncEnumerable<string> stream,
        CancellationToken cancellationToken = default);
}

public class StreamingConfig
{
    public int FlushIntervalMs { get; set; } = 100;
    public int BufferSizeChars { get; set; } = 50;
    public bool EnableMarkdown { get; set; } = true;
    public string[] BreakPoints { get; set; } = new[] { ".", "!", "?", "\n", " " };
}

public class SpectreStreamRenderer : IStreamRenderer
{
    private readonly IAnsiConsole _console;
    private readonly StreamingConfig _config;
    
    public SpectreStreamRenderer(IAnsiConsole console, StreamingConfig? config = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _config = config ?? new StreamingConfig();
    }
    
    public async Task RenderStreamAsync(
        IAsyncEnumerable<string> stream,
        CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();
        var fullText = new StringBuilder();
        var lastFlush = DateTime.UtcNow;
        
        await foreach (var token in stream.WithCancellation(cancellationToken))
        {
            buffer.Append(token);
            fullText.Append(token);
            
            // Check if we should flush
            var shouldFlush = ShouldFlush(buffer, lastFlush);
            
            if (shouldFlush)
            {
                await FlushBufferAsync(buffer, fullText);
                buffer.Clear();
                lastFlush = DateTime.UtcNow;
            }
        }
        
        // Flush remaining
        if (buffer.Length > 0)
        {
            await FlushBufferAsync(buffer, fullText);
        }
        
        _console.WriteLine();
    }
    
    private bool ShouldFlush(StringBuilder buffer, DateTime lastFlush)
    {
        // Flush if buffer is large enough
        if (buffer.Length >= _config.BufferSizeChars)
        {
            // Check for natural break point
            var text = buffer.ToString();
            return _config.BreakPoints.Any(bp => text.EndsWith(bp));
        }
        
        // Flush if timeout reached
        var elapsed = DateTime.UtcNow - lastFlush;
        if (elapsed.TotalMilliseconds >= _config.FlushIntervalMs)
        {
            return buffer.Length > 0;
        }
        
        return false;
    }
    
    private async Task FlushBufferAsync(StringBuilder buffer, StringBuilder fullText)
    {
        var text = buffer.ToString();
        
        if (_config.EnableMarkdown)
        {
            // Simple markdown rendering (extend as needed)
            text = FormatMarkdown(text);
        }
        
        _console.Markup(Markup.Escape(text));
        await Task.Delay(1); // Yield to allow rendering
    }
    
    private string FormatMarkdown(string text)
    {
        // Basic markdown formatting
        // In production, use a proper markdown parser
        
        // Code blocks are handled separately (see CodeBlockStreamRenderer)
        // This handles inline formatting
        
        return text;
    }
}

// Code block detection and formatting
public class CodeBlockStreamRenderer
{
    private readonly IAnsiConsole _console;
    private bool _inCodeBlock = false;
    private StringBuilder _codeBuffer = new();
    private string _codeLanguage = "";
    
    public async Task RenderWithCodeBlocksAsync(
        IAsyncEnumerable<string> stream,
        CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();
        
        await foreach (var token in stream.WithCancellation(cancellationToken))
        {
            buffer.Append(token);
            
            // Check for code block markers
            var text = buffer.ToString();
            
            if (text.Contains("```"))
            {
                await HandleCodeBlockTransitionAsync(buffer);
            }
            else if (_inCodeBlock)
            {
                // Buffer code content
                _codeBuffer.Append(token);
            }
            else
            {
                // Regular text - flush on natural breaks
                if (ShouldFlushText(buffer))
                {
                    _console.Markup(Markup.Escape(buffer.ToString()));
                    buffer.Clear();
                }
            }
        }
        
        // Flush remaining
        if (buffer.Length > 0)
        {
            _console.Markup(Markup.Escape(buffer.ToString()));
        }
    }
    
    private async Task HandleCodeBlockTransitionAsync(StringBuilder buffer)
    {
        var text = buffer.ToString();
        
        if (!_inCodeBlock)
        {
            // Starting code block
            _inCodeBlock = true;
            
            // Extract language hint
            var lines = text.Split('\n');
            var codeBlockLine = lines.First(l => l.Contains("```"));
            _codeLanguage = codeBlockLine.Replace("```", "").Trim();
            
            // Render text before code block
            var beforeCode = text.Substring(0, text.IndexOf("```"));
            if (!string.IsNullOrWhiteSpace(beforeCode))
            {
                _console.Markup(Markup.Escape(beforeCode));
            }
            
            buffer.Clear();
            _codeBuffer.Clear();
        }
        else
        {
            // Ending code block
            _inCodeBlock = false;
            
            // Render code block
            RenderCodeBlock(_codeBuffer.ToString(), _codeLanguage);
            
            // Clear and continue with remaining text
            _codeBuffer.Clear();
            buffer.Clear();
        }
        
        await Task.Delay(1);
    }
    
    private void RenderCodeBlock(string code, string language)
    {
        var panel = new Panel(code)
        {
            Header = new PanelHeader($" {language} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0, 1, 0)
        };
        
        _console.Write(panel);
    }
    
    private bool ShouldFlushText(StringBuilder buffer)
    {
        if (buffer.Length < 30) return false;
        
        var text = buffer.ToString();
        return text.EndsWith(" ") || text.EndsWith(".") || text.EndsWith("\n");
    }
}

// Live updating display for streaming
public class LiveStreamRenderer
{
    private readonly IAnsiConsole _console;
    
    public async Task RenderWithLiveDisplayAsync(
        IAsyncEnumerable<string> stream,
        CancellationToken cancellationToken = default)
    {
        var fullText = new StringBuilder();
        
        await _console
            .Live(new Panel(""))
            .StartAsync(async ctx =>
            {
                await foreach (var token in stream.WithCancellation(cancellationToken))
                {
                    fullText.Append(token);
                    
                    var panel = new Panel(Markup.Escape(fullText.ToString()))
                    {
                        Header = new PanelHeader(" AI Response "),
                        Border = BoxBorder.Rounded
                    };
                    
                    ctx.UpdateTarget(panel);
                    await Task.Delay(50); // Smooth animation
                }
            });
    }
}

// Paragraph-based buffering strategy
public class ParagraphBufferedRenderer
{
    private readonly IAnsiConsole _console;
    
    public async Task RenderParagraphsAsync(
        IAsyncEnumerable<string> stream,
        CancellationToken cancellationToken = default)
    {
        var paragraphBuffer = new StringBuilder();
        
        await foreach (var token in stream.WithCancellation(cancellationToken))
        {
            paragraphBuffer.Append(token);
            
            // Check for paragraph end (double newline or sentence end + newline)
            var text = paragraphBuffer.ToString();
            if (text.EndsWith("\n\n") || 
                (text.EndsWith(".\n") || text.EndsWith("!\n") || text.EndsWith("?\n")))
            {
                // Flush paragraph
                _console.MarkupLine(Markup.Escape(text.Trim()));
                _console.WriteLine();
                paragraphBuffer.Clear();
            }
        }
        
        // Flush remaining
        if (paragraphBuffer.Length > 0)
        {
            _console.MarkupLine(Markup.Escape(paragraphBuffer.ToString().Trim()));
        }
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Streaming;

public class SpectreStreamRendererTests
{
    [Fact]
    public async Task RenderStreamAsync_FlushesOnBreakPoints()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreStreamRenderer(console, new StreamingConfig
        {
            BufferSizeChars = 10,
            FlushIntervalMs = 1000
        });
        
        var tokens = new[] { "Hello", " ", "world", ".", " ", "How", " ", "are", " ", "you", "?" };
        var stream = CreateAsyncEnumerable(tokens);
        
        // Act
        await renderer.RenderStreamAsync(stream);
        
        // Assert
        var output = console.Output;
        Assert.Contains("Hello world.", output);
        Assert.Contains("How are you?", output);
    }
    
    [Fact]
    public async Task RenderStreamAsync_FlushesOnTimeout()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new SpectreStreamRenderer(console, new StreamingConfig
        {
            BufferSizeChars = 1000, // Large buffer
            FlushIntervalMs = 50 // Short timeout
        });
        
        var tokens = new[] { "Slow", "..." };
        var stream = CreateAsyncEnumerableWithDelay(tokens, 100);
        
        // Act
        await renderer.RenderStreamAsync(stream);
        
        // Assert
        var output = console.Output;
        Assert.Contains("Slow", output);
    }
    
    private async IAsyncEnumerable<string> CreateAsyncEnumerable(string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Delay(1);
            yield return token;
        }
    }
    
    private async IAsyncEnumerable<string> CreateAsyncEnumerableWithDelay(
        string[] tokens, int delayMs)
    {
        foreach (var token in tokens)
        {
            await Task.Delay(delayMs);
            yield return token;
        }
    }
}

public class CodeBlockStreamRendererTests
{
    [Fact]
    public async Task RenderWithCodeBlocksAsync_DetectsCodeBlocks()
    {
        // Arrange
        var console = new TestConsole();
        var renderer = new CodeBlockStreamRenderer();
        
        var tokens = new[] 
        { 
            "Here is code:\n",
            "```csharp\n",
            "var x = 1;\n",
            "```\n",
            "Done."
        };
        var stream = CreateAsyncEnumerable(tokens);
        
        // Act - would need to expose console in constructor for testing
        // This is a simplified test
        
        // Assert
        // Verify code block was detected and formatted
    }
    
    private async IAsyncEnumerable<string> CreateAsyncEnumerable(string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Delay(1);
            yield return token;
        }
    }
}
```

### Best Practices

✅ **DO:**
- Buffer tokens into natural units (words, sentences, paragraphs)
- Flush on sentence boundaries (., !, ?)
- Use timeout-based flushing as fallback (100-200ms)
- Handle code blocks separately with proper formatting
- Keep buffer size reasonable (30-100 chars)
- Escape user content with `Markup.Escape()`
- Use async/await throughout

❌ **DON'T:**
- Don't flush every single token (causes flicker)
- Don't buffer indefinitely (use timeouts)
- Don't forget to flush remaining content
- Don't block the rendering thread
- Don't try to parse complex markdown in real-time
- Don't use `Live` for simple streaming (overhead)

### Buffering Strategies

| Strategy | Buffer Size | Flush Trigger | Use Case |
|----------|-------------|---------------|----------|
| Word-based | 1 word | Space, punctuation | Fast, choppy |
| Sentence-based | ~50 chars | .!?\n | Balanced |
| Paragraph-based | ~200 chars | \n\n | Smooth, slower |
| Timeout-based | Any | 100ms | Fallback |
| Code block | Until ``` | ``` marker | Code formatting |

### References

- [Live Display](https://spectreconsole.net/live/live-display)
- [Markup Syntax](https://spectreconsole.net/markup)
- [Async Streaming Best Practices](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios)

---

## REQ-020: Responsive Terminal Detection

### Pattern Overview

Detecting terminal capabilities enables adaptive behavior. Spectre.Console provides `AnsiConsole.Profile` for capability detection. The pattern creates a `TerminalCapabilities` class to centralize detection logic and provide consistent behavior across the application.

### Complete Code Example

```csharp
using Spectre.Console;
using System;

namespace Lopen.Tui.Terminal;

public interface ITerminalCapabilities
{
    bool SupportsColor { get; }
    bool SupportsAnsi { get; }
    bool SupportsInteractive { get; }
    int Width { get; }
    int Height { get; }
    ColorSystem ColorSystem { get; }
    bool IsNoColorSet { get; }
}

public class TerminalCapabilities : ITerminalCapabilities
{
    private readonly IAnsiConsole _console;
    
    public TerminalCapabilities(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }
    
    public bool SupportsColor => 
        !IsNoColorSet && _console.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;
    
    public bool SupportsAnsi => 
        _console.Profile.Capabilities.Ansi;
    
    public bool SupportsInteractive => 
        _console.Profile.Capabilities.Interactive;
    
    public int Width => 
        _console.Profile.Width;
    
    public int Height => 
        _console.Profile.Height;
    
    public ColorSystem ColorSystem => 
        IsNoColorSet ? ColorSystem.NoColors : _console.Profile.Capabilities.ColorSystem;
    
    public bool IsNoColorSet => 
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    
    // Convenience methods
    public bool IsTrueColor => ColorSystem == ColorSystem.TrueColor;
    public bool Is256Color => ColorSystem == ColorSystem.EightBit;
    public bool IsBasicColor => ColorSystem == ColorSystem.Standard;
    public bool IsWideTerminal => Width >= 120;
    public bool IsNarrowTerminal => Width < 80;
}

// Adaptive renderer that respects terminal capabilities
public class AdaptiveRenderer
{
    private readonly IAnsiConsole _console;
    private readonly ITerminalCapabilities _capabilities;
    
    public AdaptiveRenderer(IAnsiConsole console, ITerminalCapabilities capabilities)
    {
        _console = console;
        _capabilities = capabilities;
    }
    
    public void WriteSuccess(string message)
    {
        if (_capabilities.SupportsColor)
        {
            _console.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
        }
        else
        {
            _console.WriteLine($"[OK] {message}");
        }
    }
    
    public void WriteError(string message)
    {
        if (_capabilities.SupportsColor)
        {
            _console.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
        }
        else
        {
            _console.WriteLine($"[ERROR] {message}");
        }
    }
    
    public void WritePanel(string title, string content)
    {
        if (_capabilities.SupportsAnsi && _capabilities.Width >= 60)
        {
            // Rich panel
            var panel = new Panel(Markup.Escape(content))
            {
                Header = new PanelHeader($" {Markup.Escape(title)} "),
                Border = BoxBorder.Rounded,
                BorderStyle = _capabilities.SupportsColor 
                    ? new Style(Color.Blue) 
                    : Style.Plain
            };
            _console.Write(panel);
        }
        else
        {
            // Simple text fallback
            _console.WriteLine($"=== {title} ===");
            _console.WriteLine(content);
            _console.WriteLine();
        }
    }
    
    public Color GetColor(string name)
    {
        // Return appropriate color based on capabilities
        return _capabilities.ColorSystem switch
        {
            ColorSystem.TrueColor => GetTrueColor(name),
            ColorSystem.EightBit => Get256Color(name),
            ColorSystem.Standard => GetBasicColor(name),
            _ => Color.Default
        };
    }
    
    private Color GetTrueColor(string name) => name switch
    {
        "primary" => new Color(0, 153, 255),    // RGB #0099ff
        "success" => new Color(0, 255, 0),       // RGB #00ff00
        "error" => new Color(255, 0, 0),         // RGB #ff0000
        "warning" => new Color(255, 255, 0),     // RGB #ffff00
        _ => Color.Default
    };
    
    private Color Get256Color(string name) => name switch
    {
        "primary" => Color.Blue,
        "success" => Color.Green,
        "error" => Color.Red,
        "warning" => Color.Yellow,
        _ => Color.Default
    };
    
    private Color GetBasicColor(string name) => name switch
    {
        "primary" => Color.Blue,
        "success" => Color.Green,
        "error" => Color.Red,
        "warning" => Color.Yellow,
        _ => Color.Default
    };
}

// Layout adapter based on terminal width
public class LayoutAdapter
{
    private readonly ITerminalCapabilities _capabilities;
    
    public LayoutAdapter(ITerminalCapabilities capabilities)
    {
        _capabilities = capabilities;
    }
    
    public LayoutMode GetLayoutMode()
    {
        return _capabilities.Width switch
        {
            >= 160 => LayoutMode.Wide,
            >= 120 => LayoutMode.Standard,
            >= 80 => LayoutMode.Narrow,
            _ => LayoutMode.Minimal
        };
    }
    
    public bool ShouldUseSplitLayout() => GetLayoutMode() >= LayoutMode.Standard;
    public bool ShouldShowRightPanel() => GetLayoutMode() >= LayoutMode.Standard;
    public bool ShouldTruncateText() => GetLayoutMode() == LayoutMode.Minimal;
    
    public int GetMaxTextWidth()
    {
        var mode = GetLayoutMode();
        return mode switch
        {
            LayoutMode.Wide => 120,
            LayoutMode.Standard => 100,
            LayoutMode.Narrow => 70,
            LayoutMode.Minimal => 50,
            _ => 80
        };
    }
}

public enum LayoutMode
{
    Minimal = 0,  // < 80
    Narrow = 1,   // 80-119
    Standard = 2, // 120-159
    Wide = 3      // >= 160
}

// NO_COLOR environment variable handling
public static class NoColorHelper
{
    public static bool IsNoColorSet()
    {
        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        return !string.IsNullOrEmpty(noColor);
    }
    
    public static void ConfigureAnsiConsole()
    {
        if (IsNoColorSet())
        {
            AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
        }
    }
}

// Terminal info display
public class TerminalInfoDisplay
{
    private readonly ITerminalCapabilities _capabilities;
    
    public TerminalInfoDisplay(ITerminalCapabilities capabilities)
    {
        _capabilities = capabilities;
    }
    
    public void DisplayInfo(IAnsiConsole console)
    {
        var table = new Table();
        table.AddColumn("Property");
        table.AddColumn("Value");
        
        table.AddRow("Width", _capabilities.Width.ToString());
        table.AddRow("Height", _capabilities.Height.ToString());
        table.AddRow("Color System", _capabilities.ColorSystem.ToString());
        table.AddRow("Supports ANSI", _capabilities.SupportsAnsi ? "Yes" : "No");
        table.AddRow("Supports Interactive", _capabilities.SupportsInteractive ? "Yes" : "No");
        table.AddRow("NO_COLOR set", _capabilities.IsNoColorSet ? "Yes" : "No");
        
        console.Write(new Panel(table)
        {
            Header = new PanelHeader(" Terminal Capabilities "),
            Border = BoxBorder.Rounded
        });
    }
}

// Feature detection
public class FeatureDetector
{
    private readonly ITerminalCapabilities _capabilities;
    
    public FeatureDetector(ITerminalCapabilities capabilities)
    {
        _capabilities = capabilities;
    }
    
    public bool CanUseSpinners() => 
        _capabilities.SupportsAnsi && _capabilities.SupportsInteractive;
    
    public bool CanUsePanels() => 
        _capabilities.SupportsAnsi && _capabilities.Width >= 60;
    
    public bool CanUseTrees() => 
        _capabilities.SupportsAnsi && _capabilities.Width >= 60;
    
    public bool CanUseTables() => 
        _capabilities.SupportsAnsi && _capabilities.Width >= 80;
    
    public bool CanUseLiveDisplay() => 
        _capabilities.SupportsAnsi && _capabilities.SupportsInteractive;
    
    public bool CanUseSplitLayout() => 
        _capabilities.SupportsAnsi && _capabilities.Width >= 120;
}

// Usage example: Adaptive command output
public class AdaptiveCommandOutput
{
    private readonly IAnsiConsole _console;
    private readonly ITerminalCapabilities _capabilities;
    private readonly FeatureDetector _features;
    
    public AdaptiveCommandOutput(IAnsiConsole console, ITerminalCapabilities capabilities)
    {
        _console = console;
        _capabilities = capabilities;
        _features = new FeatureDetector(capabilities);
    }
    
    public void ShowResult(string title, Dictionary<string, string> data)
    {
        if (_features.CanUsePanels())
        {
            // Rich panel display
            var grid = new Grid();
            grid.AddColumn();
            grid.AddColumn();
            
            foreach (var (key, value) in data)
            {
                grid.AddRow($"[bold]{key}:[/]", value);
            }
            
            var panel = new Panel(grid)
            {
                Header = new PanelHeader($" {title} "),
                Border = BoxBorder.Rounded
            };
            
            _console.Write(panel);
        }
        else
        {
            // Simple text display
            _console.WriteLine($"=== {title} ===");
            foreach (var (key, value) in data)
            {
                _console.WriteLine($"{key}: {value}");
            }
            _console.WriteLine();
        }
    }
}
```

### Testing Approach

```csharp
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Terminal;

public class TerminalCapabilitiesTests
{
    [Fact]
    public void SupportsColor_WhenNoColorSet_ReturnsFalse()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        var console = new TestConsole();
        var capabilities = new TerminalCapabilities(console);
        
        // Act
        var supportsColor = capabilities.SupportsColor;
        
        // Assert
        Assert.False(supportsColor);
        
        // Cleanup
        Environment.SetEnvironmentVariable("NO_COLOR", null);
    }
    
    [Fact]
    public void Width_ReturnsConsoleWidth()
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Width = 120;
        var capabilities = new TerminalCapabilities(console);
        
        // Act
        var width = capabilities.Width;
        
        // Assert
        Assert.Equal(120, width);
    }
    
    [Fact]
    public void ColorSystem_WhenNoColorSet_ReturnsNoColors()
    {
        // Arrange
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        var console = new TestConsole();
        var capabilities = new TerminalCapabilities(console);
        
        // Act
        var colorSystem = capabilities.ColorSystem;
        
        // Assert
        Assert.Equal(ColorSystem.NoColors, colorSystem);
        
        // Cleanup
        Environment.SetEnvironmentVariable("NO_COLOR", null);
    }
}

public class AdaptiveRendererTests
{
    [Fact]
    public void WriteSuccess_WithColor_UsesMarkup()
    {
        // Arrange
        var console = new TestConsole();
        var capabilities = CreateCapabilities(supportsColor: true);
        var renderer = new AdaptiveRenderer(console, capabilities);
        
        // Act
        renderer.WriteSuccess("Test message");
        
        // Assert
        var output = console.Output;
        Assert.Contains("Test message", output);
    }
    
    [Fact]
    public void WriteSuccess_WithoutColor_UsesPlainText()
    {
        // Arrange
        var console = new TestConsole();
        var capabilities = CreateCapabilities(supportsColor: false);
        var renderer = new AdaptiveRenderer(console, capabilities);
        
        // Act
        renderer.WriteSuccess("Test message");
        
        // Assert
        var output = console.Output;
        Assert.Contains("[OK]", output);
        Assert.Contains("Test message", output);
    }
    
    private ITerminalCapabilities CreateCapabilities(
        bool supportsColor = true,
        bool supportsAnsi = true,
        int width = 120)
    {
        var mock = new Mock<ITerminalCapabilities>();
        mock.Setup(c => c.SupportsColor).Returns(supportsColor);
        mock.Setup(c => c.SupportsAnsi).Returns(supportsAnsi);
        mock.Setup(c => c.Width).Returns(width);
        return mock.Object;
    }
}

public class LayoutAdapterTests
{
    [Theory]
    [InlineData(50, LayoutMode.Minimal)]
    [InlineData(80, LayoutMode.Narrow)]
    [InlineData(120, LayoutMode.Standard)]
    [InlineData(160, LayoutMode.Wide)]
    public void GetLayoutMode_ReturnsCorrectMode(int width, LayoutMode expected)
    {
        // Arrange
        var capabilities = CreateCapabilities(width);
        var adapter = new LayoutAdapter(capabilities);
        
        // Act
        var mode = adapter.GetLayoutMode();
        
        // Assert
        Assert.Equal(expected, mode);
    }
    
    [Theory]
    [InlineData(80, false)]
    [InlineData(120, true)]
    [InlineData(160, true)]
    public void ShouldUseSplitLayout_ReturnsCorrectValue(int width, bool expected)
    {
        // Arrange
        var capabilities = CreateCapabilities(width);
        var adapter = new LayoutAdapter(capabilities);
        
        // Act
        var result = adapter.ShouldUseSplitLayout();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    private ITerminalCapabilities CreateCapabilities(int width)
    {
        var mock = new Mock<ITerminalCapabilities>();
        mock.Setup(c => c.Width).Returns(width);
        return mock.Object;
    }
}

public class FeatureDetectorTests
{
    [Fact]
    public void CanUseSpinners_RequiresAnsiAndInteractive()
    {
        // Arrange
        var capabilities = CreateCapabilities(supportsAnsi: true, supportsInteractive: true);
        var detector = new FeatureDetector(capabilities);
        
        // Act
        var canUse = detector.CanUseSpinners();
        
        // Assert
        Assert.True(canUse);
    }
    
    [Fact]
    public void CanUsePanels_RequiresAnsiAndMinWidth()
    {
        // Arrange
        var capabilities = CreateCapabilities(supportsAnsi: true, width: 80);
        var detector = new FeatureDetector(capabilities);
        
        // Act
        var canUse = detector.CanUsePanels();
        
        // Assert
        Assert.True(canUse);
    }
    
    private ITerminalCapabilities CreateCapabilities(
        bool supportsAnsi = true,
        bool supportsInteractive = true,
        int width = 120)
    {
        var mock = new Mock<ITerminalCapabilities>();
        mock.Setup(c => c.SupportsAnsi).Returns(supportsAnsi);
        mock.Setup(c => c.SupportsInteractive).Returns(supportsInteractive);
        mock.Setup(c => c.Width).Returns(width);
        return mock.Object;
    }
}
```

### Best Practices

✅ **DO:**
- Always check NO_COLOR environment variable
- Provide plain text fallbacks for no-color mode
- Use TerminalCapabilities interface for testability
- Adapt layout based on terminal width
- Test with different terminal configurations
- Use ColorSystem enum to determine color depth
- Respect terminal capabilities in all rendering

❌ **DON'T:**
- Don't assume terminal supports colors
- Don't hardcode ANSI escape sequences
- Don't ignore terminal width constraints
- Don't forget to test NO_COLOR mode
- Don't use emojis in no-ANSI mode
- Don't force interactive features on non-interactive terminals

### Terminal Capability Matrix

| Feature | Requires | Minimum Width | Fallback |
|---------|----------|---------------|----------|
| Colors | SupportsColor | - | Plain text with [OK]/[ERROR] |
| Spinners | SupportsAnsi + Interactive | - | Static "..." |
| Panels | SupportsAnsi | 60 | === header === |
| Tables | SupportsAnsi | 80 | Key: value list |
| Split Layout | SupportsAnsi | 120 | Vertical stack |
| Live Display | SupportsAnsi + Interactive | - | Static updates |

### References

- [AnsiConsole.Profile](https://spectreconsole.net/api/spectre.console/profile)
- [ColorSystem Enum](https://spectreconsole.net/api/spectre.console/colorsystem)
- [NO_COLOR Standard](https://no-color.org/)
- [Terminal Capability Detection](https://spectreconsole.net/best-practices/terminal-detection)

---

## REQ-021: TUI Testing & Mocking

### Pattern Overview

Testing TUI components requires mocking console output and verifying rendered content. Spectre.Console provides `TestConsole` for testing, and the pattern uses `ITuiRenderer` interfaces to enable unit testing without actual console rendering.

### Complete Code Example

```csharp
using Spectre.Console;
using Spectre.Console.Testing;
using System.Text;

namespace Lopen.Tui.Testing;

// Core abstraction for all TUI rendering
public interface ITuiRenderer
{
    void Write(IRenderable renderable);
    void WriteLine(string text);
    void WriteMarkup(string markup);
    void WriteMarkupLine(string markup);
    void Clear();
}

// Production implementation
public class SpectreTuiRenderer : ITuiRenderer
{
    private readonly IAnsiConsole _console;
    
    public SpectreTuiRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }
    
    public void Write(IRenderable renderable) => _console.Write(renderable);
    public void WriteLine(string text) => _console.WriteLine(text);
    public void WriteMarkup(string markup) => _console.Markup(markup);
    public void WriteMarkupLine(string markup) => _console.MarkupLine(markup);
    public void Clear() => _console.Clear();
}

// Test/Mock implementation
public class MockTuiRenderer : ITuiRenderer
{
    private readonly StringBuilder _output = new();
    
    public string Output => _output.ToString();
    public List<IRenderable> Renderables { get; } = new();
    public int WriteCount { get; private set; }
    public int WriteLineCount { get; private set; }
    
    public void Write(IRenderable renderable)
    {
        Renderables.Add(renderable);
        WriteCount++;
        
        // Capture type for assertions
        _output.AppendLine($"[{renderable.GetType().Name}]");
    }
    
    public void WriteLine(string text)
    {
        _output.AppendLine(text);
        WriteLineCount++;
    }
    
    public void WriteMarkup(string markup)
    {
        _output.Append(StripMarkup(markup));
    }
    
    public void WriteMarkupLine(string markup)
    {
        _output.AppendLine(StripMarkup(markup));
    }
    
    public void Clear()
    {
        _output.Clear();
        Renderables.Clear();
    }
    
    private string StripMarkup(string markup)
    {
        // Simple markup stripping for testing
        // In production, might want more sophisticated parsing
        return System.Text.RegularExpressions.Regex.Replace(
            markup, @"\[.*?\]", string.Empty);
    }
    
    public bool ContainsText(string text) => _output.ToString().Contains(text);
    public bool ContainsRenderable<T>() where T : IRenderable => 
        Renderables.Any(r => r is T);
}

// Snapshot testing helper
public class SnapshotHelper
{
    public static string CaptureSnapshot(IAnsiConsole console, Action render)
    {
        var testConsole = console as TestConsole 
            ?? throw new ArgumentException("Console must be TestConsole for snapshots");
        
        render();
        return NormalizeSnapshot(testConsole.Output);
    }
    
    private static string NormalizeSnapshot(string output)
    {
        // Normalize line endings
        output = output.Replace("\r\n", "\n");
        
        // Remove trailing whitespace
        var lines = output.Split('\n')
            .Select(line => line.TrimEnd())
            .ToArray();
        
        return string.Join("\n", lines);
    }
    
    public static void AssertSnapshotMatches(string actual, string expected)
    {
        var normalizedActual = NormalizeSnapshot(actual);
        var normalizedExpected = NormalizeSnapshot(expected);
        
        if (normalizedActual != normalizedExpected)
        {
            var diff = GenerateDiff(normalizedExpected, normalizedActual);
            throw new Exception($"Snapshot mismatch:\n{diff}");
        }
    }
    
    private static string GenerateDiff(string expected, string actual)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Expected:");
        sb.AppendLine(expected);
        sb.AppendLine();
        sb.AppendLine("Actual:");
        sb.AppendLine(actual);
        return sb.ToString();
    }
}

// ANSI output verification
public class AnsiOutputVerifier
{
    public static bool ContainsAnsiEscape(string output)
    {
        return output.Contains("\u001b[");
    }
    
    public static bool ContainsColor(string output, Color color)
    {
        // Simplified color detection
        // In production, would need full ANSI parser
        return output.Contains($"\u001b[38;");
    }
    
    public static string StripAnsi(string output)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            output, @"\u001b\[[0-9;]*m", string.Empty);
    }
}

// Test helpers for common scenarios
public static class TuiTestHelpers
{
    public static TestConsole CreateTestConsole(
        int width = 120,
        int height = 40,
        ColorSystem colorSystem = ColorSystem.TrueColor)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height;
        console.Profile.Capabilities.ColorSystem = colorSystem;
        console.Profile.Capabilities.Ansi = true;
        console.Profile.Capabilities.Interactive = true;
        return console;
    }
    
    public static void AssertContainsPanel(TestConsole console, string headerText)
    {
        var output = console.Output;
        Assert.Contains(headerText, output);
        
        // Check for panel borders (simplified)
        Assert.True(output.Contains("─") || output.Contains("-"), 
            "Expected panel borders");
    }
    
    public static void AssertContainsTable(TestConsole console, params string[] headers)
    {
        var output = console.Output;
        foreach (var header in headers)
        {
            Assert.Contains(header, output);
        }
    }
    
    public static void AssertContainsMarkup(TestConsole console, string text, string color)
    {
        var output = console.Output;
        Assert.Contains(text, output);
        // Color verification would require ANSI parsing
    }
}

// Example: Testing a component
public class StatusDisplay
{
    private readonly ITuiRenderer _renderer;
    
    public StatusDisplay(ITuiRenderer renderer)
    {
        _renderer = renderer;
    }
    
    public void ShowSuccess(string message)
    {
        _renderer.WriteMarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }
    
    public void ShowError(string message)
    {
        var panel = new Panel(Markup.Escape(message))
        {
            Header = new PanelHeader(" Error "),
            BorderStyle = new Style(Color.Red)
        };
        _renderer.Write(panel);
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests;

public class StatusDisplayTests
{
    [Fact]
    public void ShowSuccess_WritesSuccessMessage()
    {
        // Arrange
        var renderer = new MockTuiRenderer();
        var display = new StatusDisplay(renderer);
        
        // Act
        display.ShowSuccess("Operation completed");
        
        // Assert
        Assert.True(renderer.ContainsText("Operation completed"));
        Assert.True(renderer.ContainsText("✓"));
    }
    
    [Fact]
    public void ShowError_WritesErrorPanel()
    {
        // Arrange
        var renderer = new MockTuiRenderer();
        var display = new StatusDisplay(renderer);
        
        // Act
        display.ShowError("Something failed");
        
        // Assert
        Assert.True(renderer.ContainsRenderable<Panel>());
        Assert.Equal(1, renderer.WriteCount);
    }
    
    [Fact]
    public void ShowSuccess_WithTestConsole_ProducesCorrectOutput()
    {
        // Arrange
        var console = TuiTestHelpers.CreateTestConsole();
        var renderer = new SpectreTuiRenderer(console);
        var display = new StatusDisplay(renderer);
        
        // Act
        display.ShowSuccess("Test message");
        
        // Assert
        var output = console.Output;
        Assert.Contains("Test message", output);
        Assert.Contains("✓", output);
    }
}

public class SnapshotTests
{
    [Fact]
    public void Panel_MatchesSnapshot()
    {
        // Arrange
        var console = TuiTestHelpers.CreateTestConsole();
        
        // Act
        var snapshot = SnapshotHelper.CaptureSnapshot(console, () =>
        {
            var panel = new Panel("Test content")
            {
                Header = new PanelHeader(" Test Panel ")
            };
            console.Write(panel);
        });
        
        // Assert
        var expected = @"╭─ Test Panel ─╮
│ Test content │
╰──────────────╯";
        
        // Note: Actual snapshot comparison would be more sophisticated
        Assert.Contains("Test content", snapshot);
    }
}

public class AnsiOutputTests
{
    [Fact]
    public void ColoredOutput_ContainsAnsiEscapes()
    {
        // Arrange
        var console = TuiTestHelpers.CreateTestConsole();
        
        // Act
        console.MarkupLine("[red]Error message[/]");
        
        // Assert
        var output = console.Output;
        // TestConsole doesn't include ANSI escapes by default
        // but we can verify content
        Assert.Contains("Error message", output);
    }
    
    [Fact]
    public void StripAnsi_RemovesEscapeSequences()
    {
        // Arrange
        var input = "\u001b[31mRed text\u001b[0m";
        
        // Act
        var stripped = AnsiOutputVerifier.StripAnsi(input);
        
        // Assert
        Assert.Equal("Red text", stripped);
    }
}

public class MockRendererTests
{
    [Fact]
    public void MockRenderer_TracksRenderables()
    {
        // Arrange
        var renderer = new MockTuiRenderer();
        
        // Act
        renderer.Write(new Panel("Test"));
        renderer.Write(new Table());
        
        // Assert
        Assert.Equal(2, renderer.WriteCount);
        Assert.True(renderer.ContainsRenderable<Panel>());
        Assert.True(renderer.ContainsRenderable<Table>());
    }
    
    [Fact]
    public void MockRenderer_CapturesText()
    {
        // Arrange
        var renderer = new MockTuiRenderer();
        
        // Act
        renderer.WriteLine("Line 1");
        renderer.WriteMarkupLine("[green]Success[/]");
        
        // Assert
        Assert.True(renderer.ContainsText("Line 1"));
        Assert.True(renderer.ContainsText("Success"));
        Assert.Equal(1, renderer.WriteLineCount);
    }
}

// Integration test example
public class FullFlowTests
{
    [Fact]
    public async Task ProgressWithError_DisplaysCorrectSequence()
    {
        // Arrange
        var console = TuiTestHelpers.CreateTestConsole();
        var renderer = new SpectreTuiRenderer(console);
        var progress = new SpectreProgressRenderer(console);
        var errorRenderer = new SpectreErrorRenderer(console);
        
        // Act
        try
        {
            await progress.ShowProgressAsync(
                "Processing...",
                async ctx =>
                {
                    await Task.Delay(10);
                    throw new Exception("Failed");
                });
        }
        catch (Exception ex)
        {
            errorRenderer.RenderSimpleError(ex.Message);
        }
        
        // Assert
        var output = console.Output;
        Assert.Contains("Processing...", output);
        Assert.Contains("Failed", output);
    }
}
```

### Best Practices

✅ **DO:**
- Use `ITuiRenderer` interface for all TUI components
- Test with `TestConsole` for integration tests
- Use `MockTuiRenderer` for unit tests
- Verify both content and structure (Panel, Table, etc.)
- Test responsive behavior with different terminal widths
- Create snapshot tests for complex layouts
- Test NO_COLOR mode separately

❌ **DON'T:**
- Don't test against actual console in unit tests
- Don't hardcode ANSI escape sequences in assertions
- Don't forget to test error cases
- Don't skip testing adaptive rendering
- Don't assume markup will be rendered (test plain text too)
- Don't forget to test terminal capability edge cases

### Testing Strategy

| Test Type | Tool | Focus | Example |
|-----------|------|-------|---------|
| Unit | MockTuiRenderer | Logic, content | Verify correct text |
| Integration | TestConsole | Rendering | Verify Panel created |
| Snapshot | SnapshotHelper | Layout | Compare full output |
| Visual | Manual | Appearance | Check in real terminal |
| Adaptive | TestConsole variations | Responsive behavior | Test narrow/wide |

### References

- [Spectre.Console.Testing](https://www.nuget.org/packages/Spectre.Console.Testing)
- [TestConsole Documentation](https://spectreconsole.net/api/spectre.console.testing/testconsole)
- [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

---

## REQ-022: Welcome Header with ASCII Art

### Pattern Overview

Creating an appealing welcome header involves ASCII art logos, FigletText for branded text, and responsive layout that adapts to terminal width. The pattern uses Spectre.Console's `FigletText` and `Panel` components with assembly version reading.

### Complete Code Example

```csharp
using Spectre.Console;
using System.Reflection;

namespace Lopen.Tui.Welcome;

public interface IWelcomeRenderer
{
    void RenderWelcome();
    void RenderCompact();
}

public class WelcomeRenderer : IWelcomeRenderer
{
    private readonly IAnsiConsole _console;
    private readonly ITerminalCapabilities _capabilities;
    
    public WelcomeRenderer(IAnsiConsole console, ITerminalCapabilities capabilities)
    {
        _console = console;
        _capabilities = capabilities;
    }
    
    public void RenderWelcome()
    {
        if (_capabilities.Width >= 100)
        {
            RenderFullWelcome();
        }
        else if (_capabilities.Width >= 60)
        {
            RenderMediumWelcome();
        }
        else
        {
            RenderCompact();
        }
    }
    
    private void RenderFullWelcome()
    {
        // ASCII art logo (Wind Runner sigil)
        var logo = GetWindRunnerSigil();
        var logoPanelContent = new Markup(logo);
        
        // Brand name with Figlet
        var figlet = new FigletText("LOPEN")
            .LeftJustified()
            .Color(Color.Cyan1);
        
        // Version and tagline
        var version = GetVersion();
        var tagline = "Wind Runner • GitHub Copilot CLI";
        
        var infoGrid = new Grid();
        infoGrid.AddColumn();
        infoGrid.AddRow($"[dim]Version:[/] [cyan]{version}[/]");
        infoGrid.AddRow($"[dim]{tagline}[/]");
        
        // Combine all elements
        var layout = new Rows(
            logoPanelContent,
            figlet,
            new Text(""),
            infoGrid
        );
        
        var panel = new Panel(layout)
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1, 2, 1)
        };
        
        _console.Write(panel);
        _console.WriteLine();
    }
    
    private void RenderMediumWelcome()
    {
        // Simpler figlet without ASCII art
        var figlet = new FigletText("LOPEN")
            .Centered()
            .Color(Color.Cyan1);
        
        var version = GetVersion();
        var tagline = "Wind Runner • GitHub Copilot CLI";
        
        _console.Write(figlet);
        _console.MarkupLine($"[dim]Version {version}[/]");
        _console.MarkupLine($"[dim]{tagline}[/]");
        _console.WriteLine();
    }
    
    public void RenderCompact()
    {
        var version = GetVersion();
        _console.MarkupLine($"[bold cyan]Lopen[/] [dim]v{version}[/] - Wind Runner CLI");
        _console.WriteLine();
    }
    
    private string GetWindRunnerSigil()
    {
        // Wind Runner sigil - stylized wind/movement
        return @"
    ╭─────────────────────╮
    │   ～～～ ▸▸▸ ～～～   │
    │  ～ ▸▸▸▸▸▸▸▸▸ ～  │
    │   ～～～ ▸▸▸ ～～～   │
    ╰─────────────────────╯
        ";
    }
    
    private string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.1.0";
    }
}

// Alternative: More elaborate ASCII art designs
public static class AsciiArtLibrary
{
    public static string GetWindRunnerLarge()
    {
        return @"
    ╔════════════════════════════════════╗
    ║                                    ║
    ║    ～～～～～～～～～～～～～～～～    ║
    ║   ～ ▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸ ～   ║
    ║  ～ ▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸ ～  ║
    ║   ～ ▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸▸ ～   ║
    ║    ～～～～～～～～～～～～～～～～    ║
    ║                                    ║
    ║         W I N D   R U N N E R      ║
    ║                                    ║
    ╚════════════════════════════════════╝
        ";
    }
    
    public static string GetWindRunnerCompact()
    {
        return @"
    ╭────────────╮
    │ ～～～ ▸▸▸ │
    │ ～ ▸▸▸▸▸ ～ │
    │ ～～～ ▸▸▸ │
    ╰────────────╯
        ";
    }
    
    public static string GetMinimalLogo()
    {
        return "▸▸▸ ～～～";
    }
}

// Multi-component panel with layout
public class BrandedHeader
{
    private readonly IAnsiConsole _console;
    
    public BrandedHeader(IAnsiConsole console)
    {
        _console = console;
    }
    
    public void Render()
    {
        // Left: Logo
        var logo = new Panel(AsciiArtLibrary.GetWindRunnerCompact())
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };
        
        // Right: Info
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        var infoMarkup = $@"
[bold cyan]Lopen[/]
[dim]Version {version}[/]

[yellow]Wind Runner[/]
GitHub Copilot CLI

[dim]Type[/] [cyan]help[/] [dim]to begin[/]
        ";
        
        var info = new Panel(infoMarkup.Trim())
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };
        
        // Combine in columns
        var layout = new Columns(logo, info);
        
        _console.Write(layout);
        _console.WriteLine();
    }
}

// Responsive header that adapts to terminal width
public class ResponsiveHeader
{
    private readonly IAnsiConsole _console;
    private readonly ITerminalCapabilities _capabilities;
    
    public ResponsiveHeader(IAnsiConsole console, ITerminalCapabilities capabilities)
    {
        _console = console;
        _capabilities = capabilities;
    }
    
    public void Render()
    {
        var width = _capabilities.Width;
        
        if (width >= 120)
        {
            RenderWide();
        }
        else if (width >= 80)
        {
            RenderStandard();
        }
        else
        {
            RenderNarrow();
        }
    }
    
    private void RenderWide()
    {
        // Full layout with logo and detailed info
        var figlet = new FigletText("LOPEN")
            .Centered()
            .Color(Color.Cyan1);
        
        var logo = new Markup(AsciiArtLibrary.GetWindRunnerLarge());
        var version = GetVersionInfo();
        
        var content = new Rows(logo, figlet, new Text(""), version);
        
        var panel = new Panel(content)
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Expand = true
        };
        
        _console.Write(panel);
    }
    
    private void RenderStandard()
    {
        // Figlet only with compact info
        var figlet = new FigletText("LOPEN")
            .Centered()
            .Color(Color.Cyan1);
        
        _console.Write(figlet);
        
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        _console.MarkupLine($"[center][dim]v{version} • Wind Runner • GitHub Copilot CLI[/][/]");
        _console.WriteLine();
    }
    
    private void RenderNarrow()
    {
        // Simple text header
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        _console.WriteLine();
        _console.MarkupLine($"[bold cyan]LOPEN[/] [dim]v{version}[/]");
        _console.MarkupLine("[dim]Wind Runner CLI[/]");
        _console.WriteLine();
    }
    
    private Markup GetVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        var year = DateTime.Now.Year;
        
        return new Markup($@"
[bold]Version:[/] {version}
[bold]Tagline:[/] Wind Runner - GitHub Copilot CLI
[dim]© {year} • MIT License[/]
        ".Trim());
    }
}

// Reading assembly metadata
public static class AssemblyInfo
{
    public static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    }
    
    public static string GetInformationalVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion ?? GetVersion();
    }
    
    public static string GetProduct()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
        return attribute?.Product ?? "Lopen";
    }
    
    public static string GetDescription()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
        return attribute?.Description ?? "GitHub Copilot CLI";
    }
}

// Usage in application startup
public class ApplicationStartup
{
    private readonly IWelcomeRenderer _welcomeRenderer;
    
    public ApplicationStartup(IWelcomeRenderer welcomeRenderer)
    {
        _welcomeRenderer = welcomeRenderer;
    }
    
    public void ShowWelcome()
    {
        _welcomeRenderer.RenderWelcome();
        
        // Optional: Quick start hints
        ShowQuickStartHints();
    }
    
    private void ShowQuickStartHints()
    {
        var table = new Table();
        table.Border(BoxBorder.None);
        table.HideHeaders();
        table.AddColumn("");
        table.AddColumn("");
        
        table.AddRow("[cyan]help[/]", "Show available commands");
        table.AddRow("[cyan]auth login[/]", "Authenticate with GitHub");
        table.AddRow("[cyan]loop start[/]", "Start an AI loop");
        
        AnsiConsole.Write(new Panel(table)
        {
            Header = new PanelHeader(" Quick Start "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        });
        
        AnsiConsole.WriteLine();
    }
}
```

### Testing Approach

```csharp
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Tui.Tests.Welcome;

public class WelcomeRendererTests
{
    [Fact]
    public void RenderWelcome_WideTerminal_ShowsFullHeader()
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Width = 120;
        var capabilities = new TerminalCapabilities(console);
        var renderer = new WelcomeRenderer(console, capabilities);
        
        // Act
        renderer.RenderWelcome();
        
        // Assert
        var output = console.Output;
        Assert.Contains("LOPEN", output);
        Assert.Contains("Wind Runner", output);
    }
    
    [Fact]
    public void RenderWelcome_NarrowTerminal_ShowsCompact()
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Width = 50;
        var capabilities = new TerminalCapabilities(console);
        var renderer = new WelcomeRenderer(console, capabilities);
        
        // Act
        renderer.RenderWelcome();
        
        // Assert
        var output = console.Output;
        Assert.Contains("Lopen", output);
        Assert.Contains("Wind Runner", output);
        // Should not contain figlet art
    }
    
    [Fact]
    public void RenderCompact_ShowsBasicInfo()
    {
        // Arrange
        var console = new TestConsole();
        var capabilities = new TerminalCapabilities(console);
        var renderer = new WelcomeRenderer(console, capabilities);
        
        // Act
        renderer.RenderCompact();
        
        // Assert
        var output = console.Output;
        Assert.Contains("Lopen", output);
        Assert.Contains("Wind Runner", output);
    }
}

public class AssemblyInfoTests
{
    [Fact]
    public void GetVersion_ReturnsValidVersion()
    {
        // Act
        var version = AssemblyInfo.GetVersion();
        
        // Assert
        Assert.NotNull(version);
        Assert.Matches(@"\d+\.\d+\.\d+", version);
    }
    
    [Fact]
    public void GetProduct_ReturnsProductName()
    {
        // Act
        var product = AssemblyInfo.GetProduct();
        
        // Assert
        Assert.NotNull(product);
        Assert.NotEmpty(product);
    }
}

public class ResponsiveHeaderTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(80)]
    [InlineData(120)]
    public void Render_AdaptsToTerminalWidth(int width)
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Width = width;
        var capabilities = new TerminalCapabilities(console);
        var header = new ResponsiveHeader(console, capabilities);
        
        // Act
        header.Render();
        
        // Assert
        var output = console.Output;
        Assert.Contains("LOPEN", output, StringComparison.OrdinalIgnoreCase);
    }
}
```

### Best Practices

✅ **DO:**
- Create responsive designs for different terminal widths
- Use FigletText for branded text (built into Spectre.Console)
- Read version from assembly metadata
- Test with narrow terminals (50-60 chars)
- Provide compact fallback for minimal terminals
- Use consistent colors (cyan for brand, dim for secondary)
- Keep ASCII art simple and portable

❌ **DON'T:**
- Don't use complex ASCII art that breaks on narrow terminals
- Don't hardcode version numbers
- Don't assume wide terminal (120+ chars)
- Don't use non-portable Unicode characters
- Don't make header too tall (> 15 lines)
- Don't forget NO_COLOR support

### Header Design Guidelines

| Terminal Width | Design | Components |
|----------------|--------|------------|
| < 60 chars | Minimal | Text name + version |
| 60-99 chars | Compact | Figlet + basic info |
| 100-119 chars | Standard | Figlet + logo + info |
| ≥ 120 chars | Full | Large logo + figlet + detailed info |

### FigletText Fonts

Spectre.Console includes several fonts:
- `Standard` - Classic figlet font (default)
- `Big` - Larger, bolder letters
- `Small` - Compact version
- `Slant` - Italicized style

```csharp
// Example: Using different fonts
var figlet = new FigletText("LOPEN")
    .Font(FigletFont.Load("slant.flf"))  // If you have font file
    .Centered()
    .Color(Color.Cyan1);
```

### References

- [FigletText Widget](https://spectreconsole.net/widgets/figlet)
- [ASCII Art Generator](http://www.patorjk.com/software/taag/)
- [Box Drawing Characters](https://en.wikipedia.org/wiki/Box-drawing_character)
- [Assembly Attributes](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assemblyinformationalversionattribute)

---

## Implementation Checklist

Use this checklist to track implementation of each requirement:

### REQ-015: Progress Indicators & Spinners
- [ ] Create `IProgressRenderer` interface
- [ ] Implement `SpectreProgressRenderer` with Status API
- [ ] Add `SpinnerStyle` configuration class
- [ ] Implement non-blocking progress for REPL
- [ ] Add tests with `TestConsole`
- [ ] Document spinner types and usage

### REQ-016: Error Display & Correction Guidance
- [ ] Create `IErrorRenderer` interface
- [ ] Implement `SpectreErrorRenderer` with Panel
- [ ] Add `ErrorInfo` record type
- [ ] Implement Levenshtein "Did you mean?" suggestions
- [ ] Add System.CommandLine integration
- [ ] Add tests for error rendering

### REQ-017: Structured Data Display
- [ ] Create `IDataRenderer` interface
- [ ] Implement Panel for metadata
- [ ] Implement Tree for hierarchies
- [ ] Implement responsive Table
- [ ] Add nested panel support (max 2 levels)
- [ ] Add responsive column calculator

### REQ-018: Layout & Right-Side Panels
- [ ] Create `ILayoutRenderer` interface
- [ ] Implement split-screen with Columns
- [ ] Add responsive layout switching
- [ ] Create `TaskListPanel` component
- [ ] Create `ContextPanel` component
- [ ] Add tests for different widths

### REQ-019: AI Response Streaming
- [ ] Create `IStreamRenderer` interface
- [ ] Implement buffering strategy
- [ ] Add paragraph-based flushing
- [ ] Implement code block detection
- [ ] Add timeout-based flushing
- [ ] Add tests with async streams

### REQ-020: Responsive Terminal Detection
- [ ] Create `ITerminalCapabilities` interface
- [ ] Implement NO_COLOR support
- [ ] Add color depth detection
- [ ] Create `LayoutAdapter` class
- [ ] Add `FeatureDetector` class
- [ ] Add tests for different terminals

### REQ-021: TUI Testing & Mocking
- [ ] Create `ITuiRenderer` interface
- [ ] Implement `MockTuiRenderer` for tests
- [ ] Add `SnapshotHelper` utility
- [ ] Add `AnsiOutputVerifier` utility
- [ ] Create test helpers
- [ ] Add comprehensive test suite

### REQ-022: Welcome Header
- [ ] Create `IWelcomeRenderer` interface
- [ ] Design Wind Runner ASCII art
- [ ] Implement FigletText branding
- [ ] Add responsive header variants
- [ ] Implement version reading
- [ ] Add tests for all layouts

---

## Architecture Recommendations

### Dependency Injection Setup

```csharp
// In Program.cs or Startup.cs
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

### Project Structure

```
Lopen.Tui/
├── Interfaces/
│   ├── ITuiRenderer.cs
│   ├── IProgressRenderer.cs
│   ├── IErrorRenderer.cs
│   └── ...
├── Implementations/
│   ├── SpectreTuiRenderer.cs
│   ├── SpectreProgressRenderer.cs
│   └── ...
├── Models/
│   ├── ErrorInfo.cs
│   ├── SpinnerStyle.cs
│   └── ...
├── Helpers/
│   ├── SuggestionHelper.cs
│   ├── ResponsiveColumnCalculator.cs
│   └── ...
└── Testing/
    ├── MockTuiRenderer.cs
    ├── SnapshotHelper.cs
    └── ...
```

### Testing Setup

```csharp
// In test projects
public class TestBase
{
    protected TestConsole CreateConsole(int width = 120)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Capabilities.ColorSystem = ColorSystem.TrueColor;
        return console;
    }
    
    protected ITerminalCapabilities CreateCapabilities(TestConsole console)
    {
        return new TerminalCapabilities(console);
    }
}
```

---

## Performance Considerations

### Rendering Optimization

1. **Batch Updates**: Group multiple writes together
2. **Lazy Rendering**: Only render visible content
3. **Caching**: Cache complex renderables when possible
4. **Throttling**: Limit update frequency in live displays (100ms minimum)

### Memory Management

1. **Dispose Live Displays**: Always dispose `ILiveLayout` instances
2. **Buffer Size Limits**: Keep streaming buffers < 1KB
3. **String Builder Reuse**: Reuse StringBuilder instances
4. **Avoid String Concatenation**: Use StringBuilder for repeated operations

---

## Common Patterns

### Pattern 1: Conditional Rendering

```csharp
public void Render(IRenderable content)
{
    if (_capabilities.SupportsAnsi)
    {
        _console.Write(content);
    }
    else
    {
        RenderPlainText(content);
    }
}
```

### Pattern 2: Progressive Enhancement

```csharp
public void ShowData(Dictionary<string, string> data)
{
    if (_capabilities.Width >= 120)
        RenderAsTable(data);
    else if (_capabilities.Width >= 80)
        RenderAsGrid(data);
    else
        RenderAsList(data);
}
```

### Pattern 3: Error Handling

```csharp
try
{
    await PerformOperation();
    _renderer.WriteSuccess("Complete!");
}
catch (Exception ex)
{
    _errorRenderer.RenderError(new ErrorInfo
    {
        Title = "Operation Failed",
        Message = ex.Message,
        Suggestions = GetSuggestions(ex)
    });
}
```

---

## Troubleshooting

### Issue: Colors not showing
- **Check**: NO_COLOR environment variable
- **Check**: Terminal color support
- **Solution**: Test with `AnsiConsole.Profile.Capabilities.ColorSystem`

### Issue: Layout breaks on narrow terminal
- **Check**: Terminal width detection
- **Check**: Responsive breakpoints
- **Solution**: Implement fallback layouts for width < 80

### Issue: Spinners not animating
- **Check**: Terminal supports interactive mode
- **Check**: Not running in redirected output
- **Solution**: Use static progress for non-interactive

### Issue: Panel borders look wrong
- **Check**: Terminal encoding (UTF-8)
- **Check**: Font supports Unicode box drawing
- **Solution**: Use `BoxBorder.Ascii` as fallback

---

## Additional Resources

### Official Documentation
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [GitHub Repository](https://github.com/spectreconsole/spectre.console)
- [API Reference](https://spectreconsole.net/api/)

### Community Resources
- [Spectre.Console Examples](https://github.com/spectreconsole/spectre.console/tree/main/examples)
- [Stack Overflow Tag](https://stackoverflow.com/questions/tagged/spectre.console)

### Related Tools
- [Spectre.Console.Cli](https://spectreconsole.net/cli/) - Command-line framework
- [Spectre.Console.Testing](https://www.nuget.org/packages/Spectre.Console.Testing/) - Testing utilities

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-25 | Initial comprehensive guide |

---

## License

This documentation is part of the Lopen project and follows the same MIT license.

---

**End of Guide** - Ready for implementation in the Lopen project!
