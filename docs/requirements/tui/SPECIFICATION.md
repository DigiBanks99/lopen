# Terminal UI - Specification

> Comprehensive TUI guidelines for human-first terminal experience using Spectre.Console

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-014 | Output Formatting & Status Indicators | High | 🟢 Complete |
| REQ-015 | Progress Indicators & Spinners | High | 🟡 Partial |
| REQ-016 | Error Display & Correction Guidance | High | 🔴 Planned |
| REQ-017 | Structured Data Display | Medium | 🟡 Partial |
| REQ-018 | Layout & Right-Side Panels | Medium | 🔴 Planned |
| REQ-019 | AI Response Streaming | High | 🔴 Planned |
| REQ-020 | Responsive Terminal Detection | Medium | 🔴 Planned |
| REQ-021 | TUI Testing & Mocking | High | 🟡 Partial |
| REQ-022 | Welcome Header & REPL Banner | High | 🔴 Planned |

---

## Design Principles

### Primary Use Case: Interactive REPL
- **REPL sessions**: Rich, full-featured TUI with colors, symbols, layouts, animations
- **CLI commands**: Simpler output, pipe-friendly, with `--json` flag for scripting
- **NO_COLOR**: Always respected for accessibility

### Human-First Design
- Meaningful visual hierarchy and grouping
- Clear status indicators and progress feedback
- Contextual error messages with correction guidance
- Adaptive layouts based on terminal capabilities

### Terminal Capabilities
- **Color depth**: Adaptive (16 colors → 256 → RGB/TrueColor)
- **Symbol set**: Emoji + Unicode (✓ ✗ ⚠ ℹ ⏳ ✨)
- **Width**: Responsive (detect and adapt, minimum 60 chars)
- **Features**: Full set (colors, symbols, layout, animations)

---

## REQ-014: Output Formatting & Status Indicators

### Description
Consistent output formatting with clear status indicators for all message types.

### Acceptance Criteria
- [x] Colored output for different message types (Success, Error, Warning, Info, Muted)
- [x] Unicode symbol support (✓ ✗ ⚠ ℹ)
- [x] NO_COLOR environment variable support
- [x] ConsoleOutput helper with standard methods
- [ ] Emoji support for enhanced visual feedback (⏳ ✨ 🚀 ⚡ 💡)
- [ ] Adaptive color depth detection

### Implemented Components

#### ConsoleOutput Helper
```csharp
ConsoleOutput.Success(message)  // ✓ Green checkmark + message
ConsoleOutput.Error(message)    // ✗ Red X + message
ConsoleOutput.Warning(message)  // ⚠ Yellow warning + message
ConsoleOutput.Info(message)     // ℹ Blue info + message
ConsoleOutput.Muted(message)    // Gray secondary text
ConsoleOutput.KeyValue(k, v)    // Bold key: value
```

### Color Palette

| Type | 16-color | 256-color | RGB | Usage |
|------|----------|-----------|-----|-------|
| Success | Green | `#00ff00` | `0,255,0` | Completed operations |
| Error | Red | `#ff0000` | `255,0,0` | Failures and critical errors |
| Warning | Yellow | `#ffff00` | `255,255,0` | Cautions and non-critical issues |
| Info | Blue | `#0099ff` | `0,153,255` | Informational messages |
| Muted | Gray | `#808080` | `128,128,128` | Secondary information |
| Highlight | Cyan | `#00ffff` | `0,255,255` | Emphasized content |
| Accent | Magenta | `#ff00ff` | `255,0,255` | Special markers |

### Symbol Standards

| Symbol | Unicode | Fallback | Context |
|--------|---------|----------|---------|
| ✓ | U+2713 | `[OK]` | Success, completed |
| ✗ | U+2717 | `[X]` | Error, failed |
| ⚠ | U+26A0 | `[!]` | Warning, caution |
| ℹ | U+2139 | `[i]` | Information |
| ⏳ | U+23F3 | `...` | In progress |
| ✨ | U+2728 | `*` | New, special |
| 🚀 | U+1F680 | `>>` | Launch, start |
| ⚡ | U+26A1 | `!` | Fast, important |
| 💡 | U+1F4A1 | `?` | Tip, suggestion |

### Indentation & Hierarchy

```
Main operation
  ├─ Sub-task 1
  │  └─ Detail
  ├─ Sub-task 2
  └─ Sub-task 3
```

Use Spectre.Console `Tree` component for hierarchical data, or manual indentation with box-drawing characters.

---

## REQ-015: Progress Indicators & Spinners

### Description
Visual feedback for long-running operations using spinners (indeterminate) and progress bars (determinate).

