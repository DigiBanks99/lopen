# Implementation Plan

## Current Job: JOB-046 — Copilot SDK Authentication

**Module**: llm  
**Priority**: P3  
**AC**: Lopen authenticates with the Copilot SDK using credentials from the Auth module

### Tasks

- [ ] 1. Add `GitHub.Copilot.SDK` v0.1.23 NuGet package to Lopen.Llm (DONE)
- [ ] 2. Create `IGitHubTokenProvider` interface in Lopen.Llm for auth decoupling
- [ ] 3. Create `ICopilotClientProvider` interface for CopilotClient lifecycle management
- [ ] 4. Implement `CopilotClientProvider` wrapping CopilotClient creation with auth token injection
- [ ] 5. Implement `CopilotLlmService` replacing `StubLlmService`, using ICopilotClientProvider
- [ ] 6. Update DI registration in `ServiceCollectionExtensions.AddLopenLlm`
- [ ] 7. Write unit tests for auth token injection, auth failure handling, client lifecycle
- [ ] 8. Update module state in `.lopen/module/llm/state.json`

### Design Decisions

- `IGitHubTokenProvider` lives in Lopen.Llm to avoid LLM→Auth dependency
- Composition root wires Auth module's `ITokenSourceResolver` to provide tokens
- SDK's built-in credential chain used as fallback (env vars, gh CLI stored credentials)
- `CopilotClientProvider` creates a singleton `CopilotClient` per application lifecycle
- Auth failures from SDK throw `LlmException` with clear error messages

### Previous Completed Jobs

| Job | Module | Description | Tests |
|-----|--------|-------------|-------|
| JOB-051 | llm | OracleVerifier | 30 tests |
| JOB-054 | llm | Model selection | 9 tests |
| JOB-055 | llm | Token tracking | 8 tests |
| JOB-065 | core | Quality gate guardrail | 9 tests |
| JOB-070 | core | GitWorkflowService | 25 tests |
| JOB-071 | core | RevertService | 11 tests |
| JOB-028 | configuration | BudgetEnforcer | 24 tests |
| JOB-036 | storage | AutoSaveService | 15 tests |
| JOB-038 | storage | PlanManager | 28 tests |
| JOB-039 | storage | SectionCache | 20 tests |
| JOB-040 | storage | AssessmentCache | 18 tests |
