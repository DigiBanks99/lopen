# Implementation Plan

> Current Focus: JTBD-030 - TUI AI Streaming (REQ-019) ✅ COMPLETE

## Overview

Implemented buffered paragraph rendering for AI streaming responses. Buffers tokens until paragraph break or timeout to reduce flicker and improve readability.

## Workplan

### Phase 1: Core Infrastructure ✅

- [x] Create `IStreamRenderer` interface in Lopen.Core
  - `RenderStreamAsync(tokenStream, config, cancellationToken)`
- [x] Create `StreamConfig` record for configuration (timeout, token limit)

### Phase 2: Spectre Implementation ✅

- [x] Create `SpectreStreamRenderer` with buffering logic
  - Buffer tokens in StringBuilder
  - Flush on paragraph break (`\n\n`)
  - Flush on timeout (500ms default)
  - Flush on token limit (100 tokens default)
  - Handle code blocks (wait for complete block)
  - Show "Thinking..." indicator initially
- [x] Handle NO_COLOR (plain text, no formatting)
- [x] Add ITimeProvider for testable time

### Phase 3: Mock Implementation ✅

- [x] Create `MockStreamRenderer` for testing
  - Record flush events with content
  - Track timing and cancellation

### Phase 4: Tests ✅

- [x] Unit tests for MockStreamRenderer (14 tests)
- [x] Unit tests for SpectreStreamRenderer (18 tests)
  - Paragraph break triggers flush
  - Timeout triggers flush
  - Code blocks rendered in panels
  - Cancellation handled gracefully

### Phase 5: Documentation ✅

- [x] Update tui/SPECIFICATION.md REQ-019 checkboxes
- [x] Update jobs-to-be-done.json

## Completed

JTBD-030 implementation is complete with 29 new tests (420 total tests passing).

## Next Steps

1. Commit changes
2. Move to next priority task (JTBD-031: TUI Terminal Detection)
