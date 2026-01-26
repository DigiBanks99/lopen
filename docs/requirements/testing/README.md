# Self-Testing Module Documentation

This directory contains the specification and research for the Lopen Self-Testing module (REQ-020).

## Files

### 📋 SPECIFICATION.md
**Status:** ✅ Complete  
**Purpose:** Formal requirements specification for the `lopen test self` command

Contains:
- Requirements and acceptance criteria
- Command signatures and flags
- Test suites definition (11 tests across 4 suites)
- Expected output formats (terminal UI and JSON)
- Test cases and scenarios

### 📚 RESEARCH.md
**Status:** ✅ Complete  
**Size:** 1,945 lines, 62KB  
**Purpose:** Comprehensive technical research for implementation

Contains:
- **Architecture Recommendations** - Command + Strategy + Builder patterns
- **Test Case Definition** - Embedded C# (Phase 1) → External YAML (Phase 2)
- **Validation Strategies** - Keyword matching, regex, fuzzy matching
- **Parallel Execution** - TPL with Parallel.ForEachAsync
- **Spectre.Console Patterns** - Progress bars, tables, panels, interactive prompts
- **Test Isolation** - IAsyncDisposable, session management, cleanup
- **Error Handling** - Polly resilience, timeout handling, error categories
- **Process Management** - CliWrap 3.6.0+ for command execution
- **Testing Strategy** - Unit tests with mocks, integration tests
- **Configuration** - External YAML, priority order, extensibility

## Quick Reference

### Technology Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Process Execution | **CliWrap 3.6.0+** | Fluent API, robust, async-first |
| Resilience | **Polly 8.0+** | Retry/timeout policies |
| Validation | **Built-in** | Simple string operations |
| UI | **Spectre.Console 0.54.0** | Rich terminal UI (existing) |
| Testing | **xUnit.net 2.6+** | Modern, async-first |
| Assertions | **FluentAssertions 6.12+** | Readable assertions |
| Logging | **Serilog 3.1+** | Structured logging |

### Key Design Decisions

1. **Command Pattern** for test cases → Flexible, testable
2. **CliWrap** over System.Diagnostics.Process → Better API
3. **Parallel.ForEachAsync** over Task.WhenAll → Better control
4. **Embedded tests first**, YAML later → Faster implementation
5. **Keyword matching** over Regex by default → Simpler
6. **Spectre.Console** for all UI → Rich, professional output

### Implementation Roadmap

- **Week 1:** Core Infrastructure (interfaces, TestRunner, validators)
- **Week 2:** Test Definitions (embedded tests, CLI command, filtering)
- **Week 3:** Advanced Features (timeout, Polly, JSON output, interactive)
- **Week 4:** Extensibility (YAML loader, config files, discovery)
- **Week 5:** Testing & Polish (unit tests, integration tests, CI/CD)

## Supporting Documents

Additional research materials are available in `docs/research/`:

- **TESTING_MODULE_RESEARCH.md** - Full technical details (same as RESEARCH.md)
- **SUMMARY.md** - Executive summary with technology choices
- **IMPLEMENTATION_CHECKLIST.md** - Phase-by-phase actionable tasks
- **README.md** - Documentation navigation guide

## Command Overview

```bash
# Run all tests
lopen test self

# Run with verbose output
lopen test self --verbose

# Filter tests by pattern
lopen test self --filter chat

# Interactive mode
lopen test self --interactive

# Custom model and timeout
lopen test self --model gpt-5 --timeout 60

# JSON output for CI/CD
lopen test self --format json
```

## Test Suites

1. **Chat Command** (3 tests)
   - T-CHAT-01: Basic question
   - T-CHAT-02: Code generation
   - T-CHAT-03: Error handling

2. **REPL Command** (3 tests)
   - T-REPL-01: Single prompt
   - T-REPL-02: Multi-turn conversation
   - T-REPL-03: History navigation

3. **Session Management** (2 tests)
   - T-SESSION-01: List sessions
   - T-SESSION-02: Delete session

4. **Authentication** (1 test)
   - T-AUTH-01: Check status

## Next Steps

1. ✅ **Research Complete** - All 10 research questions answered
2. 📋 **Review Research** - Review RESEARCH.md document
3. 🏗️ **Begin Implementation** - Start with Phase 1 (Week 1)
4. 🧪 **Write Tests** - Unit tests alongside implementation
5. 📝 **Update Spec** - Update SPECIFICATION.md as implementation progresses

---

**Last Updated:** January 25, 2025  
**Status:** Ready for Implementation
