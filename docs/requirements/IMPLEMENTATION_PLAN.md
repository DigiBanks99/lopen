# Implementation Plan

## Current Focus: LLM Module P3 — JOB-048, JOB-050, JOB-052, JOB-053

### Context

- Interfaces exist: `IPromptBuilder`, `IToolRegistry` in `Lopen.Llm/`
- Supporting types exist: `WorkflowPhase` (4 phases), `LopenToolDefinition` (record with phase list), `VerificationScope` (Task/Component/Module)
- DI registration in `ServiceCollectionExtensions.AddLopenLlm()` — currently registers `ILlmService`, `IModelSelector`, `ITokenTracker`
- All jobs are pure logic — no Copilot SDK runtime dependency required

### Tasks

#### JOB-048: System Prompt Assembly (`IPromptBuilder`)

- [ ] Create `DefaultPromptBuilder : IPromptBuilder` in `Lopen.Llm/`
- [ ] `BuildSystemPrompt` assembles 6 labeled sections: role/identity, workflow state, step instructions, context sections, available tools, constraints
- [ ] Context sections populated from `contextSections` dictionary parameter
- [ ] Register `IPromptBuilder → DefaultPromptBuilder` in DI
- [ ] Write `DefaultPromptBuilderTests` — all phases, null/empty context, section ordering

#### JOB-050 + JOB-053: Tool Registry with Phase Filtering (`IToolRegistry`)

- [ ] Create `DefaultToolRegistry : IToolRegistry` in `Lopen.Llm/`
- [ ] Pre-register 10 Lopen-managed tools (7 orchestration + 3 verification) with descriptions and `AvailableInPhases`
- [ ] `GetToolsForPhase()` returns only tools whose `AvailableInPhases` includes that phase
- [ ] `RegisterTool()` adds custom tools
- [ ] `GetAllTools()` returns all registered tools
- [ ] Phase filtering: `log_research` only in Research, `verify_*` only in Building
- [ ] Register `IToolRegistry → DefaultToolRegistry` in DI
- [ ] Write `DefaultToolRegistryTests` — phase filtering, custom tool registration, all-tools listing

#### JOB-052: Back-Pressure / Verification Tracker

- [ ] Create `IVerificationTracker` interface in `Lopen.Llm/`
- [ ] Create `VerificationTracker : IVerificationTracker` in `Lopen.Llm/`
- [ ] `RecordVerification(scope, identifier, passed)` records results
- [ ] `IsVerified(scope, identifier)` returns true only if a passing verification exists
- [ ] `ResetForInvocation()` clears all state
- [ ] Register `IVerificationTracker → VerificationTracker` in DI
- [ ] Write `VerificationTrackerTests` — record/query, failed does not verify, reset clears, scope isolation

#### Cross-cutting

- [ ] Update `ServiceCollectionExtensions` with all 3 new registrations
- [ ] Update `ServiceCollectionExtensionsTests` to verify new registrations
- [ ] All tests pass (existing + new)
