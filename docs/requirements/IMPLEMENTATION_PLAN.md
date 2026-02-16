# Implementation Plan — JOB-084 (CFG-12): Wire BudgetEnforcer into Orchestration Loop

**Goal:** Ensure `IBudgetEnforcer` is checked before every LLM invocation in the orchestration loop, respecting `token_budget_per_module` and `premium_request_budget` settings. Handle Warning (log), ConfirmationRequired (prompt user / halt in unattended), and Exceeded (halt immediately, save session for resume).

## Tasks

- [x] 1. Add `IBudgetEnforcer?` as optional constructor parameter to `WorkflowOrchestrator`
- [x] 2. Update DI registration in `ServiceCollectionExtensions.cs` to resolve and pass `IBudgetEnforcer`
- [x] 3. Add budget check in `RunStepAsync()` before guardrail evaluation:
  - Read `ITokenTracker.GetSessionMetrics()` (cumulative tokens + premium requests)
  - Call `IBudgetEnforcer.Check(totalTokens, premiumRequestCount)`
  - `Ok` → proceed normally
  - `Warning` → log warning via `_renderer.RenderErrorAsync()`, continue
  - `ConfirmationRequired` → if unattended → return failed step; else → prompt user → if confirmed continue, if declined return failed
  - `Exceeded` → auto-save session, return failed step with budget-exceeded message
- [x] 4. Write unit tests (10 scenarios)
- [x] 5. Update DI registration tests
- [x] 6. Run full test suite — 1,595 tests pass, 0 failures