# Implementation Plan

## Current Focus: JOB-008 — LLM Module Foundation ✅

- [x] Update `Lopen.Llm.csproj` with package references (`DI.Abstractions`, `Logging.Abstractions`, `Options`) and `InternalsVisibleTo`
- [x] Update `Lopen.Llm.Tests.csproj` with needed package references (`DI`, `Logging`, `Logging.Abstractions`, `Options`)
- [x] Create `WorkflowPhase` enum (RequirementGathering, Planning, Building, Research)
- [x] Create `VerificationScope` enum (Task, Component, Module)
- [x] Create `TokenUsage` record (InputTokens, OutputTokens, TotalTokens, ContextWindowSize, IsPremiumRequest)
- [x] Create `SessionTokenMetrics` record (PerIterationTokens list, CumulativeInputTokens, CumulativeOutputTokens, PremiumRequestCount)
- [x] Create `OracleVerdict` record (Passed bool, Gaps string list, Scope VerificationScope)
- [x] Create `ModelFallbackResult` record (SelectedModel, WasFallback, OriginalModel)
- [x] Create `LlmInvocationResult` record (Output string, TokenUsage, ToolCallsMade int, IsComplete bool)
- [x] Create `LopenToolDefinition` record (Name, Description, ParameterSchema, AvailableInPhases)
- [x] Create `LlmException` for LLM-specific failures (auth failure, rate limit, model unavailable)
- [x] Create `ILlmService` interface (InvokeAsync with prompt/model/tools, returns LlmInvocationResult)
- [x] Create `IModelSelector` interface (SelectModel with WorkflowPhase, returns ModelFallbackResult)
- [x] Create `ITokenTracker` interface (RecordUsage, GetSessionMetrics, ResetSession)
- [x] Create `IToolRegistry` interface (GetToolsForPhase with WorkflowPhase, RegisterTool, GetAllTools)
- [x] Create `IPromptBuilder` interface (BuildSystemPrompt with workflow state parameters)
- [x] Create `IOracleVerifier` interface (VerifyAsync with scope/evidence/criteria, returns OracleVerdict)
- [x] Create `StubLlmService` internal sealed implementation (throws LlmException "Copilot SDK integration pending")
- [x] Create `DefaultModelSelector` internal sealed implementation (reads from LopenOptions.Models, returns configured model per phase)
- [x] Create `InMemoryTokenTracker` internal sealed implementation (tracks metrics in memory)
- [x] Create `ServiceCollectionExtensions` with `AddLopenLlm()` method
- [x] Add project reference from `Lopen.Llm` to `Lopen.Configuration` (for LopenOptions)
- [x] Wire LLM module into `Program.cs` (`AddLopenLlm`)
- [x] Add project reference from `Lopen.csproj` to `Lopen.Llm`
- [x] Write unit tests for `WorkflowPhase` enum
- [x] Write unit tests for `VerificationScope` enum
- [x] Write unit tests for value records (TokenUsage, SessionTokenMetrics, OracleVerdict, ModelFallbackResult, LlmInvocationResult, LopenToolDefinition)
- [x] Write unit tests for `LlmException` (constructors, properties, inheritance)
- [x] Write unit tests for `DefaultModelSelector` (per-phase selection, fallback when model empty)
- [x] Write unit tests for `InMemoryTokenTracker` (record usage, get metrics, reset, cumulative tracking, premium count)
- [x] Write unit tests for `StubLlmService` (throws LlmException on invocation)
- [x] Write unit tests for `ServiceCollectionExtensions` (registers services, singletons, fluent return)
- [x] Verify `dotnet build` and `dotnet test` pass (74 LLM tests, 227 total)
- [x] Run `dotnet format --verify-no-changes`