### Acceptance Criteria
- [ ] Spinners for Copilot SDK calls (network/AI operations)
- [ ] Progress bars for batch operations with known count
- [ ] Live-updating status text during operations
- [ ] Spinner stops on completion/error with final status
- [ ] Non-blocking progress in REPL mode

### Usage Guidelines

#### When to Use Spinners
- Copilot SDK API calls (chat, model listing, session operations)
- Network requests (authentication, GitHub API)
- Any operation without known item count
- Operations expected to take > 1 second

#### When to Use Progress Bars
- Processing multiple files/items
- Batch operations with known total count
- Downloads with known size
- Multi-step workflows with defined steps

### Implementation Pattern

```csharp
// Spinner for indeterminate operations
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Connecting to Copilot...", async ctx => {
        var result = await copilotClient.ConnectAsync();
        ctx.Status("Processing response...");
        return result;
    });

// Progress bar for determinate operations
await AnsiConsole.Progress()
    .StartAsync(async ctx => {
        var task = ctx.AddTask("Processing files", maxValue: count);
        foreach (var item in items) {
            await ProcessItem(item);
            task.Increment(1);
        }
    });
```

### Spinner Types

| Spinner | Use Case |
|---------|----------|
| `Dots` | Default for most operations |
| `Line` | Fast operations |
| `SimpleDotsScrolling` | Long-running network calls |
| `Arc` | Heavy processing |

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-015-01 | SDK call shows spinner | Spinner visible, stops on completion |
| TC-015-02 | Batch operation shows progress | Progress bar 0-100% |
| TC-015-03 | Spinner on error | Stops with error status |
| TC-015-04 | NO_COLOR with spinner | Text-only status updates |

---

## REQ-016: Error Display & Correction Guidance

### Description
Clear, actionable error messages with contextual correction guidance using Spectre.Console rendering capabilities.

### Acceptance Criteria
- [ ] Structured error display with symbols and colors
- [ ] Contextual correction suggestions ("Did you mean...", "Try: lopen X")
- [ ] Error panels for complex/multi-line errors
- [ ] Stack traces only in debug/verbose mode
- [ ] Integration with System.CommandLine error handling

### Design Inspiration

