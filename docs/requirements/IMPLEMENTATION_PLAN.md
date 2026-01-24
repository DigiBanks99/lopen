# Implementation Plan

> Status: **Phase 3 - Copilot Integration**
> Next: JTBD-014 (REQ-021 Chat Command)
> Last updated: 2026-01-24

## Completed

| Phase | JTBDs | Status |
|-------|-------|--------|
| Phase 1 - Foundation | JTBD-001 to JTBD-006 | ✅ Complete |
| Phase 2 - REPL | JTBD-007 to JTBD-010 | ✅ Complete |
| Platform NFRs | JTBD-011, JTBD-012 | ✅ Complete |
| Copilot SDK | JTBD-013 | ✅ Complete |

**Tests: 172 passing**

---

## Completed: JTBD-013 - Copilot SDK Integration (REQ-020)

GitHub.Copilot.SDK v0.1.17 integrated with:
- `ICopilotService` / `ICopilotSession` interfaces
- `CopilotService` / `CopilotSession` SDK wrappers
- `MockCopilotService` / `MockCopilotSession` for testing
- 24 unit tests covering all service/session operations

Files created:
- `src/Lopen.Core/ICopilotService.cs`
- `src/Lopen.Core/ICopilotSession.cs`
- `src/Lopen.Core/CopilotService.cs`
- `src/Lopen.Core/CopilotSession.cs`
- `src/Lopen.Core/CopilotModels.cs`
- `src/Lopen.Core/MockCopilotService.cs`
- `src/Lopen.Core/MockCopilotSession.cs`
- `tests/Lopen.Core.Tests/CopilotServiceTests.cs`
- `tests/Lopen.Core.Tests/CopilotSessionTests.cs`

---

## Next: JTBD-014 - Chat Command (REQ-021)

### Overview
Add `lopen chat` command for AI-powered conversations.

### Command Signature
```bash
lopen chat                     # Start interactive chat
lopen chat "query"             # Single query mode
lopen chat --model gpt-4.1     # Specify model
```

### Implementation Steps
1. Create `ChatCommand` in Lopen.Cli
2. Add `--model` option with model selection
3. Support inline prompt argument for single-query mode
4. Integrate with `ICopilotService` for session management
5. Display streaming responses via `ConsoleOutput`

### Success Criteria
- [ ] `lopen chat "Hello"` returns AI response
- [ ] `lopen chat --model gpt-4.1` uses specified model
- [ ] Interactive mode with exit/quit commands
- [ ] Ctrl+C gracefully aborts in-flight requests
- [ ] Unit tests for command parsing

---

## Upcoming JTBDs

| ID | Requirement | Description |
|----|-------------|-------------|
| JTBD-014 | REQ-021 | Chat Command (`lopen chat`) |
| JTBD-015 | REQ-022 | Streaming Responses display |
| JTBD-016 | REQ-023 | Custom Tools (AIFunctionFactory) |
| JTBD-017 | REQ-024 | Session Persistence |

---

## References

- [copilot/RESEARCH.md](copilot/RESEARCH.md) - Full SDK API patterns
- [copilot/SPECIFICATION.md](copilot/SPECIFICATION.md) - Requirements
