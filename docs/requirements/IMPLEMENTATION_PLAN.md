# Implementation Plan — JOB-039 (TUI-07): Wire SessionResumeModal

**Goal:** Display SessionResumeModalComponent when a previous active session is detected at TUI startup.

## Acceptance Criteria
- [TUI-07] Session resume modal displayed when previous active session detected

## Tasks

- [x] 1. Add `SessionResume` to TuiModalState enum
- [x] 2. Create ISessionDetector interface + SessionDetector implementation
- [x] 3. Add session resume modal rendering in RenderFrame
- [x] 4. Add arrow key + Enter + Escape handling for modal option selection
- [x] 5. Wire session detection at startup (after landing page or directly)
- [x] 6. Register AddSessionDetector() in DI
- [x] 7. Write 20 new tests (SessionDetectorTests + integration tests)
- [x] 8. Run full test suite — 1,844 tests pass, 0 failures
- [x] 9. Verify acceptance criteria with sub-agent
- [x] 10. Update state.json, jobs-to-be-done.json, commit