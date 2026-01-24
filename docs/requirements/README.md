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

---

## Requirements Summary

### Phase 1 - Foundation (Current)

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| REQ-001 | Version Command | [cli-core](cli-core/SPECIFICATION.md) | 🔴 Not Started |
| REQ-002 | Help/Commands List | [cli-core](cli-core/SPECIFICATION.md) | 🔴 Not Started |
| REQ-003 | Copilot SDK Authentication | [auth](auth/SPECIFICATION.md) | 🔴 Not Started |

### Phase 2 - REPL & TUI (Planned)

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| REQ-010 | REPL Mode | [repl](repl/SPECIFICATION.md) | 🔴 Not Started |
| REQ-011 | Session State Management | [repl](repl/SPECIFICATION.md) | 🔴 Not Started |
| REQ-012 | Command History | [repl](repl/SPECIFICATION.md) | 🔴 Not Started |
| REQ-013 | Auto-completion | [repl](repl/SPECIFICATION.md) | 🔴 Not Started |
| REQ-014 | Modern TUI Patterns | [tui](tui/SPECIFICATION.md) | 🔴 Not Started |

### Non-Functional Requirements

| ID | Requirement | Module | Status |
|----|-------------|--------|--------|
| NFR-001 | Performance | [platform](platform/SPECIFICATION.md) | 🔴 Not Started |
| NFR-002 | Cross-Platform | [platform](platform/SPECIFICATION.md) | 🔴 Not Started |
| NFR-003 | Accessibility | [platform](platform/SPECIFICATION.md) | 🔴 Not Started |

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| CLI Framework | System.CommandLine |
| TUI | Spectre.Console |
| Copilot SDK | GitHub.Copilot.SDK |
| Testing | xUnit + FluentAssertions + Verify |
| Logging | Serilog |

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
| 2026-01-23 | Initial requirements, split into modules |
