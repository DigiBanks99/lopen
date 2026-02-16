# Implementation Plan — JOB-047 (TUI-14): Phase Transition Summaries

**Goal:** Phase transition summaries in activity area with ◆ prefix.

## Acceptance Criteria
- [TUI-14] Phase transition summaries shown in activity area

## Tasks

- [x] 1. Add AddPhaseTransition to IActivityPanelDataProvider interface
- [x] 2. Implement AddPhaseTransition in ActivityPanelDataProvider
- [x] 3. Write 6 new tests (kind, summary, details, expansion, collapse)
- [x] 4. Run full test suite — 1,897 tests pass, 0 failures
- [x] 5. Verify acceptance criteria with sub-agent
- [x] 6. Update state.json, jobs-to-be-done.json, commit