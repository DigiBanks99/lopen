# Implementation Plan

## Current Job: JOB-001 — Register Core Workflow Services in DI

**Module**: core · **Requirement**: CORE-02

### Goal

Register `IWorkflowEngine`, `IStateAssessor`, and `IPhaseTransitionController` in the DI container via `AddLopenCore()`.

### DI Registrations

**File**: `src/Lopen.Core/ServiceCollectionExtensions.cs`

| Interface | Implementation | Lifetime | Conditional |
|-----------|---------------|----------|-------------|
| `IPhaseTransitionController` | `PhaseTransitionController` | Singleton | No — only needs `ILogger` |
| `IStateAssessor` | `CodebaseStateAssessor` | Singleton | Yes — needs `IFileSystem` + `IModuleScanner` |
| `IWorkflowEngine` | `WorkflowEngine` | Singleton | Yes — needs `IStateAssessor` |

- `IPhaseTransitionController` goes in the **unconditional** (always) block.
- `IStateAssessor` and `IWorkflowEngine` go inside the **`projectRoot` guard** block, since `CodebaseStateAssessor` depends on `IFileSystem` and `IModuleScanner` which are only registered when `projectRoot` is provided.

### Tests

**File**: `tests/Lopen.Core.Tests/ServiceCollectionExtensionsTests.cs`

| Test | Validates |
|------|-----------|
| `AddLopenCore_WithProjectRoot_RegistersWorkflowEngine` | `IWorkflowEngine` resolves when projectRoot given |
| `AddLopenCore_WithProjectRoot_RegistersStateAssessor` | `IStateAssessor` resolves when projectRoot given |
| `AddLopenCore_RegistersPhaseTransitionController` | `IPhaseTransitionController` resolves unconditionally |
| `AddLopenCore_WorkflowEngine_IsSingleton` | Same instance returned on repeated resolve |
| `AddLopenCore_WithoutProjectRoot_DoesNotRegisterWorkflowEngine` | `IWorkflowEngine` is not registered without projectRoot |

### Upcoming Priority Jobs

| Job | Description |
|-----|-------------|
| JOB-002 | Implement main orchestration loop |
| JOB-003 | Implement tool handlers for LLM tools |
| JOB-004–007 | Wire CLI commands to workflow engine |