# Implementation Plan — JOB-037 (TUI-41): Ctrl+P Pause Agent Execution

**Goal:** Wire Ctrl+P to pause/resume the workflow orchestrator via a shared IPauseController.

## Acceptance Criteria
- [TUI-41] `Ctrl+P` pauses agent execution

## Tasks

- [x] 1. Create `IPauseController` interface in Lopen.Core with Pause/Resume/Toggle/WaitIfPausedAsync/IsPaused
- [x] 2. Create `PauseController` implementation (thread-safe with SemaphoreSlim + lock)
- [x] 3. Wire `IPauseController` into `WorkflowOrchestrator` — pause gate at top of main loop
- [x] 4. Wire `IPauseController` into `TuiApplication.TogglePause` action
- [x] 5. Register `IPauseController` as `PauseController` singleton in `AddLopenCore()` DI
- [x] 6. Pass `IPauseController` to orchestrator via DI factory
- [x] 7. Write unit tests for `PauseController` — 14 tests
- [x] 8. Write orchestrator pause integration tests — 5 tests
- [x] 9. Run full test suite — 1,748 tests pass, 0 failures
- [x] 10. Verify acceptance criteria with sub-agent
- [x] 11. Update module state and jobs-to-be-done, commit