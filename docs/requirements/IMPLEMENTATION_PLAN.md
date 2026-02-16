# Implementation Plan — JOB-041 (TUI-05): Wire PromptAreaComponent Multi-line Input

**Goal:** Make PromptAreaComponent fully functional: backspace editing, context-aware keyboard hints, and user prompt queue for non-slash input to reach the orchestrator.

## Acceptance Criteria
- [TUI-05] Multi-line prompt area with keyboard hints at bottom
- [TUI-20] Multi-line prompt input with Alt+Enter for newlines

## Tasks

- [x] 1. Add `Backspace` KeyAction + KeyboardHandler mapping + ApplyAction handling (delete char before cursor)
- [x] 2. Wire `KeyboardHandler.GetHints()` into TuiApplication render loop → PromptAreaData.CustomHints
- [x] 3. Create `IUserPromptQueue` interface + `UserPromptQueue` implementation (thread-safe)
- [x] 4. Wire SubmitPrompt in ApplyAction to push non-slash text to `IUserPromptQueue`
- [x] 5. Register `IUserPromptQueue` in DI
- [x] 6. Write 17 unit tests (Backspace, hints, queue, DI, integration)
- [x] 7. Run full test suite — 1,806 tests pass, 0 failures
- [x] 8. Verify acceptance criteria with sub-agent
- [x] 9. Update state.json, jobs-to-be-done.json, commit