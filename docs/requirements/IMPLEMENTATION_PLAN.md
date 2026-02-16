# Implementation Plan

## Current Job: JOB-083 — CLI Integration Tests

**Module**: cli  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Write integration tests for all CLI acceptance criteria (commands, flags, exit codes, DI wiring).

### Tasks

- [x] **1. Create `CliIntegrationTests.cs`** — 12 tests covering DI wiring, command registration, root command, global flags
- [x] **2. Validate** — 1158 tests pass across all 8 test projects

### Coverage: 20/25 ACs covered across CLI test suite

| Status | ACs | Notes |
|--------|-----|-------|
| ✅ Covered | AC-3–15, AC-17, AC-19–21, AC-25 | Full test coverage |
| ⏳ Deferred | AC-1 (TUI), AC-2 (E2E headless), AC-18 (TUI input) | Requires TUI/workflow |
| ⏳ Deferred | AC-22, AC-23, AC-24 | CI pipeline checks |

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-083 | cli | CLI integration tests (12 tests, 20/25 ACs) |
| JOB-082 | cli | --prompt flag, exit codes, --help/--version |
| JOB-078 | cli | Phase subcommands (spec/plan/build) |
| JOB-081 | cli | Config show + revert subcommands |
| JOB-080 | cli | Session CLI subcommands |