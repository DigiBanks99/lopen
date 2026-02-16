# Implementation Plan

## Current Job: JOB-084 — Split-Screen Layout

**Module**: tui  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Implement split-screen layout calculator with activity/context panes, adjustable 50/50 to 80/20 ratio.

### Tasks

- [x] **1. Create `ScreenRect`** — Value type for layout rectangles with Inflate
- [x] **2. Create `LayoutRegions`** — Record holding header/activity/context/prompt regions
- [x] **3. Create `LayoutCalculator`** — Calculates regions from screen dimensions and split percent
- [x] **4. Write tests** — 12 LayoutCalculatorTests + 4 ScreenRectTests
- [x] **5. Validate** — 1189 tests pass

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-084 | tui | Split-screen layout calculator |
| JOB-077 | core/cli | --headless mode with IOutputRenderer |
| JOB-037 | storage/cli | Session resume (--resume/--no-resume) |
| JOB-083 | cli | CLI integration tests |
| JOB-082 | cli | --prompt, exit codes, --help/--version |
| JOB-078 | cli | Phase subcommands (spec/plan/build) |