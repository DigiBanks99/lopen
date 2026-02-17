# Implementation Plan — Current Batch

## Completed This Session (21 jobs)

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
| JOB-010 | LLM-11 | RetryingLlmService decorator + 19 tests |
| JOB-011 | LLM-11 | Tests covered by RetryingLlmServiceTests |
| JOB-013 | LLM-13 | Token metrics restore on resume + 4 tests |
| JOB-014 | LLM-13 | Round-trip persistence tests |
| JOB-015 | CLI-27 | --no-welcome flag + 3 tests |
| JOB-016 | CLI-27 | Tests covered by RootCommandTests |
| JOB-022 | STOR-14 | Corrupted session detection tests (5) |
| JOB-023 | STOR-16 | Disk-full / write-failure tests (2) |
| JOB-024 | CLI-22 | ProgramTests expansion (19 integration tests) |
| JOB-025 | TUI-06 | Landing page skip wiring (covered by CLI-27) |
| JOB-027 | TUI-51 | Live data provider wiring tests (5) |

## Remaining P3-P4 Jobs

### P3: Integration tests (require more infrastructure)
- [ ] JOB-028 (CORE-02): End-to-end workflow integration test
- [ ] JOB-029 (CLI-01): TUI mode launch integration test
- [ ] JOB-030: Headless mode integration test

### P4: Additional test coverage
- [ ] JOB-031: Session resume flow test
- [ ] JOB-038-041: Drift detection, git auto-commit, branch-per-module, revert
- [ ] JOB-044+: Exit codes, guardrails, budget enforcement