# Implementation Plan — Current Batch

## Completed This Session

### JOB-063 (TUI-40): Queued User Messages in Next SDK Invocation ✅
- [x] IUserPromptQueue interface moved from Tui to Core
- [x] WorkflowOrchestrator drains queue before each LLM invocation
- [x] DI registration updated in Core ServiceCollectionExtensions
- [x] 6 new tests (drain, concatenate, empty queue, drain count, null queue, both prompt+queue)

### JOB-076 (OTEL-17): Instrumentation Overhead < 5ms ✅
- [x] AddLopenOtel wired into CLI Program.cs
- [x] Overhead benchmark test (50 iterations, < 5ms assertion)

## Remaining Open Jobs

### JOB-052 (TUI-22): Guided Conversation UI for Requirement Gathering
- Priority: P3
- Status: Not started — complex, requires spec study

### JOB-075 (OTEL-13): Aspire Dashboard Integration
- Priority: P3
- Status: Not started — requires aspire tooling