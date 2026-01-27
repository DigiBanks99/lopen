# TUI Component Map

Visual reference of all implemented TUI components and their relationships.

## Component Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Lopen.Cli (Program.cs)                           │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐                   │
│  │ ReplService │  │ LoopService │  │ CLI Commands │                   │
│  └──────┬──────┘  └──────┬──────┘  └──────┬───────┘                   │
└─────────┼─────────────────┼─────────────────┼──────────────────────────┘
          │                 │                 │
          │                 │                 │
┌─────────▼─────────────────▼─────────────────▼──────────────────────────┐
│                         Lopen.Core                                      │
│                                                                         │
│  ┌──────────────────┐    ┌──────────────────┐                         │
│  │  ConsoleOutput   │────│ IAnsiConsole     │ Spectre.Console         │
│  └────────┬─────────┘    └──────────────────┘                         │
│           │                                                             │
│           │ Uses all renderers below                                   │
│           │                                                             │
│  ┌────────▼───────────────────────────────────────────────────┐       │
│  │                    TUI Renderers                            │       │
│  │                                                             │       │
│  │  ┌─────────────────────────┐  ┌─────────────────────────┐ │       │
│  │  │ IWelcomeHeaderRenderer  │  │ IProgressRenderer       │ │       │
│  │  │   SpectreWelcome...     │  │   SpectreProgress...    │ │       │
│  │  │   MockWelcome...        │  │   MockProgress...       │ │       │
│  │  └─────────────────────────┘  └─────────────────────────┘ │       │
│  │                                                             │       │
│  │  ┌─────────────────────────┐  ┌─────────────────────────┐ │       │
│  │  │ IErrorRenderer          │  │ IDataRenderer           │ │       │
│  │  │   SpectreError...       │  │   SpectreData...        │ │       │
│  │  │   MockError...          │  │   MockData...           │ │       │
│  │  └─────────────────────────┘  └─────────────────────────┘ │       │
│  │                                                             │       │
│  │  ┌─────────────────────────┐  ┌─────────────────────────┐ │       │
│  │  │ ILayoutRenderer         │  │ IStreamRenderer         │ │       │
│  │  │   SpectreLayout...      │  │   SpectreStream...      │ │       │
│  │  │   MockLayout...         │  │   MockStream...         │ │       │
│  │  └─────────────────────────┘  └─────────────────────────┘ │       │
│  │                                                             │       │
│  │  ┌─────────────────────────┐                              │       │
│  │  │ ITerminalCapabilities   │                              │       │
│  │  │   TerminalCapabilities  │                              │       │
│  │  │   MockTerminalCap...    │                              │       │
│  │  └─────────────────────────┘                              │       │
│  └─────────────────────────────────────────────────────────────┘       │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────┐       │
│  │                    Supporting Components                     │       │
│  │                                                              │       │
│  │  ┌─────────────────┐  ┌──────────────────┐                 │       │
│  │  │ AsciiLogoProvider│  │ SymbolProvider   │                 │       │
│  │  │  (Wind Runner)   │  │  (✓ ✗ ⚠ ℹ ⏳)    │                 │       │
│  │  └─────────────────┘  └──────────────────┘                 │       │
│  │                                                              │       │
│  │  ┌─────────────────┐  ┌──────────────────┐                 │       │
│  │  │ ColorProvider   │  │ TreeRenderer     │                 │       │
│  │  │  (16/256/RGB)   │  │  (Hierarchies)   │                 │       │
│  │  └─────────────────┘  └──────────────────┘                 │       │
│  └─────────────────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────────────┘
```

## Renderer Details

### IWelcomeHeaderRenderer
**Purpose**: Display branded welcome header with session info  
**Implementations**:
- `SpectreWelcomeHeaderRenderer` - Responsive layouts (full/compact/minimal)
- `MockWelcomeHeaderRenderer` - Testing

**Features**:
- ASCII logo (Wind Runner sigil)
- Version display
- Session name & context window tracking
- Responsive to terminal width (80+/50+/<50)
- Respects NO_COLOR

**Status**: ✅ Complete | ⚠️ Not integrated into REPL/Loop

---

### IProgressRenderer
**Purpose**: Show spinners for async operations  
**Implementations**:
- `SpectreProgressRenderer` - Dots/Arc/Line/SimpleDotsScrolling spinners
- `MockProgressRenderer` - Testing

**Features**:
- Indeterminate progress (spinners)
- Status text updates during operation
- Support for async operations with return values
- NO_COLOR fallback

**Status**: ✅ Complete | ✅ Used in some operations

---

### IErrorRenderer
**Purpose**: Display structured errors with suggestions  
**Implementations**:
- `SpectreErrorRenderer` - Panels, validation errors, command suggestions
- `MockErrorRenderer` - Testing

**Features**:
- Simple errors (single line)
- Panel errors (multi-line with border)
- Validation errors (with valid options)
- Command not found (with suggestions)
- Structured ErrorInfo model

**Status**: ✅ Complete | ⚠️ Not used for CLI errors

---

### IDataRenderer
**Purpose**: Display structured data (tables, metadata)  
**Implementations**:
- `SpectreDataRenderer` - Tables with borders, metadata panels
- `MockDataRenderer` - Testing

**Features**:
- Tables with configurable columns
- Metadata panels (key-value pairs)
- Border styles (rounded/ASCII)
- NO_COLOR support

**Status**: ✅ Complete | ⚠️ Column responsiveness pending

---

### ILayoutRenderer
**Purpose**: Split-screen layouts with side panels  
**Implementations**:
- `SpectreLayoutRenderer` - 70/30 split with responsive fallback
- `MockLayoutRenderer` - Testing

**Features**:
- Split layout (main + side panel)
- Task panel (with status symbols ✓⏳○✗)
- Context panel (key-value metadata)
- Responsive (hides panel if terminal < 100 chars)
- Configurable ratios

**Status**: ✅ Complete | ⚠️ Live display pending

---

### IStreamRenderer
**Purpose**: Buffer and render streaming AI responses  
**Implementations**:
- `SpectreStreamRenderer` - Paragraph buffering, markdown formatting
- `MockStreamRenderer` - Testing

**Features**:
- Token buffering (paragraph breaks or timeout)
- Code block detection (doesn't flush mid-block)
- Markdown formatting (bold, italic, code)
- Thinking indicator
- Metrics collection
- NO_COLOR support

**Status**: ✅ Complete | ⚠️ REPL prompt positioning pending

---

### ITerminalCapabilities
**Purpose**: Detect terminal features and dimensions  
**Implementations**:
- `TerminalCapabilities` - Real detection
- `MockTerminalCapabilities` - Testing with presets

**Features**:
- Width/height detection
- Color depth (16/256/TrueColor)
- Unicode support detection
- Interactive mode detection
- NO_COLOR environment variable support

**Status**: ✅ Complete | ✅ Fully functional

---

## Supporting Components

### AsciiLogoProvider
- Wind Runner sigil ASCII art
- Multiple size variants
- Tagline: "Interactive Copilot Agent Loop"
- Help tip generation

### SymbolProvider
- Unicode symbols: ✓ ✗ ⚠ ℹ ⏳ ✨ 🚀 ⚡ 💡
- ASCII fallbacks: [OK] [X] [!] [i] ...
- Status symbol enum

### ColorProvider
- Adaptive color selection
- TrueColor → 256 → 16 color graceful degradation
- Color categories: Success, Error, Warning, Info, Muted, Highlight, Accent

### TreeRenderer
- Hierarchical data display
- Tree node model with children
- Icon support
- Spectre.Console Tree integration

### ConsoleOutput
- Convenience wrapper for common operations
- Success/Error/Warning/Info/Muted helpers
- Progress/New/Launch/Fast/Tip emoji helpers
- KeyValue display
- Rule drawing
- NO_COLOR support

---

## Test Coverage

All components have comprehensive unit tests:

```
Component                       Test File                           Tests
===========================================================================
SpectreWelcomeHeaderRenderer    SpectreWelcomeHeaderRendererTests   13
SpectreProgressRenderer         SpectreProgressRendererTests        12
SpectreErrorRenderer            SpectreErrorRendererTests           18
SpectreDataRenderer             SpectreDataRendererTests            15
SpectreLayoutRenderer           SpectreLayoutRendererTests          14
SpectreStreamRenderer           SpectreStreamRendererTests          23
TerminalCapabilities            TerminalCapabilitiesTests           10
AsciiLogoProvider               AsciiLogoProviderTests               8
ColorProvider                   ColorProviderTests                  12
SymbolProvider                  SymbolProviderTests                 10
ConsoleOutput                   ConsoleOutputTests                  25
TreeRenderer                    TreeRendererTests                   12
===========================================================================
TOTAL                                                              172+
```

---

## Integration Status

| Component | Implemented | Tested | Integrated | Notes |
|-----------|-------------|--------|------------|-------|
| WelcomeHeaderRenderer | ✅ | ✅ | ❌ | Needs wiring in REPL/Loop |
| ProgressRenderer | ✅ | ✅ | ⚠️ | Some operations use it |
| ErrorRenderer | ✅ | ✅ | ⚠️ | Not used for CLI errors |
| DataRenderer | ✅ | ✅ | ✅ | Ready for use |
| LayoutRenderer | ✅ | ✅ | ✅ | Ready for use |
| StreamRenderer | ✅ | ✅ | ✅ | Ready for use |
| TerminalCapabilities | ✅ | ✅ | ✅ | Fully integrated |

---

## Quick Integration Guide

### To use WelcomeHeaderRenderer:
```csharp
var renderer = new SpectreWelcomeHeaderRenderer();
var context = new WelcomeHeaderContext
{
    Version = "1.0.0",
    SessionName = "my-session",
    ContextWindow = new ContextWindowInfo { MessageCount = 5 }
};
renderer.RenderWelcomeHeader(context);
```

### To use ProgressRenderer:
```csharp
var renderer = new SpectreProgressRenderer();
var result = await renderer.ShowProgressAsync(
    "Loading...",
    async (ctx) => {
        ctx.UpdateStatus("Processing...");
        return await DoWorkAsync();
    }
);
```

### To use ErrorRenderer:
```csharp
var renderer = new SpectreErrorRenderer();
renderer.RenderError(new ErrorInfo
{
    Title = "Command Not Found",
    Message = "Unknown command: chatr",
    DidYouMean = "chat",
    Suggestions = new[] { "chat", "repl", "loop" }
});
```

### To use StreamRenderer:
```csharp
var renderer = new SpectreStreamRenderer();
await renderer.RenderStreamAsync(
    tokenStream,
    config: new StreamConfig 
    { 
        ShowThinkingIndicator = true,
        FlushTimeoutMs = 500
    }
);
```

---

## Files Reference

### Core Interfaces (src/Lopen.Core/)
- IWelcomeHeaderRenderer.cs
- IProgressRenderer.cs
- IErrorRenderer.cs
- IDataRenderer.cs
- ILayoutRenderer.cs
- IStreamRenderer.cs
- ITerminalCapabilities.cs

### Spectre.Console Implementations
- SpectreWelcomeHeaderRenderer.cs
- SpectreProgressRenderer.cs
- SpectreErrorRenderer.cs
- SpectreDataRenderer.cs
- SpectreLayoutRenderer.cs
- SpectreStreamRenderer.cs
- TerminalCapabilities.cs

### Mock Implementations (for testing)
- MockWelcomeHeaderRenderer.cs
- MockProgressRenderer.cs
- MockErrorRenderer.cs
- MockDataRenderer.cs
- MockLayoutRenderer.cs
- MockStreamRenderer.cs
- MockTerminalCapabilities.cs

### Tests (tests/Lopen.Core.Tests/)
- SpectreWelcomeHeaderRendererTests.cs
- SpectreProgressRendererTests.cs
- SpectreErrorRendererTests.cs
- SpectreDataRendererTests.cs
- SpectreLayoutRendererTests.cs
- SpectreStreamRendererTests.cs
- TerminalCapabilitiesTests.cs

### Supporting Components
- ConsoleOutput.cs
- AsciiLogoProvider.cs
- ColorProvider.cs
- SymbolProvider.cs
- TreeRenderer.cs

---

**Last Updated**: January 27, 2026
