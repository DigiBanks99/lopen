# Implementation Plan

> Current Focus: JTBD-029 - TUI Right-Side Panels (REQ-018) ✅ COMPLETE

## Overview

Implemented split-screen layouts with right-side task/status panels for enhanced context in interactive REPL mode. Uses Spectre.Console Layout with SplitColumns for 70/30 split and responsive fallback for narrow terminals.

## Workplan

### Phase 1: Core Infrastructure ✅

- [x] Create `ILayoutRenderer` interface in Lopen.Core
  - `RenderSplitLayout(mainContent, sidePanel?, config)`
  - `RenderTaskPanel(tasks)` for task list display
  - `RenderContextPanel(data, title)` for session/context display
- [x] Create `TaskItem` record for task list state (status enum, name)
- [x] Create `TaskStatus` enum (Pending, InProgress, Completed, Failed)
- [x] Create `SplitLayoutConfig` record for layout configuration

### Phase 2: Spectre Implementation ✅

- [x] Create `SpectreLayoutRenderer` with `IAnsiConsole` injection
  - Detect terminal width via `_console.Profile.Width`
  - Split layout when width >= 100 chars
  - Fallback to full-width main content when narrow
- [x] Implement 70/30 ratio split layout
- [x] Handle NO_COLOR (ASCII borders, no colors)

### Phase 3: Mock Implementation ✅

- [x] Create `MockLayoutRenderer` for testing
  - Record all render calls (SplitLayoutCall, TaskPanelCall, ContextPanelCall)
  - Store main content, panel content, width threshold
- [x] Add Reset() method for test cleanup

### Phase 4: ConsoleOutput Integration ✅

- [x] Add `SplitLayout(mainContent, sidePanel, config)` convenience method
- [x] Add `TaskPanel(tasks)` convenience method
- [x] Add `ContextPanel(data, title)` convenience method

### Phase 5: Tests ✅

- [x] Unit tests for MockLayoutRenderer (15 tests)
- [x] Unit tests for SpectreLayoutRenderer with TestConsole (17 tests)

### Phase 6: Documentation ✅

- [x] Update tui/SPECIFICATION.md REQ-018 checkboxes
- [x] Update jobs-to-be-done.json

## Completed

JTBD-029 implementation is complete with 32 new tests (391 total tests passing).

## Next Steps

1. Commit changes
2. Move to next priority task (JTBD-030: TUI AI Streaming)
