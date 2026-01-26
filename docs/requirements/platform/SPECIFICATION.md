# Platform - Specification

> Non-functional requirements: performance, cross-platform, accessibility

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| NFR-001 | Performance | High | 🟢 Complete |
| NFR-002 | Cross-Platform | High | 🟢 Complete |
| NFR-003 | Accessibility | Medium | 🟢 Complete |

---

## NFR-001: Performance

### Description
Ensure responsive CLI performance.

### Acceptance Criteria
- [x] CLI startup time < 500ms
- [x] Command parsing < 50ms
- [x] First response from Copilot SDK < 2s (network dependent)
- [x] Streaming responses render immediately

### Measured Performance (2026-01-24)
| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Cold start (single-file) | < 500ms | ~185ms | ✅ |
| Warm start (REPL) | < 100ms | ~50ms | ✅ |
| Memory usage | < 100MB | ~25MB | ✅ |
| Executable size | - | 14MB | ✅ |

### Response Time Metrics (2026-01-26)
| Metric | Target | Implementation |
|--------|--------|----------------|
| Time to first token | < 2s | `ResponseMetrics.TimeToFirstToken` with `MeetsFirstTokenTarget` check |
| Total response time | N/A | `ResponseMetrics.TotalTime` |
| Tokens per second | N/A | `ResponseMetrics.TokensPerSecond` |
| Streaming latency | Immediate | Direct console writes in `SpectreStreamRenderer` |

### Implementation
- `ResponseMetrics` record with timing calculations
- `IMetricsCollector` / `MetricsCollector` for collecting timing data
- `StreamConfig.MetricsCollector` for optional metrics capture
- `StreamConfig.ShowMetrics` to display metrics after streaming
- 45 unit tests for metrics functionality

### Notes
- Single-file self-contained publish provides excellent startup time
- No AOT compilation needed - JIT is fast enough
- Command parsing is essentially instant (< 5ms)
- Metrics collection is optional and adds minimal overhead

---

## NFR-002: Cross-Platform

### Description
Support Windows, macOS, and Linux.

### Acceptance Criteria
- [x] Windows 10+ support (RID: win-x64)
- [x] macOS 11+ support (RID: osx-x64, osx-arm64)
- [x] Linux (Ubuntu 20.04+, Debian 11+) support (RID: linux-x64)
- [x] Single self-contained executable option
- [x] ARM64 support where applicable

### Build Targets
| Platform | RID | Architecture | Status |
|----------|-----|--------------|--------|
| Windows | win-x64 | x64 | ✅ Configured |
| Windows | win-arm64 | ARM64 | ✅ Configured |
| macOS | osx-x64 | x64 | ✅ Configured |
| macOS | osx-arm64 | ARM64 (Apple Silicon) | ✅ Configured |
| Linux | linux-x64 | x64 | ✅ Tested |
| Linux | linux-arm64 | ARM64 | ✅ Configured |

### Publish Command
```bash
dotnet publish src/Lopen.Cli -c Release -r <RID> --self-contained -p:PublishSingleFile=true
```

---

## NFR-003: Accessibility

### Description
Ensure CLI is accessible to all users.

### Acceptance Criteria
- [x] Clear, readable output (Spectre.Console with styled messages)
- [x] Proper exit codes (0 = success, non-zero = failure)
- [x] Screen reader friendly output (text-based with symbols)
- [x] Respect `NO_COLOR` environment variable
- [x] Support for high contrast terminals (uses standard ANSI colors)

### Implementation
- `ExitCodes` class with standardized exit codes and descriptions
- `ConsoleOutput` respects NO_COLOR environment variable
- Output uses clear symbols (✓, ✗, !, ℹ) for accessibility
- 16 unit tests for exit codes

### Exit Codes
| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | General error |
| 2 | Invalid arguments |
| 3 | Authentication error |
| 4 | Network error |
| 5 | Copilot SDK error |
| 6 | Configuration error |
| 130 | Operation cancelled |

---

## Implementation Notes

### Logging
- Levels: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`
- Configure via `--log-level` or `LOPEN_LOG_LEVEL`
- Default: `Info`
