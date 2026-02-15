# Implementation Plan

## Current Focus: JOB-009 — Core Module Foundation ✅

- [x] Update `Lopen.Core.csproj` with packages (Markdig, Stateless, System.IO.Hashing, Logging.Abstractions, Options) and `InternalsVisibleTo`
- [x] Add project references to Configuration, Llm, Storage
- [x] Update `Lopen.Core.Tests.csproj` with test dependencies
- [x] Create enums: `WorkflowStep`, `WorkflowTrigger`, `WorkNodeState`
- [x] Create records: `GuardrailResult` (Pass/Warn/Block), `GuardrailContext`, `CachedSection`, `DocumentSection`, `GitResult`
- [x] Create exceptions: `GitException`, `InvalidStateTransitionException`
- [x] Create interfaces: `IStateAssessor`, `IGuardrail`, `IGuardrailPipeline`, `IGitService`, `ISpecificationParser`, `IContentHasher`
- [x] Create task hierarchy: `IWorkNode`, `WorkNode<T>`, `ModuleNode`, `ComponentNode`, `TaskNode`, `SubtaskNode`, `WorkNodeExtensions`
- [x] Create implementations: `GitCliService` (stub), `MarkdigSpecificationParser`, `XxHashContentHasher`, `GuardrailPipeline`
- [x] Update `ServiceCollectionExtensions` with all registrations
- [x] Write 135 comprehensive tests (enums, records, exceptions, hierarchy, implementations, DI)
- [x] Verify `dotnet build` (0 warnings, 0 errors), `dotnet test` (362 total), `dotnet format`
- [x] Update `jobs-to-be-done.json`
