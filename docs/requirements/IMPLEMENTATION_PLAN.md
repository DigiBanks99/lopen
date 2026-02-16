# Implementation Plan — JOB-036 (TUI-38/TUI-39): Wire SlashCommandRegistry

**Goal:** Connect the existing SlashCommandRegistry to TuiApplication so slash commands typed in the prompt area invoke corresponding CLI logic, and unknown commands display an error with valid command list.

## Acceptance Criteria
- [TUI-38] Slash commands (`/help`, `/spec`, `/plan`, `/build`, `/session`, `/config`, `/revert`, `/auth`) invoke corresponding CLI commands
- [TUI-39] Unknown slash commands display error with valid command list

## Tasks

- [x] 1. Create `ISlashCommandExecutor` interface with `ExecuteAsync(string input, CancellationToken)` returning `SlashCommandResult`
- [x] 2. Create `SlashCommandResult` record with `IsSuccess`, `OutputMessage`, and `ErrorMessage` properties
- [x] 3. Create `SlashCommandExecutor` implementation that parses input via `SlashCommandRegistry.TryParse()`, delegates to appropriate handlers for known commands, and returns error for unknown slash commands with valid command list
- [x] 4. Wire `SlashCommandRegistry` (as singleton via `CreateDefault()`) and `ISlashCommandExecutor` into DI in `AddLopenTui()`
- [x] 5. Add `ISlashCommandExecutor` dependency to `TuiApplication` constructor (optional parameter)
- [x] 6. Update `TuiApplication.ApplyAction` `SubmitPrompt` case: if text starts with `/`, delegate to `ISlashCommandExecutor`; add result to activity panel entries
- [x] 7. Write unit tests for `SlashCommandExecutor` — 37 tests covering parsing, known commands, unknown commands, error display, DI, TUI integration
- [x] 8. Run full test suite — 1,729 tests pass, 0 failures
- [x] 9. Verify acceptance criteria with sub-agent
- [x] 10. Update module state and jobs-to-be-done, commit