# Implementation Plan

> Current Focus: JTBD-033 - TUI Welcome Header (REQ-022) ✅ COMPLETE

## Overview

Responsive welcome header for REPL with version info, session name, context window display, and ASCII logo. Adapts to terminal width.

## Workplan

### Phase 1: Core Interface & Models ✅

- [x] Create `IWelcomeHeaderRenderer` interface
- [x] Create `WelcomeHeaderContext` record (version, session, context, preferences)
- [x] Create `ContextWindowInfo` record (tokens, messages, format methods)
- [x] Create `WelcomeHeaderPreferences` record (showLogo, showTip, showContext, showSession)

### Phase 2: ASCII Logo ✅

- [x] Create `AsciiLogoProvider` class with full/compact/minimal logos
- [x] Wind Runner sigil ASCII art for full display

### Phase 3: Spectre Implementation ✅

- [x] Create `SpectreWelcomeHeaderRenderer` implementing interface
- [x] Render full header (width ≥ 80): logo, version, tagline, tip, session, context
- [x] Render compact header (50-79): version with accent, tagline, session
- [x] Render minimal header (<50): version line only
- [x] Respect NO_COLOR and terminal capabilities

### Phase 4: Mock Implementation ✅

- [x] Create `MockWelcomeHeaderRenderer` for testing

### Phase 5: Tests ✅

- [x] Unit tests for ContextWindowInfo (10 tests)
- [x] Unit tests for AsciiLogoProvider (8 tests)
- [x] Unit tests for MockWelcomeHeaderRenderer (5 tests)
- [x] Unit tests for SpectreWelcomeHeaderRenderer (10 tests)

### Phase 6: Documentation ✅

- [x] Update tui/SPECIFICATION.md REQ-022 checkboxes
- [x] Update jobs-to-be-done.json status

## Completed

JTBD-033 implementation complete with 31 new tests (480 total tests passing).

## Previous Completion

- JTBD-031: TUI Terminal Detection ✅
- JTBD-032: TUI Testing ✅

## Next Steps

1. Commit changes
2. Move to next priority task (JTBD-034: Self-Testing Command)
