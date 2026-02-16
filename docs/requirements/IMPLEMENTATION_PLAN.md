# Implementation Plan — JOB-042 (TUI-08): Progressive Disclosure

**Goal:** Expand current action, collapse previous to summaries. Errors stay expanded.

## Acceptance Criteria
- [TUI-08] Current action expanded, previous actions collapsed to summaries

## Tasks

- [x] 1. Add SelectedEntryIndex to ActivityPanelData
- [x] 2. Update ActivityPanelDataProvider.AddEntry to auto-expand latest (with details), collapse previous non-errors
- [x] 3. Auto-expand error entries regardless of position
- [x] 4. Add ScrollUp/ScrollDown KeyAction + KeyboardHandler mappings
- [x] 5. Handle ToggleExpand, ScrollUp, ScrollDown in TuiApplication.ApplyAction
- [x] 6. Write 13 new tests (7 progressive disclosure + 6 scroll navigation)
- [x] 7. Run full test suite — 1,856 tests pass, 0 failures
- [x] 8. Verify acceptance criteria with sub-agent
- [x] 9. Update state.json, jobs-to-be-done.json, commit