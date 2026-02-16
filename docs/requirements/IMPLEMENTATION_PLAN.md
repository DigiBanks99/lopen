# Implementation Plan

## Status: P2 OTel + Wiring Complete, Moving to P2 Feature Jobs

All P1 jobs and P2 OTel instrumentation + service wiring jobs are complete.

### Completed Jobs (P1 + P2 Wiring/OTel)

| Job | Description | Status |
|-----|-------------|--------|
| JOB-001–008 | Core DI, orchestration, tools, CLI, headless, TUI shell | ✅ Done |
| JOB-009–010 | Layout/Keyboard handler wiring | ✅ Done |
| JOB-013–020 | Drift, transitions, gates, git, autosave, session resume | ✅ Done |
| JOB-021 | Token metrics wiring | ✅ Done |
| JOB-022–027 | OTel span instrumentation (6 jobs) | ✅ Done |
| JOB-028 | Backpressure spans + counter | ✅ Done |
| JOB-029 | Counter metrics (10 counters) | ✅ Done |
| JOB-030 | Histogram/gauge metrics | ✅ Done |
| JOB-035 | Test TUI command | ✅ Done |
| JOB-077 | Wire root to real TUI | ✅ Done |

### Next P2 Jobs

| Job | Description | Module |
|-----|-------------|--------|
| JOB-011 | Connect TopPanelComponent to live data sources | tui |
| JOB-012 | Connect ContextPanelComponent to live task tree | tui |
| JOB-031 | --prompt flag injection into LLM context (headless) | cli |
| JOB-032 | --prompt flag populating TUI input field | cli/tui |
| JOB-033 | Headless error when no --prompt and no active session | cli |
| JOB-034 | Consistent exit codes (0/1/2) | cli |
| JOB-036–041 | TUI wiring (slash commands, Ctrl+P, landing page, session modal, activity, prompt) | tui |