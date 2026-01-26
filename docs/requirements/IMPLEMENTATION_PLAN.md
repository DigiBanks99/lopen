# Implementation Plan

> Current Focus: JTBD-019 - Loop Command (REQ-030)

## Overview

Implementing the `lopen loop` command - an autonomous development workflow that plans and builds features iteratively with minimal human intervention.

## Workplan

### Phase 1: Core Loop Infrastructure ✅

- [x] Create `LoopConfig` model in Lopen.Core
- [x] Create `LoopConfigService` for loading/merging configs
- [x] Create `LoopStateManager` for file-based state (jobs, plan, done file)
- [x] Create `LoopOutputService` for streaming with phase indicators
- [x] Create `LoopService` for orchestrating plan/build phases

### Phase 2: Loop Command (REQ-030) ✅

- [x] Add `loop` command to Program.cs with `--auto` and `--config` options
- [x] Implement interactive setup prompt (specification/plan/build choices)
- [x] Add Ctrl+C handling for graceful exit
- [x] Integrate with existing Copilot SDK

### Phase 3: Configuration (REQ-031) ✅

- [x] Add `loop configure` subcommand
- [x] Load user config from `~/.lopen/loop-config.json`
- [x] Load project config from `.lopen/loop-config.json`
- [x] Merge configs with project overriding user

### Phase 4: Plan/Build Phases (REQ-032, REQ-033) ✅

- [x] Implement plan phase (load PLAN.PROMPT.md, run Copilot)
- [x] Implement build phase loop (iterate until done file)
- [x] Iteration counter display
- [x] Loop completion detection (lopen.loop.done)

### Phase 5: Tests ✅

- [x] Unit tests for LoopConfig (6 tests)
- [x] Unit tests for LoopConfigService (9 tests)
- [x] Unit tests for LoopStateManager (12 tests)
- [x] Unit tests for LoopOutputService (8 tests)
- [x] Unit tests for LoopService (10 tests)

## Completed

JTBD-019 implementation is complete with 35 new tests (248 total tests passing).

## Next Steps

1. Update jobs-to-be-done.json to mark JTBD-019 as done
2. Commit changes
3. Move to next priority task (JTBD-020: Loop Configuration enhancements or JTBD-026: TUI Spinners)
