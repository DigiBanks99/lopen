# Implementation Plan — JOB-038 (TUI-06): Wire LandingPageComponent

**Goal:** Display LandingPageComponent as a modal overlay on first TUI startup, dismissible on any keypress, skippable with --no-welcome.

## Acceptance Criteria
- [TUI-06] Landing page modal with quick commands on first startup (skippable with `--no-welcome`)

## Tasks

- [x] 1. Add `TuiModalState` enum (None, LandingPage)
- [x] 2. Add modal state, LandingPageData fields, showLandingPage constructor param to TuiApplication
- [x] 3. Set modal state to LandingPage on RunAsync start (when showLandingPage=true)
- [x] 4. Wire modal rendering in RenderFrame (fullscreen when modal active)
- [x] 5. Wire keypress dismissal in DrainKeyboardInput (any key → None)
- [x] 6. Sync landing page version/auth from TopPanelDataProvider
- [x] 7. Write 15 unit tests (TuiLandingPageTests + TuiApplicationTests)
- [x] 8. Run full test suite — 1,819 tests pass, 0 failures
- [x] 9. Verify acceptance criteria with sub-agent
- [x] 10. Update state.json, jobs-to-be-done.json, commit