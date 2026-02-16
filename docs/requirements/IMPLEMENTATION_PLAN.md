# Implementation Plan — JOB-045 (TUI-12): Numbered Resource Access

**Goal:** Press 1-9 to open a scrollable resource viewer modal.

## Acceptance Criteria
- [TUI-12] Numbered resource access (press 1-9 to view active resources)

## Tasks

- [x] 1. Add Content property to ResourceItem
- [x] 2. Add ResourceViewer to TuiModalState enum
- [x] 3. Create ResourceViewerData in ModalData.cs
- [x] 4. Create ResourceViewerModalComponent (scrollable, title bar, footer)
- [x] 5. Handle ViewResource1-9 in ApplyAction (open resource viewer)
- [x] 6. Handle Esc/Up/Down in DrainKeyboardInput for resource viewer
- [x] 7. Add resource viewer rendering in RenderFrame
- [x] 8. Add UpdateContextData method to TuiApplication
- [x] 9. Write 13 new tests (10 modal + 3 TuiApp)
- [x] 10. Run full test suite — 1,884 tests pass, 0 failures
- [x] 11. Verify acceptance criteria with sub-agent
- [x] 12. Update state.json, jobs-to-be-done.json, commit