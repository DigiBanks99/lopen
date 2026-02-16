# Implementation Plan — JOB-012 (TUI-03): Wire ContextPanelComponent to Live Data Sources

**Goal:** Connect the ContextPanelComponent to live task tree data from IPlanManager and IWorkflowEngine so the context panel displays real-time task hierarchy, component progress, and active resources.

## Acceptance Criteria
- [TUI-03] Context panel shows current task, task tree with completion states, and active resources
- [TUI-10] Real-time task progress updates in context panel (subset)
- [TUI-31] Real-time progress percentages in context panel (subset)

## Tasks

- [x] 1. Add `Lopen.Storage` project reference to `Lopen.Tui.csproj`
- [x] 2. Create `IContextPanelDataProvider` interface with `GetCurrentData()`, `RefreshAsync()`, and `SetActiveModule()`
- [x] 3. Create `ContextPanelDataProvider` implementation mapping `IPlanManager` tasks to `ContextPanelData` hierarchy
- [x] 4. Wire `IContextPanelDataProvider` into `TuiApplication` with throttled refresh (same pattern as top panel)
- [x] 5. Register `ContextPanelDataProvider` in DI (`ServiceCollectionExtensions`)
- [x] 6. Write unit tests for `ContextPanelDataProvider` — 30 tests
- [x] 7. Write integration tests for `TuiApplication` context panel data wiring — 2 tests
- [x] 8. Run full test suite — 1,692 tests pass, 0 failures
- [x] 9. Update module state and commit