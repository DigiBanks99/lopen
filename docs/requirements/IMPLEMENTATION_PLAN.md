# Implementation Plan

## Current Job: JOB-081 — Implement `lopen config show` and `lopen revert` Subcommands

**Module**: cli  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Implement `lopen config show` (with `--json` flag) and `lopen revert` subcommands wired to Configuration and Core modules.

### Acceptance Criteria

- [x] AC14: `lopen config show` displays resolved configuration with sources (table format default, `--json` for JSON)
- [x] AC15: `lopen revert` rolls back to the last task-completion commit and updates session state

### Tasks

- [x] **1. Register `IConfigurationRoot` in DI** — Updated `AddLopenConfiguration` to preserve and register it
- [x] **2. Create `ConfigCommand.cs`** — `config show` with `--json` option using `ConfigurationDiagnostics`
- [x] **3. Create `RevertCommand.cs`** — Resolves commit SHA from session state, calls `IRevertService`, updates session state
- [x] **4. Wire in `Program.cs`** — Add both commands to root
- [x] **5. Write tests** — 4 ConfigCommandTests + 7 RevertCommandTests + FakeRevertService
- [x] **6. Validate** — 1124 tests pass, formatting clean

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-081 | cli | Config show + revert subcommands |
| JOB-080 | cli | Session CLI subcommands (list/show/resume/delete/prune) |
| JOB-079 | cli | Auth CLI subcommands (login/status/logout) |
| JOB-075 | core | Core AC tests (all 24 ACs) |