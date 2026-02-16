# Implementation Plan

## Current Job: JOB-037 — Session Resume with --resume/--no-resume

**Module**: storage/cli  
**Priority**: P2  
**Status**: ✅ Complete  
**Description**: Implement session resume from latest with --resume {id} and --no-resume flags.

### Tasks

- [x] **1. Add global options** — `--resume` and `--no-resume` in GlobalOptions
- [x] **2. Create `ResolveSessionAsync`** — Validates format, existence, not-complete; auto-resume from latest
- [x] **3. Wire into phase commands** — spec/plan/build use ResolveSessionAsync
- [x] **4. Write tests** — 7 resume tests covering all scenarios
- [x] **5. Validate** — 1165 tests pass

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-037 | storage/cli | Session resume (--resume/--no-resume) |
| JOB-083 | cli | CLI integration tests (12 tests, 20/25 ACs) |
| JOB-082 | cli | --prompt flag, exit codes, --help/--version |
| JOB-078 | cli | Phase subcommands (spec/plan/build) |
| JOB-081 | cli | Config show + revert subcommands |
| JOB-080 | cli | Session CLI subcommands |