# Implementation Plan

> Current Focus: JTBD-027 - TUI Error Display (REQ-016) ✅ COMPLETE

## Overview

Implemented structured error display with contextual correction guidance using Spectre.Console. This includes panel-based errors, "Did you mean?" suggestions, and NO_COLOR fallback.

## Workplan

### Phase 1: Core Infrastructure ✅

- [x] Create `IErrorRenderer` interface in Lopen.Core
- [x] Create `ErrorInfo` record with Title, Message, Suggestions, TryCommand
- [x] Create `ErrorSeverity` enum (Error, Warning, Validation)
- [x] Create `SpectreErrorRenderer` implementation
- [x] Create `MockErrorRenderer` for testing

### Phase 2: Error Display Methods ✅

- [x] `RenderSimpleError(message, suggestion?)` - single line with suggestion
- [x] `RenderPanelError(title, message, suggestions)` - bordered panel
- [x] `RenderValidationError(input, message, validOptions)` - inline context
- [x] `RenderCommandNotFound(command, suggestions)` - "Did you mean?"
- [x] NO_COLOR fallback for all methods

### Phase 3: ConsoleOutput Integration ✅

- [x] Add `ErrorWithSuggestion(message, suggestion)` convenience method
- [x] Add `ErrorPanel(title, message, suggestions)` convenience method
- [x] Add `CommandNotFoundError(command, suggestions)` convenience method
- [x] Add `ValidationError(input, message, validOptions)` convenience method

### Phase 4: Tests ✅

- [x] Unit tests for MockErrorRenderer (13 tests)
- [x] Unit tests for SpectreErrorRenderer with TestConsole (18 tests)
- [x] Tests for NO_COLOR fallback behavior (5 tests)
- [x] Tests for ConsoleOutput convenience methods (4 tests)

### Phase 5: Documentation ✅

- [x] Update tui/SPECIFICATION.md with completion status
- [x] Update jobs-to-be-done.json

## Completed

JTBD-027 implementation is complete with 35 new tests (338 total tests passing).

## Next Steps

1. Commit changes
2. Move to next priority task (JTBD-028: TUI Structured Data)
