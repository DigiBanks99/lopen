# Implementation Plan

> Current Focus: JTBD-031 - TUI Terminal Detection (REQ-020) ✅ COMPLETE

## Overview

Detect terminal capabilities (size, color, unicode, interactivity) for adaptive TUI rendering. Uses Spectre.Console detection with Console fallbacks.

## Workplan

### Phase 1: Core Interface ✅

- [x] Create `ITerminalCapabilities` interface in Lopen.Core
  - Width, Height properties (int)
  - ColorSystem property (Spectre.Console.ColorSystem)
  - SupportsUnicode property (bool)
  - IsInteractive property (bool)
  - IsNoColorSet property (bool)
  - Helper properties: SupportsColor, IsWideTerminal (≥120), IsNarrowTerminal (<60)

### Phase 2: Implementation ✅

- [x] Create `TerminalCapabilities` class
  - Private constructor (use factory method)
  - Static `Detect()` factory method
  - Console.WindowWidth/Height with 80x24 fallback
  - AnsiConsole.Profile.Capabilities for color/unicode
  - Check `!Console.IsInputRedirected && !Console.IsOutputRedirected` for interactive
  - Check NO_COLOR environment variable

### Phase 3: Mock Implementation ✅

- [x] Create `MockTerminalCapabilities` for testing
  - All properties settable via constructor/init
  - Defaults: Width=80, Height=24, Interactive=true, Unicode=true
  - ColorSystem defaults to TrueColor
  - Factory methods: NoColor(), Narrow(), Wide(), NonInteractive()

### Phase 4: Tests ✅

- [x] Unit tests for TerminalCapabilities.Detect() (10 tests)
- [x] Unit tests for MockTerminalCapabilities (9 tests)
- [x] Tests verify NO_COLOR, helper properties, default values

### Phase 5: Documentation ✅

- [x] Update tui/SPECIFICATION.md REQ-020 checkboxes
- [x] Update jobs-to-be-done.json status

## Completed

JTBD-031 implementation complete with 19 new tests (439 total tests passing).

## Next Steps

1. Commit changes
2. Move to next priority task (JTBD-032: TUI Testing)
