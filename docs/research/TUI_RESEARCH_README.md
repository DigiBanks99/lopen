# TUI Research Documentation

This directory contains comprehensive research and implementation guidance for Terminal UI (TUI) features in the Lopen project using Spectre.Console.

## Documents

### 📘 [SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md](./SPECTRE_CONSOLE_TUI_IMPLEMENTATION_GUIDE.md)
**The Complete Guide** - 3,800+ lines of detailed implementation patterns

Comprehensive documentation covering all 8 TUI requirements:
- REQ-015: Progress Indicators & Spinners
- REQ-016: Error Display & Correction Guidance  
- REQ-017: Structured Data Display (Panels & Trees)
- REQ-018: Layout & Right-Side Panels
- REQ-019: AI Response Streaming
- REQ-020: Responsive Terminal Detection
- REQ-021: TUI Testing & Mocking
- REQ-022: Welcome Header with ASCII Art

**Contents:**
- Pattern overviews and architecture
- Complete, production-ready C# code examples
- Comprehensive testing approaches with TestConsole and mocks
- Best practices and anti-patterns
- Performance considerations
- Troubleshooting guides
- Implementation checklist

### 📋 [TUI_QUICK_REFERENCE.md](./TUI_QUICK_REFERENCE.md)
**Quick Lookup Guide**

Condensed reference for rapid development:
- Interface signatures
- Common code patterns
- Spinner type guide
- Terminal width breakpoints
- Testing snippets
- DI setup
- Troubleshooting table

## Quick Start

1. **Start here**: Read the relevant section in the full implementation guide
2. **During development**: Reference the quick guide for patterns
3. **For testing**: Use the testing patterns and mock implementations
4. **When stuck**: Check troubleshooting sections

## Implementation Workflow

### Phase 1: Core Abstractions (Week 1)
```
├── Create interfaces (ITuiRenderer, IProgressRenderer, etc.)
├── Implement TerminalCapabilities
└── Set up DI registration
```

### Phase 2: Basic Components (Week 1-2)
```
├── Implement SpectreProgressRenderer (REQ-015)
├── Implement SpectreErrorRenderer (REQ-016)
└── Add MockTuiRenderer for testing (REQ-021)
```

### Phase 3: Data Display (Week 2)
```
├── Implement SpectreDataRenderer (REQ-017)
├── Add Panel, Tree, and Table rendering
└── Implement responsive column calculation
```

### Phase 4: Advanced Features (Week 2-3)
```
├── Implement SpectreLayoutRenderer (REQ-018)
├── Add split-screen layouts
├── Implement SpectreStreamRenderer (REQ-019)
└── Add AI response streaming
```

### Phase 5: Polish (Week 3)
```
├── Implement WelcomeRenderer (REQ-022)
├── Add comprehensive test suite
└── Polish responsive behavior
```

## Key Architectural Decisions

### Interface-Based Design
All TUI components use interfaces for:
- **Testability**: Easy mocking with `MockTuiRenderer`
- **Flexibility**: Swap implementations without changing consumers
- **DI Integration**: Clean dependency injection setup

### Progressive Enhancement
Adaptive behavior based on terminal capabilities:
```
TrueColor Terminal → Rich panels with RGB colors
8-bit Terminal    → Standard panels with 256 colors  
16-color Terminal → Basic colors
NO_COLOR          → Plain text fallback
```

### Responsive Layouts
Width-based breakpoints:
```
< 60 chars   → Minimal text output
60-79 chars  → Compact panels
80-119 chars → Standard layout
≥ 120 chars  → Split-screen with side panels
```

## Testing Strategy

### Unit Tests
- Use `MockTuiRenderer` to verify logic without rendering
- Test each component in isolation
- Verify correct interface calls

### Integration Tests  
- Use `TestConsole` to verify actual rendering
- Test with different terminal widths
- Verify responsive behavior

### Snapshot Tests
- Capture and compare full output
- Detect unintended layout changes
- Use for complex multi-component renders

## Dependencies

```xml
<PackageReference Include="Spectre.Console" Version="0.49.1" />
<PackageReference Include="Spectre.Console.Testing" Version="0.49.1" />
```

## Code Organization

```
Lopen.Tui/
├── Interfaces/          # All ITuiRenderer, IProgressRenderer, etc.
├── Implementations/     # Spectre*.cs implementations
├── Models/             # ErrorInfo, SpinnerStyle, etc.
├── Helpers/            # SuggestionHelper, ResponsiveCalculator
├── Testing/            # MockTuiRenderer, SnapshotHelper
└── Components/         # TaskListPanel, ContextPanel, etc.
```

## Examples

### Simple Progress
```csharp
await _progress.ShowProgressAsync(
    "Loading...",
    async ctx => await DoWork()
);
```

### Error with Suggestions
```csharp
_errorRenderer.RenderError(new ErrorInfo
{
    Title = "Not Found",
    Message = "Command 'stat' not recognized",
    DidYouMean = "status"
});
```

### Split Layout
```csharp
var main = new Panel("Main content");
var side = TaskListPanel.Create(tasks);
_layout.RenderSplitLayout(main, side);
```

## Common Pitfalls

❌ **Forgetting NO_COLOR**
```csharp
// Bad: Always use colors
_console.MarkupLine("[green]Success![/]");

// Good: Check capabilities
if (_capabilities.SupportsColor)
    _console.MarkupLine("[green]Success![/]");
else
    _console.WriteLine("[OK] Success!");
```

❌ **Not escaping user input**
```csharp
// Bad: Markup injection risk
_console.MarkupLine($"[green]{userInput}[/]");

// Good: Escape markup
_console.MarkupLine($"[green]{Markup.Escape(userInput)}[/]");
```

❌ **Assuming wide terminal**
```csharp
// Bad: Breaks on narrow terminals
RenderSplitLayout();

// Good: Check width first
if (_capabilities.Width >= 120)
    RenderSplitLayout();
else
    RenderStackedLayout();
```

## Performance Tips

1. **Batch updates**: Group multiple writes together
2. **Throttle live displays**: Update at most every 100ms
3. **Buffer streaming**: Don't flush every token
4. **Dispose properly**: Clean up live displays
5. **Cache renderables**: Reuse expensive constructions

## Resources

### Official Documentation
- [Spectre.Console Website](https://spectreconsole.net/)
- [API Reference](https://spectreconsole.net/api/)
- [GitHub Repository](https://github.com/spectreconsole/spectre.console)

### Community
- [Stack Overflow Tag](https://stackoverflow.com/questions/tagged/spectre.console)
- [GitHub Discussions](https://github.com/spectreconsole/spectre.console/discussions)

### Standards
- [NO_COLOR Standard](https://no-color.org/)
- [ANSI Escape Codes](https://en.wikipedia.org/wiki/ANSI_escape_code)

## Questions?

- Check the **Troubleshooting** sections in the full guide
- Review **Best Practices** for dos and don'ts
- See **Testing Approach** sections for test examples
- Consult **References** at end of each requirement section

---

**Status**: ✅ Complete - Ready for implementation

**Last Updated**: January 25, 2026

**Version**: 1.0
