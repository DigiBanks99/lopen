# Implementation Plan — Current Batch

## JOB-002 (CORE-25): Register Lopen-managed tools with Copilot SDK session ✅

### Tasks
- [x] Create `ToolConversion.cs` — converts `LopenToolDefinition` → `AIFunction`
- [x] Wire `SessionConfig.Tools` in `CopilotLlmService`
- [x] 11 tests in `ToolConversionTests`

## JOB-004 (TUI-50): Implement TuiOutputRenderer ✅

### Tasks
- [x] Create `TuiOutputRenderer.cs` — bridges IOutputRenderer to IActivityPanelDataProvider
- [x] Add `AddTuiOutputRenderer()` DI registration
- [x] 16 tests in `TuiOutputRendererTests`

## JOB-003 (TUI-52): Bridge TUI to WorkflowOrchestrator ✅

### Tasks
- [x] Add `IWorkflowOrchestrator?` to `TuiApplication` constructor
- [x] `TryLaunchOrchestrator` / `CheckOrchestratorCompletion` in render loop
- [x] 11 tests in `TuiOrchestratorBridgeTests`