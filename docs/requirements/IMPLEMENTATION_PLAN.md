# Implementation Plan

> Status: **Phase 4 - Quality & Enhancements**
> Last updated: 2026-01-25
> Tests: 200 passing

## Summary

Phase 1-3 complete. JTBD-018 (Shouldly migration) complete. Now focusing on TUI enhancements.

---

## ✅ Completed: JTBD-018 - Migrate to Shouldly

**Requirement**: REQ-TD-001

Migrated 200 tests from FluentAssertions 8.8.0 to Shouldly 4.3.0. All tests pass.

---

## Next Priority: JTBD-019 - TUI Spinners

**Requirement**: REQ-014

### What
Add spinners for async operations using Spectre.Console.

### Why
- Better UX during Copilot API calls
- Visual feedback for long-running operations

### Research
See `docs/research/SPECTRE_SUMMARY.md`

---

## Backlog (Priority Order)

| JTBD | Description | Requirement | Notes |
|------|-------------|-------------|-------|
| JTBD-020 | TUI Tables for sessions | REQ-014 | Same research |
| JTBD-021 | Quoted string parsing | REQ-010 | TODO in ReplService |
| JTBD-022 | OAuth Device Flow | REQ-003 | See auth/DEVICE_FLOW.md |
| JTBD-023 | Secure token storage | REQ-003 | Platform-specific |
| JTBD-024 | Git tools for Copilot | REQ-023 | Extend LopenTools |
| JTBD-025 | Shell tool for Copilot | REQ-023 | Needs permission model |

---

## Completed Phases

| Phase | JTBDs | Status |
|-------|-------|--------|
| Phase 1 - Foundation | JTBD-001 to JTBD-006 | ✅ Complete |
| Phase 2 - REPL | JTBD-007 to JTBD-010 | ✅ Complete |
| Platform NFRs | JTBD-011, JTBD-012 | ✅ Complete |
| Phase 3 - Copilot | JTBD-013 to JTBD-017 | ✅ Complete |
| Tech Debt | JTBD-018 | ✅ Complete |

---

## References

- [tech-debt/SPECIFICATION.md](tech-debt/SPECIFICATION.md) - Tech debt requirements
- [copilot/RESEARCH.md](copilot/RESEARCH.md) - SDK patterns
- [auth/DEVICE_FLOW.md](auth/DEVICE_FLOW.md) - OAuth implementation
