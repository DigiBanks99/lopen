# Implementation Plan

## Current Job: JOB-078 — Implement spec/plan/build Phase Subcommands

**Module**: cli  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Implement `lopen spec`, `lopen plan`, `lopen build` subcommands with prerequisite validation (plan requires spec, build requires spec + plan).

### Acceptance Criteria

- [x] AC-01: `lopen spec` starts requirement-gathering phase
- [x] AC-04: `lopen plan` validates spec exists before proceeding
- [x] AC-06: `lopen build` validates spec + plan exist before proceeding
- [x] Error messages guide user to run prerequisite commands

### Tasks

- [x] **1. Create `PhaseCommands.cs`** — Factory methods for spec/plan/build with prerequisite validation
- [x] **2. Wire in `Program.cs`** — Add all three commands to root
- [x] **3. Create test fakes** — FakeModuleScanner + FakePlanManager
- [x] **4. Write tests** — 9 PhaseCommandTests covering success and all failure paths
- [x] **5. Validate** — 1133 tests pass, formatting clean

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-078 | cli | Phase subcommands (spec/plan/build) with prerequisite validation |
| JOB-081 | cli | Config show + revert subcommands |
| JOB-080 | cli | Session CLI subcommands (list/show/resume/delete/prune) |
| JOB-079 | cli | Auth CLI subcommands (login/status/logout) |
| JOB-075 | core | Core AC tests (all 24 ACs) |