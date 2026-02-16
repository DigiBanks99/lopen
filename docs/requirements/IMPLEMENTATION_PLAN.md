# Implementation Plan — JOB-073 (CORE-23): Critical System Error Handling

**Goal:** Ensure critical system errors (disk full, auth failure, SDK errors, unrecoverable exceptions) block workflow execution and require user action. The orchestrator must detect critical errors, call `RecordCriticalError()`, render a clear blocking message, auto-save session, and return an interrupted result.

## Tasks

- [x] 1. Add `IsCriticalException` helper to classify exceptions as critical (IOException, UnauthorizedAccessException, OutOfMemoryException, SecurityException)
- [x] 2. Update `InvokeLlmForStepAsync` catch block to detect critical exceptions and mark `StepResult` with `IsCriticalError` flag
- [x] 3. Update `StepResult` with `IsCriticalError` property and `CriticalFailure` factory method
- [x] 4. Update `RunAsync` failure handling to call `RecordCriticalError()` when `stepResult.IsCriticalError` is true, render critical error message, auto-save, and return `OrchestrationResult.CriticalError`
- [x] 5. Add `IsCriticalError` property and `CriticalError` factory to `OrchestrationResult`
- [x] 6. Write unit tests for critical error flow through orchestrator (12 scenarios)
- [x] 7. Update AC-23 acceptance tests (3 tests: handler classification, OrchestrationResult, StepResult)
- [x] 8. Run full test suite — 1,534 tests pass, 0 failures