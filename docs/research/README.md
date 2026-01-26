# Lopen Testing Module Research

This directory contains comprehensive research for implementing the self-testing module for Lopen CLI.

## Documents

### [TESTING_MODULE_RESEARCH.md](./TESTING_MODULE_RESEARCH.md)
**1,945 lines** - Complete technical research document covering:
- Architecture patterns and design decisions
- Code examples and implementation patterns
- Test case definition formats (JSON/YAML)
- Validation strategies for AI responses
- Parallel execution with TPL
- Spectre.Console UI patterns
- Test isolation and cleanup
- Error handling and resilience
- Process management (CliWrap)
- Testing the test harness
- Configuration and extensibility
- Additional research findings
- CI/CD integration examples

### [SUMMARY.md](./SUMMARY.md)
**Quick reference guide** covering:
- Technology stack recommendations
- Architecture patterns
- Key design decisions
- Core interfaces
- Implementation phases
- Command usage examples
- Performance targets
- Dependencies list
- Success criteria
- Risk mitigation strategies

## Research Topics Covered

### 1. TestRunner Architecture ✅
- Command + Strategy + Builder patterns
- Core interfaces: `ITestCase`, `IProcessExecutor`, `IResponseValidator`
- Concrete implementations with full code examples
- TestRunner with parallel execution support

### 2. Test Case Definition ✅
- Embedded C# definitions (Phase 1)
- External YAML format (Phase 2)
- JSON schema for validation
- Test suite and test case structures

### 3. AI Response Validation ✅
- Keyword matching (case-insensitive substring)
- Regex patterns for complex matching
- Fuzzy matching for variance handling
- Composite validator with match modes (any/all/regex)
- Handling non-deterministic responses

### 4. Parallel Execution ✅
- `Parallel.ForEachAsync` with controlled parallelism
- ConcurrentBag for thread-safe aggregation
- Rate limiting (10 requests/second)
- Cancellation token support

### 5. Spectre.Console Patterns ✅
- Progress display with multiple columns
- Results table with status indicators
- Summary panel with statistics
- Interactive selection with MultiSelectionPrompt
- Custom progress columns
- Live display patterns

### 6. Test Isolation ✅
- IAsyncDisposable pattern
- Temporary directory per test
- Session cleanup
- Isolation context implementation

### 7. Error Handling ✅
- Polly resilience policies
- Error categorization
- Timeout handling
- API rate limit handling
- User-friendly error messages

### 8. Testing the Test Harness ✅
- Unit testing strategy with xUnit
- Mocking approach
- Integration testing
- CI/CD integration

### 9. Process Management ✅
- CliWrap library (recommended)
- System.Diagnostics.Process (fallback)
- Timeout and cancellation support
- Output capture (buffered/streaming)

### 10. Configuration ✅
- Configuration priority (CLI > env > file > default)
- Test definitions storage
- External test loader
- Plugin architecture

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 9.0 | Runtime |
| Spectre.Console | 0.54.0 | Terminal UI |
| System.CommandLine | 2.0.2 | CLI framework |
| CliWrap | 3.6.0+ | Process execution |
| Polly | 8.6.5+ | Resilience |
| Serilog | 3.1.1+ | Logging |
| xUnit | 2.6.5+ | Testing framework |
| YamlDotNet | 13.7.1+ | YAML parsing |

## Implementation Roadmap

```
Phase 1 (Week 1) - Core Infrastructure
├── ITestCase interface
├── TestRunner with parallel execution
├── CliWrapProcessExecutor
├── KeywordValidator
└── Spectre.Console progress

Phase 2 (Week 2) - Test Definitions
├── Embedded test suites
├── Test filtering
├── Interactive mode
└── Test case factory

Phase 3 (Week 3) - Advanced Features
├── Timeout support
├── Polly policies
├── Structured logging
└── JSON output

Phase 4 (Week 4) - Extensibility
├── YAML loader
├── JSON schema validation
├── Plugin support
└── Documentation

Phase 5 (Week 5) - Testing & Polish
├── Unit tests
├── Integration tests
├── CI/CD pipeline
└── Performance optimization
```

## Quick Start

```bash
# Read the executive summary first
cat SUMMARY.md

# Then dive into detailed research
cat TESTING_MODULE_RESEARCH.md

# Or jump to specific sections:
grep -n "## 1. TestRunner Architecture" TESTING_MODULE_RESEARCH.md
grep -n "## 5. Spectre.Console Patterns" TESTING_MODULE_RESEARCH.md
```

## Key Insights

### Architecture
✅ Command Pattern for flexible test cases  
✅ Strategy Pattern for pluggable validation  
✅ TPL for parallel execution  
✅ IAsyncDisposable for cleanup  

### Validation
✅ Keyword matching as primary strategy  
✅ Multiple patterns with "any" mode  
✅ Retry logic for AI variance  
✅ Fuzzy matching as optional enhancement  

### User Experience
✅ Rich terminal UI with Spectre.Console  
✅ Interactive mode for test selection  
✅ Real-time progress updates  
✅ Colored status indicators  

### Reliability
✅ Polly for transient fault handling  
✅ Comprehensive timeout support  
✅ Test isolation with cleanup  
✅ Rate limiting to avoid throttling  

## References

- [Microsoft .NET Testing](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [Task Parallel Library](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [CliWrap GitHub](https://github.com/Tyrrrz/CliWrap)
- [Polly Documentation](https://www.pollydocs.org/)

## Related Documents

- [Specification](../requirements/testing/SPECIFICATION.md) - REQ-020 requirements
- [Implementation Guide](../../IMPLEMENTATION_GUIDE.md) - When created

---

**Status:** Research Complete ✅  
**Next Step:** Begin Phase 1 Implementation  
**Last Updated:** 2026-01-25
