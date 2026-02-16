# Implementation Plan — JOB-072 (CORE-22): Repeated Failure Escalation

**Goal:** When consecutive failures hit the configured threshold (default 3), prompt the user to confirm continuation instead of silently interrupting. In unattended mode, skip the prompt and continue automatically.

## Tasks

- [x] 1. Add `WorkflowOptions?` as optional constructor parameter to `WorkflowOrchestrator`
- [x] 2. Update DI registration in `ServiceCollectionExtensions.cs` to resolve and pass `WorkflowOptions` to `WorkflowOrchestrator`
- [x] 3. In `RunAsync()`, handle `FailureAction.PromptUser`:
  - If `WorkflowOptions.Unattended` is true → log warning, continue (like `SelfCorrect`)
  - Otherwise → call `_renderer.PromptAsync()` with message including task name and failure count
  - If user responds affirmatively (`y`/`yes`) → continue loop (retry)
  - If user responds negatively or `null` (headless) → return `Interrupted`
- [x] 4. Write unit tests:
  - `PromptUser` + user confirms `"y"` → continues and retries
  - `PromptUser` + user declines `"n"` → returns `Interrupted`
  - `PromptUser` + headless (`null` response) → returns `Interrupted`
  - `PromptUser` + unattended mode → continues without prompting
  - Prompt message includes task name and failure count
- [x] 5. Run full test suite — 1585 tests pass, 0 failures