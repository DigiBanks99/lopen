# Implementation Plan — JOB-079 (STOR-09): Wire PlanManager into Orchestration Loop

**Goal:** Wire the already-implemented `IPlanManager` into `WorkflowOrchestrator` so that plans are persisted to `.lopen/modules/{module}/plan.md` with checkbox task hierarchy during the workflow.

## Acceptance Criteria
- [STOR-09] Plans stored at `.lopen/modules/{module}/plan.md` with checkbox task hierarchy
- [STOR-10] Plan checkboxes are updated programmatically by Lopen, not by the LLM (already implemented in ToolHandlerBinder)

## Tasks

- [x] 1. Add `IPlanManager?` as optional constructor parameter to `WorkflowOrchestrator`
- [x] 2. Update DI registration in `ServiceCollectionExtensions.cs` to resolve and pass `IPlanManager`
- [x] 3. After `BreakIntoTasks` step succeeds, persist plan content via `WritePlanAsync` (append to existing plan for multi-component workflows)
- [x] 4. Update test helper `CreateOrchestrator()` to compile with new parameter
- [x] 5. Add tests for plan writing after BreakIntoTasks succeeds
- [x] 6. Add test for plan appending when existing plan content exists
- [x] 7. Add test for graceful no-op when IPlanManager is null
- [x] 8. Run full test suite — 1,627 tests pass, 0 failures