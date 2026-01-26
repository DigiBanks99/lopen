# Implementation Plan

> Current Focus: JTBD-028 - TUI Structured Data (REQ-017) ✅ COMPLETE

## Overview

Implemented structured data display using Spectre.Console Tables and Panels. Enhanced the `sessions list` command with proper table display, following the IDataRenderer pattern.

## Workplan

### Phase 1: Core Infrastructure ✅

- [x] Create `IDataRenderer` interface in Lopen.Core
- [x] Create `TableColumn<T>` and `TableConfig<T>` helper records
- [x] Create `SpectreDataRenderer` implementation with Table/Panel/Metadata
- [x] Create `MockDataRenderer` for testing

### Phase 2: Data Display Methods ✅

- [x] `RenderTable<T>(items, config)` - generic table with columns
- [x] `RenderMetadata(data, title)` - key-value panel display
- [x] `RenderInfo(message)` - informational message
- [x] NO_COLOR fallback (ASCII borders, no colors)
- [x] Row count summary ("X sessions found")

### Phase 3: ConsoleOutput Integration ✅

- [x] Add `Table<T>(items, config)` convenience method
- [x] Add `Metadata(data, title)` convenience method

### Phase 4: Sessions List Enhancement ✅

- [x] Update `sessions list` command to use IDataRenderer
- [x] Display: ID, Modified, Summary columns
- [x] Use RoundedBorder for interactive, ASCII for piped output

### Phase 5: Tests ✅

- [x] Unit tests for MockDataRenderer (11 tests)
- [x] Unit tests for SpectreDataRenderer with TestConsole (13 tests)
- [x] Tests for ConsoleOutput convenience methods (2 tests)

### Phase 6: Documentation ✅

- [x] Update tui/SPECIFICATION.md with completion status
- [x] Update jobs-to-be-done.json

## Completed

JTBD-028 implementation is complete with 24 new tests (362 total tests passing).

## Next Steps

1. Commit changes
2. Move to next priority task (JTBD-029: TUI Right-Side Panels)
