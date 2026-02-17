# Implementation Plan — Current Batch

## Completed This Session

| Job | Spec | Summary |
|-----|------|---------|
| JOB-002 | CORE-25 | ToolConversion bridge + 11 tests |
| JOB-009 | CORE-25 | Tests covered by ToolConversionTests |
| JOB-004 | TUI-50 | TuiOutputRenderer + 16 tests |
| JOB-006 | TUI-50 | Tests covered by TuiOutputRendererTests |
| JOB-003 | TUI-52 | Orchestrator bridge + 11 tests |
| JOB-007 | TUI-52 | Tests covered by TuiOrchestratorBridgeTests |
| JOB-005 | TUI-51 | DI wiring for all data providers + 4 tests |
| JOB-008 | CLI-26 | Project root discovery tests (13 total) |

## Next Priority: P3 Jobs

### JOB-010 (LLM-11): Runtime model fallback
- [ ] Implement catch-retry logic in CopilotLlmService for model-unavailable errors
- [ ] Add fallback chain configuration
- [ ] Tests (JOB-011)

### JOB-013/014 (LLM-13): Token metrics persistence
- [ ] Verify InMemoryTokenTracker round-trip with AutoSaveService
- [ ] Add persistence tests

### JOB-024 (CLI-22): Expand ProgramTests.cs
- [ ] Integration tests for DI container resolution