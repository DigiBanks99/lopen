# Implementation Plan — JOB-074 (CORE-24): Module Selection UI

**Goal:** List modules with current state and allow user to choose one when no session-based module is available.

## Acceptance Criteria
- [CORE-24] Module selection lists modules with current state and allows user to choose

## Tasks

- [x] 1. Create `IModuleSelectionService` interface with `SelectModuleAsync`
- [x] 2. Create `ModuleSelectionService` — lists modules via IModuleLister, displays with status indicators, prompts for selection by number or name
- [x] 3. Register `IModuleSelectionService` in DI via `AddLopenCore()`
- [x] 4. Wire into `PhaseCommands.ResolveModuleNameAsync` as fallback when session has no module
- [x] 5. Write 17 unit tests (parsing, formatting, DI, edge cases)
- [x] 6. Run full test suite — 1,765 tests pass, 0 failures
- [x] 7. Verify acceptance criteria with sub-agent
- [x] 8. Update module state and commit