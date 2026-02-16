# Implementation Plan

## Current Job: JOB-077 — Implement --headless mode with IOutputRenderer

**Module**: core/cli  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: IOutputRenderer abstraction with HeadlessRenderer for plain text output in headless mode.

### Tasks

- [x] **1. Create `IOutputRenderer`** — Progress, error, result, prompt methods
- [x] **2. Create `HeadlessRenderer`** — Plain text to stdout/stderr, PromptAsync returns null
- [x] **3. Register in DI** — Default HeadlessRenderer via TryAddSingleton
- [x] **4. Write tests** — 7 HeadlessRendererTests + 1 DI registration test
- [x] **5. Validate** — 1173 tests pass

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-077 | core/cli | --headless mode with IOutputRenderer |
| JOB-037 | storage/cli | Session resume (--resume/--no-resume) |
| JOB-083 | cli | CLI integration tests |
| JOB-082 | cli | --prompt flag, exit codes, --help/--version |
| JOB-078 | cli | Phase subcommands (spec/plan/build) |
| JOB-081 | cli | Config show + revert subcommands |
| JOB-080 | cli | Session CLI subcommands |