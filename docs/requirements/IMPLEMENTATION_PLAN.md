# Implementation Plan

## Status: P2 Jobs In Progress

All P1 jobs are complete. Currently working through P2 wiring jobs.

### Completed Jobs (P1 + P2)

| Job | Description | Status |
|-----|-------------|--------|
| JOB-001 | Register DI services | ✅ Done |
| JOB-002 | Orchestration loop | ✅ Done |
| JOB-003 | Tool handlers | ✅ Done |
| JOB-004–006 | Wire CLI commands | ✅ Done |
| JOB-007 | Headless runner | ✅ Done |
| JOB-008 | TUI application shell | ✅ Done |
| JOB-009 | Wire LayoutCalculator | ✅ Done |
| JOB-010 | Wire KeyboardHandler | ✅ Done |
| JOB-013 | Wire DriftDetector | ✅ Done |
| JOB-014/015 | Auto-transitions + human gate | ✅ Done |
| JOB-016 | Wire GitWorkflowService.CommitTaskCompletion | ✅ Done |
| JOB-017 | Wire GitWorkflowService.EnsureModuleBranch | ✅ Done |
| JOB-018 | Guardrails already wired | ✅ Done |
| JOB-019 | Wire AutoSaveService | ✅ Done |
| JOB-020 | Wire session resume | ✅ Done |
| JOB-035 | Test TUI command | ✅ Done |
| JOB-077 | Wire root to real TUI | ✅ Done |

### Next P2 Jobs

| Job | Description | Module |
|-----|-------------|--------|
| JOB-021 | Wire token metrics from CopilotLlmService to TUI/session stats | core/llm |
| JOB-022 | Instrument WorkflowOrchestrator with OTel spans | otel |
| JOB-023 | Instrument CopilotLlmService with OTel spans | otel |
| JOB-024 | Instrument GitWorkflowService with OTel spans | otel |
| JOB-025 | Instrument SessionManager with OTel spans | otel |
| JOB-026 | Instrument GuardrailPipeline with OTel spans | otel |
| JOB-027 | Instrument DriftDetector with OTel spans | otel |