# Implementation Plan — Current Batch

## JOB-002 (CORE-25): Register Lopen-managed tools with Copilot SDK session ✅

### Context
`CopilotLlmService.InvokeAsync` accepted `IReadOnlyList<LopenToolDefinition> tools` but never passed them to the SDK `SessionConfig.Tools`.

### Tasks
- [x] Study SDK types: `SessionConfig.Tools` is `ICollection<AIFunction>`, `AIFunctionFactory.Create(Delegate, name, description)` converts delegates
- [x] Create `ToolConversion.cs` in `src/Lopen.Llm/` — static class converting `LopenToolDefinition` → `AIFunction` via `AIFunctionFactory.Create`
- [x] Add `Microsoft.Extensions.AI.Abstractions` explicit package reference to `Lopen.Llm.csproj`
- [x] Wire `Tools = ToolConversion.ToAiFunctions(tools)` into `SessionConfig` in `CopilotLlmService`
- [x] Add unit tests for `ToolConversion` — 11 tests covering null guard, filtering, name/description mapping, handler invocation, order preservation
- [x] Run all tests — 2,099 passed, 0 failures
- [x] Update module state and jobs-to-be-done
- [x] Commit changes