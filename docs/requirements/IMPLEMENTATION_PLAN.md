# Implementation Plan

## Current Job: JOB-082 — Implement --prompt flag, exit codes, --help/--version

**Module**: cli  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Add global `--prompt`/`-p` and `--headless`/`--quiet`/`-q` flags, define exit codes (0/1/2), verify `--help`/`--version`.

### Acceptance Criteria

- [x] AC-19: Headless mode without `--prompt` and without active session errors with guidance
- [x] AC-20: Exit codes: `0` success, `1` failure, `2` user intervention required
- [x] AC-21: `--help` and `--version` flags work as expected
- [ ] AC-17: `--prompt` injects into LLM context (deferred — workflow engine not wired)
- [ ] AC-18: `--prompt` populates TUI input field (deferred — TUI not implemented)

### Tasks

- [x] **1. Create `ExitCodes.cs`** — Constants for exit codes 0, 1, 2
- [x] **2. Create `GlobalOptions.cs`** — `--headless`/`-q`/`--quiet` and `--prompt`/`-p` as recursive options
- [x] **3. Wire in `Program.cs`** — `GlobalOptions.AddTo(rootCommand)`
- [x] **4. Update `PhaseCommands.cs`** — `ValidateHeadlessPromptAsync` for AC-19
- [x] **5. Write tests** — 6 GlobalOptionsTests + 6 headless/prompt PhaseCommandTests
- [x] **6. Validate** — 1146 tests pass, formatting clean

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-082 | cli | --prompt flag, exit codes, --help/--version |
| JOB-078 | cli | Phase subcommands (spec/plan/build) with prerequisite validation |
| JOB-081 | cli | Config show + revert subcommands |
| JOB-080 | cli | Session CLI subcommands (list/show/resume/delete/prune) |
| JOB-079 | cli | Auth CLI subcommands (login/status/logout) |