# Implementation Plan — JOB-082 (LLM-06): Wire OracleVerifier Tool Handlers

**Goal:** Wire the three oracle verification tool handlers (`verify_task_completion`, `verify_component_completion`, `verify_module_completion`) in `ToolHandlerBinder` to actually dispatch `IOracleVerifier.VerifyAsync()` instead of auto-passing. Record the real verdict in `IVerificationTracker`.

## Acceptance Criteria
- [LLM-06] Oracle verification tools dispatch a sub-agent and return pass/fail verdicts
- [LLM-07] Oracle verification runs within the same SDK invocation (no additional premium request)
- [LLM-08] `update_task_status(complete)` rejected without prior passing verification (already implemented)

## Tasks

- [x] 1. Add `IOracleVerifier?` as optional constructor parameter to `ToolHandlerBinder`
- [x] 2. Update DI registration in `ServiceCollectionExtensions.cs` to resolve and pass `IOracleVerifier`
- [x] 3. Update `HandleVerifyTaskCompletion` to call `IOracleVerifier.VerifyAsync()` with evidence/criteria, record real verdict
- [x] 4. Update `HandleVerifyComponentCompletion` to call `IOracleVerifier.VerifyAsync()` with evidence/criteria, record real verdict
- [x] 5. Update `HandleVerifyModuleCompletion` to call `IOracleVerifier.VerifyAsync()` with evidence/criteria, record real verdict
- [x] 6. Handle graceful fallback when `IOracleVerifier` is null (auto-pass with warning, maintaining backward compat)
- [x] 7. Update existing tests and add new tests for oracle dispatch scenarios
- [x] 8. Run full test suite — 1,192 tests pass, 0 failures