Based on [Spectre.Console.Errata](https://github.com/spectreconsole/spectre.console) patterns:
- Source code context with highlights
- Inline annotations and suggestions
- Multi-line error explanations
- Suggestion panels

### Error Display Patterns

#### Simple Errors (Single Line)
```
✗ Authentication failed
  💡 Try: lopen auth login
```

#### Complex Errors (Panel)
```
╭─ Error: Invalid command ─────────────────────╮
│ Command 'chatr' not found                    │
│                                               │
│ Did you mean?                                 │
│   • chat                                      │
│   • repl                                      │
│                                               │
│ Run 'lopen --help' for available commands    │
╰───────────────────────────────────────────────╯
```

#### Validation Errors (Context)
```
✗ Invalid model name

  lopen chat --model gpt-5-turbo
                      ^^^^^^^^^^^
                      Unknown model

  💡 Available models: claude-sonnet-4, gpt-4-turbo
```

### Error Categories

| Category | Symbol | Color | Includes Suggestions |
|----------|--------|-------|---------------------|
| Command Not Found | ✗ | Red | Yes - similar commands |
| Authentication | 🔒 | Red | Yes - auth instructions |
| Network | 🌐 | Red | Yes - retry, check connection |
| Validation | ⚠ | Yellow | Yes - valid options |
| SDK Error | ⚡ | Red | Maybe - depends on error |
| Configuration | ⚙ | Yellow | Yes - config fix commands |

### Implementation Requirements

```csharp
public interface IErrorRenderer {
    void RenderSimpleError(string message, string? suggestion = null);
    void RenderValidationError(string input, string message, 
                                IEnumerable<string> suggestions);
    void RenderPanelError(string title, string message, 
                          IEnumerable<string> suggestions);
}
```

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-016-01 | Invalid command | Shows similar commands |
| TC-016-02 | Auth error | Shows auth login guidance |
| TC-016-03 | Invalid option | Shows valid options |
| TC-016-04 | SDK error | Shows error + context |
| TC-016-05 | NO_COLOR error | Plain text with structure |

---

## REQ-017: Structured Data Display

### Description
Consistent display of structured data (lists, metadata, hierarchies) using appropriate Spectre.Console components.

### Acceptance Criteria
- [x] Tables for list data (basic implementation exists)
- [ ] Panels for metadata and grouped information
- [ ] Trees for hierarchical data
- [ ] Responsive column widths
- [ ] Sortable and filterable tables (future)

### Component Selection Matrix

| Data Type | Component | Example Use Case |
|-----------|-----------|------------------|
| List (rows) | `Table` | Session list, model list, history |
| Metadata (key-value) | `Panel` + key-value rows | Session details, model info |
| Hierarchy | `Tree` | Conversation tree, file structure |
| Status groups | `Panel` with nested content | Auth status, configuration |
| Code/logs | `Panel` with `Code` | Error details, JSON output |

### Table Design Standards

#### Session List Example
```
╭─────────┬──────────────────────┬─────────┬─────────╮
│ ID      │ Created              │ Model   │ Status  │
├─────────┼──────────────────────┼─────────┼─────────┤
│ abc123  │ 2026-01-24 10:30:00  │ claude  │ active  │
│ def456  │ 2026-01-23 14:22:11  │ gpt-4   │ closed  │
╰─────────┴──────────────────────┴─────────┴─────────╯
```

**Rules:**
- Use `.RoundedBorder()` for REPL mode
- Use `.AsciiBorder()` for `NO_COLOR` or piped output
- Auto-truncate columns if terminal width < 80
- Show row count below table: "2 sessions found"

#### Panel Design Standards

```csharp
var panel = new Panel(content)
{
    Header = new PanelHeader("Session Details", Justify.Left),
    Border = BoxBorder.Rounded,
    BorderStyle = new Style(Color.Blue),
    Padding = new Padding(1, 0, 1, 0)
};
```

**Rules:**
- Use rounded borders in REPL, square in piped mode
- Panel headers should be concise (< 40 chars)
- Nest panels max 2 levels deep
- Use muted color for borders (not bright)

#### Tree Design Standards

```
📁 Conversation History
├─ 🗨 User: "How do I authenticate?"
│  └─ 🤖 Assistant: "Use 'lopen auth login'..."
└─ 🗨 User: "What models are available?"
   └─ 🤖 Assistant: "Available models: ..."
```

**Rules:**
- Use emoji icons for visual hierarchy
- Limit tree depth to 5 levels
- Collapse long text (> 80 chars) with ellipsis
- Provide expand/collapse for interactive mode (future)

### Responsive Width Handling

```csharp
var terminalWidth = Console.WindowWidth;

if (terminalWidth < 60) {
    // Fallback: Vertical list format
    RenderVerticalList(data);
} else if (terminalWidth < 100) {
    // Compact: Fewer columns
    RenderCompactTable(data);
} else {
    // Full: All columns
    RenderFullTable(data);
}
```

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-017-01 | Session list table | Rounded borders, 4 columns |
| TC-017-02 | Model info panel | Metadata in panel |
| TC-017-03 | Conversation tree | Hierarchical display |
| TC-017-04 | Narrow terminal (< 60) | Vertical fallback |
| TC-017-05 | NO_COLOR table | ASCII borders |

---

## REQ-018: Layout & Right-Side Panels

### Description
Split-screen layouts with right-side task/status panels for enhanced context in interactive REPL mode.

### Acceptance Criteria
- [ ] Two-column layout support (main content | right panel)
- [ ] Right panel shows task progress, status, or context
- [ ] Responsive: Hide right panel when terminal width < 100 chars
- [ ] Non-blocking: Main content updates independently
- [ ] Auto-scroll right panel for long content

### Layout Architecture

```
╭─ Main Content ────────────────╮ ╭─ Tasks ─────────╮
│                                │ │ ✓ Connect SDK   │
│ User: "List models"            │ │ ⏳ Fetch models │
│                                │ │ ○ Display       │
│ Assistant: Fetching...         │ ╰─────────────────╯
│                                │
╰────────────────────────────────╯
   70% width                         30% width
```

### When to Use Right Panel

| Scenario | Right Panel Content |
|----------|---------------------|
| Multi-step operation | Task checklist with status |
| Long-running SDK call | Model info, session context |
| Batch processing | Progress summary, errors count |
| REPL conversation | Session metadata, token usage |

### Implementation Pattern

```csharp
var layout = new Layout("Root")
    .SplitColumns(
        new Layout("Main").Ratio(7),
        new Layout("Panel").Ratio(3)
    );

layout["Main"].Update(mainContent);
layout["Panel"].Update(CreateTaskPanel(tasks));

AnsiConsole.Write(layout);
```

### Right Panel Components

#### Task List Panel
```
╭─ Progress ──────╮
│ ✓ Step 1        │
│ ✓ Step 2        │
│ ⏳ Step 3...    │
│ ○ Step 4        │
╰─────────────────╯
```

#### Context Panel
```
╭─ Session ───────╮
│ Model: claude   │
│ Tokens: 1.2K    │
│ Duration: 12s   │
╰─────────────────╯
```

### Responsive Rules

| Terminal Width | Layout |
|----------------|--------|
| < 60 chars | No panels, vertical only |
| 60-99 chars | No right panel, full width main |
| 100-139 chars | 70/30 split (main/panel) |
| 140+ chars | 75/25 split (more main space) |

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-018-01 | Wide terminal (120 chars) | Split layout visible |
| TC-018-02 | Narrow terminal (60 chars) | No right panel |
| TC-018-03 | Task list panel | Shows current step |
| TC-018-04 | Update main while panel visible | Independent updates |
| TC-018-05 | NO_COLOR split layout | Plain text, borders |

---

## REQ-019: AI Response Streaming

### Description
Display Copilot SDK streaming responses with buffered paragraph rendering for optimal readability in REPL.

### Acceptance Criteria
- [ ] Buffer streaming tokens into paragraphs
- [ ] Render complete paragraphs (avoid char-by-char flicker)
- [ ] Show subtle progress indicator during buffering
- [ ] Maintain REPL prompt position after response
- [ ] Support inline code blocks and formatting

### Streaming Strategy: Static Chunks

**Rationale:** Buffering tokens into paragraphs provides better readability than real-time streaming, reduces terminal flicker, and allows for better formatting detection.

### Implementation Approach

```csharp
// Buffer tokens until paragraph break or timeout
var buffer = new StringBuilder();
var lastFlush = DateTime.Now;

await foreach (var token in streamingResponse) {
    buffer.Append(token);
    
    // Flush on paragraph break or timeout
    if (token.Contains("\n\n") || 
        (DateTime.Now - lastFlush).TotalMilliseconds > 500) {
        
        AnsiConsole.Write(FormatMarkdown(buffer.ToString()));
        buffer.Clear();
        lastFlush = DateTime.Now;
    }
}

// Final flush
if (buffer.Length > 0) {
    AnsiConsole.Write(FormatMarkdown(buffer.ToString()));
}
```

### Formatting Rules

| Markdown | Rendering |
|----------|-----------|
| `**bold**` | Bold style |
| `*italic*` | Italic style |
| `` `code` `` | Inline code (highlighted background) |
| ``` code block ``` | Panel with syntax highlighting |
| `# Heading` | Bold + larger text |
| `- list item` | Indented with bullet |

### Progress Indication

While buffering (< 500ms of silence):
```
Assistant: ⏳ Thinking...
```

Once first chunk arrives:
```
Assistant: [content appears here]
```

### Edge Cases

| Case | Handling |
|------|----------|
| Very long response (> 1000 tokens) | Flush every 100 tokens minimum |
| No paragraph breaks | Flush every 500ms timeout |
| Code blocks | Complete block before flush |
| Interrupted stream | Show partial with "..." indicator |
| Error mid-stream | Show error panel, keep previous content |

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-019-01 | Short response (< 100 tokens) | Single flush, no flicker |
| TC-019-02 | Long response with paragraphs | Multiple paragraph flushes |
| TC-019-03 | Response with code block | Complete block rendered |
| TC-019-04 | Stream interrupted | Partial content + error |
| TC-019-05 | NO_COLOR streaming | Plain text chunks |

---

## REQ-020: Responsive Terminal Detection

### Description
Automatic detection and adaptation to terminal capabilities (width, height, color depth, features).

### Acceptance Criteria
- [ ] Detect terminal width and adapt layouts
- [ ] Detect color support (16/256/RGB)
- [ ] Detect Unicode support
- [ ] Graceful fallback for limited terminals
- [ ] Respect NO_COLOR environment variable

### Detection Priority

1. **NO_COLOR** - If set, disable all colors (highest priority)
2. **TERM** - Check terminal type for capabilities
3. **Console.WindowWidth/Height** - Get dimensions
4. **Spectre.Console Detection** - Use built-in capability detection

### Capability Matrix

| Capability | Detection Method | Fallback |
|------------|------------------|----------|
| Color depth | `AnsiConsole.Profile.Capabilities.ColorSystem` | 16-color |
| Unicode | Check UTF-8 encoding | ASCII |
| Width | `Console.WindowWidth` | Assume 80 |
| Height | `Console.WindowHeight` | Assume 24 |
| Interactive | `Console.IsInputRedirected` | Non-interactive |

### Adaptive Behaviors

#### Width-Based Adaptations

| Width | Behavior |
|-------|----------|
| < 60 chars | Vertical layouts only, no tables |
| 60-79 chars | Compact tables, no right panels |
| 80-99 chars | Standard layouts, limited columns |
| 100-139 chars | Full layouts, right panels enabled |
| 140+ chars | Wide layouts, all features |

#### Color-Based Adaptations

| Color Support | Usage |
|---------------|-------|
| No color (NO_COLOR) | Plain text, ASCII borders, symbols |
| 16 colors | Standard ANSI colors |
| 256 colors | Enhanced gradients, better highlighting |
| RGB/TrueColor | Full color spectrum, brand colors |

### Implementation

```csharp
public class TerminalCapabilities {
    public int Width { get; init; }
    public int Height { get; init; }
    public ColorSystem ColorSystem { get; init; }
    public bool SupportsUnicode { get; init; }
    public bool IsInteractive { get; init; }
    
    public static TerminalCapabilities Detect() {
        return new TerminalCapabilities {
            Width = Console.WindowWidth,
            Height = Console.WindowHeight,
            ColorSystem = AnsiConsole.Profile.Capabilities.ColorSystem,
            SupportsUnicode = Console.OutputEncoding.Equals(Encoding.UTF8),
            IsInteractive = !Console.IsInputRedirected
        };
    }
}
```

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-020-01 | NO_COLOR=1 | All colors disabled |
| TC-020-02 | Width < 60 | Vertical layouts |
| TC-020-03 | Width > 140 | Split layouts enabled |
| TC-020-04 | 16-color terminal | Standard ANSI only |
| TC-020-05 | Piped output | Plain text, no ANSI |

---

## REQ-021: TUI Testing & Mocking

### Description
Comprehensive testing infrastructure for TUI components with mockable interfaces and snapshot testing.

### Acceptance Criteria
- [x] `IConsoleInput` interface for Console.ReadLine() abstraction (implemented)
- [x] Spectre.Console.Testing.TestConsole for output validation (partial usage)
- [ ] Create `ITuiRenderer` interface for all TUI operations
- [ ] Mock implementations for unit tests
- [ ] Snapshot testing for complex layouts
- [ ] Integration tests with real Spectre.Console

### Testing Architecture

```csharp
public interface ITuiRenderer {
    void RenderSuccess(string message);
    void RenderError(string message, string? suggestion = null);
    void RenderTable<T>(IEnumerable<T> data, TableConfig config);
    void RenderPanel(string title, string content);
    Task<T> RenderSpinnerAsync<T>(string status, Func<Task<T>> operation);
    void RenderLayout(Layout layout);
}

// Real implementation
public class SpectreTuiRenderer : ITuiRenderer {
    // Uses actual Spectre.Console
}

// Test implementation
public class MockTuiRenderer : ITuiRenderer {
    public List<string> RenderedMessages { get; } = new();
    // Records calls for assertion
}
```

### Test Categories

#### Unit Tests (Mock Renderer)
- Command handler logic
- Error handling and suggestions
- Data formatting logic
- Layout decisions

#### Integration Tests (TestConsole)
- Actual Spectre.Console output
- Color rendering
- Border styles
- Table formatting

#### Snapshot Tests (Visual Regression)
- Complex layouts
- Multi-panel outputs
- Error displays
- Progress indicators

### Testing Patterns

```csharp
// Unit test with mock
[Fact]
public async Task Chat_Command_Renders_Success() {
    var mockRenderer = new MockTuiRenderer();
    var handler = new ChatCommandHandler(mockRenderer);
    
    await handler.ExecuteAsync("Hello");
    
    mockRenderer.RenderedMessages
        .ShouldContain(m => m.Contains("✓"));
}

// Integration test with TestConsole
[Fact]
public void Table_Renders_With_Rounded_Borders() {
    var console = new TestConsole();
    var renderer = new SpectreTuiRenderer(console);
    
    renderer.RenderTable(data, new TableConfig { 
        Border = TableBorder.Rounded 
    });
    
    console.Output.ShouldContain("╭");
    console.Output.ShouldContain("╮");
}

// Snapshot test
[Fact]
public void Error_Panel_Matches_Snapshot() {
    var output = RenderErrorPanel(
        "Authentication failed",
        "Try: lopen auth login"
    );
    
    Snapshot.Match(output);
}
```

### Test Coverage Requirements

| Component | Coverage Target |
|-----------|----------------|
| Core TUI logic (ITuiRenderer implementations) | 100% |
| Command handlers using TUI | 90% |
| Layout decision logic | 100% |
| Error rendering | 100% |
| Progress indicators | 80% (exclude timing-dependent) |

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-021-01 | Mock renderer records calls | All renders captured |
| TC-021-02 | TestConsole validates output | ANSI codes verified |
| TC-021-03 | Snapshot test detects changes | Visual regression caught |
| TC-021-04 | NO_COLOR in tests | Plain output validated |
| TC-021-05 | Layout logic unit test | Correct component selection |

---

## Implementation Guidelines

### Development Workflow

1. **Check terminal capabilities** at startup
2. **Select appropriate components** based on context (REPL vs CLI)
3. **Apply responsive rules** based on width/capabilities
4. **Use ITuiRenderer interface** for all rendering (enables testing)
5. **Test with NO_COLOR** to ensure accessibility

### Code Organization

```
src/Lopen.Core/
  Tui/
    ITuiRenderer.cs              # Core interface
    SpectreTuiRenderer.cs        # Real implementation
    TerminalCapabilities.cs      # Detection logic
    Components/
      ErrorRenderer.cs
      ProgressRenderer.cs
      DataRenderer.cs
      LayoutRenderer.cs

tests/Lopen.Core.Tests/
  Tui/
    MockTuiRenderer.cs           # Test mock
    SpectreTuiRendererTests.cs   # Integration tests
    ErrorRendererTests.cs        # Component tests
    __snapshots__/               # Visual snapshots
```

### Performance Considerations

- Buffer output for batch operations (use `StringBuilder`)
- Avoid excessive `AnsiConsole.Write()` calls in loops
- Use `Live` displays for frequently updating content
- Cache terminal capabilities (detect once per session)

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Spectre.Console | ^0.54.0 | Core TUI framework |
| Spectre.Console.Testing | ^0.54.0 | Test infrastructure |
| System.CommandLine | ^2.0.2 | CLI integration |

---

## REQ-022: Welcome Header & REPL Banner

### Description
Display a branded welcome header at REPL startup featuring the Wind Runner radiant order sigil ASCII art, version information, help tips, session metadata, and context window capacity tracking.

### Context
Lopen is building an interactive REPL that serves as an enhanced version of the existing `scripts/lopen.sh` with `PLAN.PROMPT.md` and `BUILD.PROMPT.md` capabilities built in. The name "lopen" references the character from Brandon Sanderson's Stormlight Archive, whose niche role mirrors the AI agent loop concept (similar to how Ralph Wiggum represents a cultural reference point). The welcome header establishes brand identity and provides essential session context at a glance, similar to Claude Code and GitHub Copilot CLI's branded experiences.

### Acceptance Criteria
- [ ] ASCII art logo featuring Wind Runner radiant order sigil
- [ ] Display application version from assembly metadata
- [ ] Show contextual help tip referencing actual `lopen help` command
- [ ] Display session name (auto-generated with override via `--session-name` flag)
- [ ] Show context window capacity (tokens if available from SDK, else message count)
- [ ] Responsive layout adapting to terminal width (REQ-020)
- [ ] Respect TUI color guidelines (REQ-014) and NO_COLOR
- [ ] Configurable display preferences (show/hide, position)
- [ ] Support `--no-header` and `--quiet` CLI flags to suppress
- [ ] Render using Spectre.Console components for consistency

### Visual Design

#### Full Header (Terminal Width ≥ 100 chars)
```
╭─────────────────────────────────────────────────────────────────────────────╮
│                         ⚡ Wind Runner Sigil ⚡                              │
│                                                                             │
│                              ▄▄▄▄▄▄▄▄▄                                      │
│                           ▄▀▀         ▀▀▄                                   │
│                         ▄▀   ▄▄▄▄▄▄▄    ▀▄                                  │
│                        █   ▄▀▀     ▀▀▄   █                                  │
│                       █   █  ⚡ W ⚡  █   █                                  │
│                       █    ▀▄▄     ▄▄▀    █                                 │
│                        ▀▄    ▀▀▀▀▀▀▀    ▄▀                                  │
│                          ▀▄▄         ▄▄▀                                    │
│                             ▀▀▀▀▀▀▀▀▀                                       │
│                                                                             │
│                           lopen v1.0.0-alpha                                │
│                    Interactive Copilot Agent Loop                           │
│                                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│ 💡 Tip: Type 'help' or 'lopen --help' for available commands               │
│ 📊 Session: lopen-2026-01-25-1923  |  Context: 2.4K/128K tokens  |  🟢     │
╰─────────────────────────────────────────────────────────────────────────────╯

lopen>
```

#### Compact Header (Terminal Width 60-99 chars)
```
╭────────────────────────────────────────────────╮
│         ⚡ lopen v1.0.0-alpha ⚡                │
│      Interactive Copilot Agent Loop            │
│                                                │
│ Type 'help' for commands                       │
│ Session: lopen-2026-01-25-1923                 │
│ Context: 2.4K/128K tokens  🟢                  │
╰────────────────────────────────────────────────╯

lopen>
```

#### Minimal Header (Terminal Width < 60 chars)
```
lopen v1.0.0-alpha
Session: ...1923 | 2.4K tokens
Type 'help' for commands

>
```

### Component Breakdown

#### 1. ASCII Logo (Wind Runner Sigil)
- **Design**: Custom ASCII art representing the Wind Runner radiant order symbol
- **Dimensions**: ~15 lines tall, 40-60 chars wide (scales with terminal)
- **Colors**: Cyan/Blue accent colors (respecting color depth)
- **Fallback**: Simplified text logo for narrow terminals or NO_COLOR

#### 2. Version Display
- **Source**: Assembly version attribute (`AssemblyInformationalVersionAttribute`)
- **Format**: `lopen v{Major}.{Minor}.{Patch}-{Label}`
- **Example**: `lopen v1.0.0-alpha`, `lopen v2.1.3`
- **Position**: Centered below logo
- **Style**: Bold or highlighted

#### 3. Tagline
- **Text**: "Interactive Copilot Agent Loop"
- **Purpose**: Brief description of tool purpose
- **Position**: Below version
- **Style**: Muted/secondary color

#### 4. Help Tip
- **Format**: "💡 Tip: Type 'help' or 'lopen --help' for available commands"
- **Dynamic**: References actual help system
- **Symbol**: 💡 or `[i]` fallback
- **Style**: Info color (blue) per REQ-014

#### 5. Session Information
- **Name**: Auto-generated (timestamp-based) or user-specified
- **Auto Format**: `lopen-{YYYY-MM-DD-HHmm}` (e.g., `lopen-2026-01-25-1923`)
- **Override**: Via `--session-name "my-session"` flag
- **Display**: Truncated if > 30 chars (e.g., `...session-name`)
- **Symbol**: 📊 or `[S]` fallback

#### 6. Context Window Capacity
- **Primary**: Token count from Copilot SDK if available
  - Format: `{used}K/{total}K tokens` (e.g., `2.4K/128K tokens`)
  - Color: Green if < 50% used, Yellow if 50-80%, Red if > 80%
- **Fallback**: Message count if tokens unavailable
  - Format: `{count} messages` (e.g., `12 messages`)
- **Status Indicator**: Color-coded circle (🟢 🟡 🔴) or text `[OK] [WARN] [FULL]`

### Configuration Options

#### User Preferences (stored in SessionState or config file)
```csharp
public class WelcomeHeaderPreferences {
    public bool ShowHeader { get; set; } = true;  // Default: show
    public HeaderPosition Position { get; set; } = HeaderPosition.StartupOnly;
    public HeaderVerbosity Verbosity { get; set; } = HeaderVerbosity.Full;
    public bool ShowLogo { get; set; } = true;
    public bool ShowContextCapacity { get; set; } = true;
}

public enum HeaderPosition {
    StartupOnly,      // Show once at startup, scrolls away
    AlwaysTop,        // Re-render before each prompt (advanced)
    Hidden            // Never show
}

public enum HeaderVerbosity {
    Full,      // All components
    Compact,   // Logo + key info
    Minimal    // Version + session only
}
```

#### CLI Flags
```bash
lopen repl --no-header              # Suppress header entirely
lopen repl --quiet                  # Same as --no-header
lopen repl --session-name "build"   # Named session
```

### Responsive Rules

Following REQ-020 terminal detection guidelines:

| Terminal Width | Header Style | Components |
|----------------|--------------|------------|
| < 60 chars | Minimal | Version, session (truncated), context, help |
| 60-99 chars | Compact | Logo (simplified), version, tagline, session, context, help |
| 100-139 chars | Full | Complete ASCII art, all components, full layout |
| 140+ chars | Full (wide) | Extended layout, more spacing |

### Color Scheme

Following REQ-014 color palette:

| Component | Color | Fallback |
|-----------|-------|----------|
| Logo/Sigil | Cyan/Accent | White/Normal |
| Version | Highlight (Bold) | Bold |
| Tagline | Muted (Gray) | Normal |
| Help tip | Info (Blue) | Normal |
| Session name | Muted (Gray) | Normal |
| Context capacity (OK) | Success (Green) | Normal |
| Context capacity (Warn) | Warning (Yellow) | Normal |
| Context capacity (Full) | Error (Red) | Normal |
| Border | Muted (Gray) | ASCII `+-|` |

### Implementation Requirements

#### 1. Header Renderer
```csharp
public interface IWelcomeHeaderRenderer {
    void RenderWelcomeHeader(WelcomeHeaderContext context);
}

public class WelcomeHeaderContext {
    public string Version { get; init; } = "";
    public string SessionName { get; init; } = "";
    public ContextWindowInfo ContextWindow { get; init; } = new();
    public WelcomeHeaderPreferences Preferences { get; init; } = new();
    public TerminalCapabilities Terminal { get; init; } = new();
}

public class ContextWindowInfo {
    public long? TokensUsed { get; init; }
    public long? TokensTotal { get; init; }
    public int MessageCount { get; init; }
    public bool HasTokenInfo => TokensUsed.HasValue && TokensTotal.HasValue;
    public double UsagePercent => HasTokenInfo 
        ? (double)TokensUsed!.Value / TokensTotal!.Value * 100 
        : 0;
}
```

#### 2. ASCII Art Storage
- Store Wind Runner sigil as embedded resource or constant string
- Multiple variants for different widths
- Builder pattern for dynamic construction

```csharp
public class AsciiLogoProvider {
    public string GetLogo(int availableWidth) {
        return availableWidth switch {
            >= 100 => GetFullLogo(),
            >= 60 => GetCompactLogo(),
            _ => GetMinimalLogo()
        };
    }
}
```

#### 3. Integration Points
- Called by `ReplService.StartAsync()` before entering command loop
- Uses `ITuiRenderer` for consistent rendering
- Respects `--no-header` and `--quiet` flags from CLI parser
- Reads session name from `ISessionStateService`
- Queries token usage from Copilot SDK (if available)

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-022-01 | Default REPL start | Full header displayed with all components |
| TC-022-02 | `--no-header` flag | No header displayed, prompt immediately |
| TC-022-03 | `--quiet` flag | No header displayed |
| TC-022-04 | Custom session name | Header shows provided session name |
| TC-022-05 | Narrow terminal (< 60) | Minimal header variant |
| TC-022-06 | Standard terminal (80) | Compact header variant |
| TC-022-07 | Wide terminal (120) | Full header with ASCII art |
| TC-022-08 | NO_COLOR environment | Plain text with ASCII borders |
| TC-022-09 | Token info available | Shows `X.XK/XXXK tokens` |
| TC-022-10 | Token info unavailable | Shows message count fallback |
| TC-022-11 | High token usage (> 80%) | Red indicator |
| TC-022-12 | Version display | Shows correct assembly version |
| TC-022-13 | Help tip | References actual help command |
| TC-022-14 | Logo scaling | Appropriate logo for terminal width |
| TC-022-15 | Preferences: header hidden | No header shown |

### Dependencies

- **Spectre.Console**: For panel, layout, and color rendering
- **REQ-014**: Color palette and symbol standards
- **REQ-020**: Terminal capability detection
- **REQ-021**: ITuiRenderer interface for testing
- **Session State Service**: For session name and preferences
- **Copilot SDK**: For token usage metadata (optional)
- **System.Reflection**: For assembly version retrieval

### Future Enhancements (Post-MVP)

- [ ] Animated logo reveal (fade-in effect)
- [ ] Custom logo selection (user-provided ASCII art)
- [ ] Multi-language taglines (i18n)
- [ ] Theme customization (color schemes)
- [ ] Real-time context capacity updates (live display)
- [ ] Session statistics (command count, uptime)
- [ ] Integration with PLAN/BUILD modes (show current mode badge)
- [ ] Welcome tips rotation (different tips each session)

### Related Requirements

- **REQ-014**: Output formatting and color standards
- **REQ-017**: Panel and layout components
- **REQ-020**: Terminal detection and responsiveness
- **REQ-021**: TUI testing infrastructure
- **REQ-010**: REPL mode integration
- **REQ-011**: Session state management

---

## Migration Plan

### Phase 1: Foundation (Current → v1.1)
- [ ] Create `ITuiRenderer` interface
- [ ] Implement `SpectreTuiRenderer`
- [ ] Refactor existing `ConsoleOutput` to use renderer
- [ ] Add terminal capability detection (REQ-020)
- [ ] Create mock renderer for tests
- [ ] Implement welcome header (REQ-022 MVP)
  - [ ] ASCII logo provider with Wind Runner sigil
  - [ ] Version display from assembly
  - [ ] Session name handling (auto + override)
  - [ ] Context window capacity display
  - [ ] Responsive layout variants
  - [ ] CLI flags (--no-header, --quiet)

### Phase 2: Error Handling (v1.1 → v1.2)
- [ ] Implement error panel rendering
- [ ] Add validation error display with context
- [ ] Integrate with System.CommandLine error handling
- [ ] Add suggestion engine for common errors

### Phase 3: Progress & Streaming (v1.2 → v1.3)
- [ ] Implement spinner support for SDK calls
- [ ] Add progress bars for batch operations
- [ ] Implement streaming response buffering
- [ ] Add markdown formatting for responses

### Phase 4: Advanced Layouts (v1.3 → v1.4)
- [ ] Implement split-screen layouts
- [ ] Add right-side task panel
- [ ] Create responsive layout system
- [ ] Add tree and panel components

### Phase 5: Polish & Testing (v1.4 → v2.0)
- [ ] Complete test coverage (100% for core)
- [ ] Add snapshot tests for all layouts
- [ ] Performance optimization
- [ ] Documentation and examples

---

## Related Documentation

- [REPL Specification](../repl/SPECIFICATION.md) - REPL command handling
- [CLI Core Specification](../cli-core/SPECIFICATION.md) - Command structure
- [Platform Specification](../platform/SPECIFICATION.md) - Accessibility requirements
- [Spectre.Console Documentation](https://spectreconsole.net/) - Component reference
