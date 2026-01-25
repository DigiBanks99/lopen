# Lopen - Requirements Index

> A .NET 10 CLI application with REPL capabilities for GitHub Copilot SDK integration

## Project Information

| Attribute | Value |
|-----------|-------|
| Name | lopen |
| Platform | .NET 10 |
| Architecture | Single executable (initially) |

---

## Modules

| Module | Description | Document |
|--------|-------------|----------|
| [cli-core](cli-core/SPECIFICATION.md) | Version, help commands | Phase 1 |
| [auth](auth/SPECIFICATION.md) | OAuth2, device flow, token management | Phase 1 |
| [repl](repl/SPECIFICATION.md) | Interactive REPL, session state, history | Phase 2 |
| [tui](tui/SPECIFICATION.md) | Terminal UI patterns (Spectre.Console) | Phase 2 |
| [platform](platform/SPECIFICATION.md) | Performance, cross-platform, accessibility | Cross-cutting |
| [copilot](copilot/SPECIFICATION.md) | Copilot SDK, chat, streaming, tools | Phase 3 |
| [tech-debt](tech-debt/SPECIFICATION.md) | Items related to cleaning up the code | Cross-cutting |

---

## Requirements Summary

### Phase 1 - Foundation (Current)

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| REQ-001 | Version Command | [cli-core](cli-core/SPECIFICATION.md) | 🟢 Complete |
| REQ-002 | Help/Commands List | [cli-core](cli-core/SPECIFICATION.md) | 🟢 Complete |
| REQ-003 | Copilot SDK Authentication | [auth](auth/SPECIFICATION.md) | 🟢 Complete |

### Phase 2 - REPL & TUI (Complete)

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| REQ-010 | REPL Mode | [repl](repl/SPECIFICATION.md) | 🟢 Complete |
| REQ-011 | Session State Management | [repl](repl/SPECIFICATION.md) | 🟢 Complete |
| REQ-012 | Command History | [repl](repl/SPECIFICATION.md) | 🟢 Complete |
| REQ-013 | Auto-completion | [repl](repl/SPECIFICATION.md) | 🟢 Complete |
| REQ-014 | Modern TUI Patterns | [tui](tui/SPECIFICATION.md) | 🟢 Complete |

### Phase 3 - Copilot Integration (Planned)

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| REQ-020 | Copilot SDK Integration | [copilot](copilot/SPECIFICATION.md) | 🔴 Not Started |
| REQ-021 | Chat Command | [copilot](copilot/SPECIFICATION.md) | 🔴 Not Started |
| REQ-022 | Streaming Responses | [copilot](copilot/SPECIFICATION.md) | 🔴 Not Started |
| REQ-023 | Custom Tools | [copilot](copilot/SPECIFICATION.md) | 🔴 Not Started |
| REQ-024 | Session Persistence | [copilot](copilot/SPECIFICATION.md) | 🔴 Not Started |

### Non-Functional Requirements

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| NFR-001 | Performance | [platform](platform/SPECIFICATION.md) | 🟢 Complete |
| NFR-002 | Cross-Platform | [platform](platform/SPECIFICATION.md) | 🟢 Complete |
| NFR-003 | Accessibility | [platform](platform/SPECIFICATION.md) | 🟢 Complete |

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| CLI Framework | System.CommandLine (2.0.2) |
| TUI | Spectre.Console (0.54.0) |
| Auth | GitHub OAuth2 (device flow) |
| Testing | xUnit + FluentAssertions + Verify |
| Logging | Serilog |

> **Note**: The official GitHub Copilot SDK for .NET (`GitHub.Copilot.SDK` v0.1.17) is now available on NuGet. Phase 3 will integrate this SDK for AI-powered chat and agentic workflows.

---

## Status Legend

| Icon | Meaning |
|------|---------|
| 🔴 | Not Started |
| 🟡 | In Progress |
| 🟢 | Complete |
| ⏸️ | Blocked |

---

## Change Log

| Date | Change |
|------|--------|
| 2026-01-24 | Phase 2 complete, added Phase 3 Copilot Integration requirements |
| 2026-01-23 | Initial requirements, split into modules |
