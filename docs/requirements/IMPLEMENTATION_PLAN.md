# Implementation Plan — JOB-046 (TUI-13): Inline Research Display

**Goal:** Research entries inline in activity panel with drill-into full document.

## Acceptance Criteria
- [TUI-13] Inline research display with ability to drill into full document

## Tasks

- [x] 1. Add Research to ActivityEntryKind enum with 📖 prefix
- [x] 2. Add FullDocumentContent property to ActivityEntry
- [x] 3. Show "[Press Enter to view full document]" hint when expanded + full doc exists
- [x] 4. ToggleExpand on expanded entry with FullDocumentContent opens ResourceViewerModal
- [x] 5. Write 7 new tests (6 component + 1 integration)
- [x] 6. Run full test suite — 1,891 tests pass, 0 failures
- [x] 7. Verify acceptance criteria with sub-agent
- [x] 8. Update state.json, jobs-to-be-done.json, commit