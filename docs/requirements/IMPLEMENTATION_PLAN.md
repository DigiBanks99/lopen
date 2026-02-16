# Implementation Plan — JOB-071 (CORE-21): Single Task Failure Self-Correction

**Goal:** On single task failure, display error inline and let the LLM self-correct instead of immediately interrupting the workflow.

## Background

- `FailureHandler` / `IFailureHandler` exist in `src/Lopen.Core/Workflow/` with full tests
- `WorkflowOrchestrator.RunAsync()` returns `Interrupted` immediately on any step failure (blocks self-correction)
- `IFailureHandler` is **not** registered in DI and **not** wired into `WorkflowOrchestrator`

## Tasks

- [x] 1. Register `IFailureHandler` in `AddLopenCore()` (`ServiceCollectionExtensions.cs`), sourcing `FailureThreshold` from `WorkflowOptions`
- [x] 2. Add optional `IFailureHandler?` parameter to `WorkflowOrchestrator` constructor
- [x] 3. Modify `RunAsync()`: on step failure, call `RecordFailure()`. If `SelfCorrect` → render error inline, continue loop. If `PromptUser`/`Block` → return `Interrupted` (existing behavior)
- [x] 4. On successful step completion, reset failure count (task id = step name)
- [x] 5. Add task identifier (current step name) for failure tracking
- [x] 6. Unit tests: failure with handler → continues (self-correct); failure without handler → `Interrupted` (backward compat); success resets count
- [x] 7. Test `IFailureHandler` DI registration in `ServiceCollectionExtensions` tests
- [x] 8. Run full test suite — verify green (1578 tests, 0 failures)