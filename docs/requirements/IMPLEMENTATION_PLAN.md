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

### JOB-075 (OTEL-13): Aspire Dashboard Integration ✅
- [x] AppHost project already exists with Lopen reference
- [x] OTEL reads OTEL_EXPORTER_OTLP_ENDPOINT auto-injected by Aspire
- [x] Tests verify AppHost structure and OTLP activation

### JOB-052 (TUI-22): Guided Conversation UI ✅
- [x] GuidedConversationData model (turns, phases, draft spec)
- [x] GuidedConversationComponent renders all phases with Q&A prefixes
- [x] ActivityEntryKind.Conversation with 💬 prefix
- [x] Registered in ComponentGallery with 4 preview states
- [x] 31 new tests + 1 gallery name + 1 kind prefix

## All Jobs Complete 🎉
All jobs in jobs-to-be-done.json are marked as done.