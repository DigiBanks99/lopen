# Implementation Plan

## Status: P2 CLI Features Complete, TUI Wiring Next

All P1 jobs, P2 OTel, P2 service wiring, and P2 CLI feature jobs are complete.

### Completed Jobs

| Job | Description | Status |
|-----|-------------|--------|
| JOB-001–010 | Core DI, orchestration, tools, CLI, headless, TUI shell, layout/keyboard | ✅ Done |
| JOB-013–021 | Drift, transitions, gates, git, autosave, session resume, token metrics | ✅ Done |
| JOB-022–030 | OTel spans, counters, histograms, gauges, backpressure | ✅ Done |
| JOB-031–034 | CLI --prompt injection, TUI prompt, headless validation, exit codes | ✅ Done |
| JOB-035 | Test TUI command | ✅ Done |
| JOB-077 | Wire root to real TUI | ✅ Done |

### Next P2 Jobs

| Job | Description | Module |
|-----|-------------|--------|
| JOB-011 | Connect TopPanelComponent to live data sources | tui |
| JOB-012 | Connect ContextPanelComponent to live task tree | tui |
| JOB-036 | Wire SlashCommandRegistry to CLI commands | tui |
| JOB-037 | Implement Ctrl+P pause | tui |
| JOB-038 | Wire LandingPageComponent | tui |
| JOB-039 | Wire SessionResumeModal | tui |
| JOB-040 | Wire ActivityPanelComponent to stream events | tui |
| JOB-041 | Wire PromptAreaComponent to submit to orchestrator | tui |