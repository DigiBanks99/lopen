# Implementation Plan — JOB-011 (TUI-02): Wire TopPanelComponent to Live Data Sources

**Goal:** Connect the TopPanelComponent to real-time data from ITokenTracker, IGitService, IAuthService, IWorkflowEngine, and IModelSelector so the top panel displays live information.

## Acceptance Criteria
- [TUI-02] Top panel displays logo, version, model, context usage, premium requests, git branch, auth status, phase, and step
- [TUI-29] Context window usage displayed in top panel (subset of TUI-02)

## Tasks

- [x] 1. Add `GetCurrentBranchAsync` to `IGitService` interface and implement in `GitCliService`
- [x] 2. Create `ITopPanelDataProvider` interface in `Lopen.Tui` with `TopPanelData GetCurrentData()` and `Task RefreshAsync(CancellationToken)`
- [x] 3. Create `TopPanelDataProvider` implementation that aggregates ITokenTracker, IGitService, IAuthService, IWorkflowEngine, IModelSelector
- [x] 4. Add `Lopen.Auth` project reference to `Lopen.Tui.csproj`
- [x] 5. Modify `TuiApplication` to accept `ITopPanelDataProvider` and refresh top panel data with throttling (every ~1s, not every frame)
- [x] 6. Register `TopPanelDataProvider` in DI (`ServiceCollectionExtensions`)
- [x] 7. Write unit tests for `TopPanelDataProvider` — 31 tests
- [x] 8. Write integration tests for `TuiApplication` data provider wiring — 4 tests
- [x] 9. Run full test suite — 1,662 tests pass, 0 failures
- [x] 10. Update module state and commit