# Platform - Specification

> Non-functional requirements: performance, cross-platform, accessibility

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| NFR-001 | Performance | High | 🔴 Not Started |
| NFR-002 | Cross-Platform | High | 🟢 Complete |
| NFR-003 | Accessibility | Medium | 🔴 Not Started |

---

## NFR-001: Performance

### Description
Ensure responsive CLI performance.

### Acceptance Criteria
- [ ] CLI startup time < 500ms
- [ ] Command parsing < 50ms
- [ ] First response from Copilot SDK < 2s (network dependent)
- [ ] Streaming responses render immediately

### Metrics
| Metric | Target | Measurement |
|--------|--------|-------------|
| Cold start | < 500ms | Time from invocation to ready |
| Warm start | < 100ms | Subsequent commands in REPL |
| Memory usage | < 100MB | Baseline memory footprint |

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
- [ ] Clear, readable output
- [ ] Proper exit codes (0 = success, non-zero = failure)
- [ ] Screen reader friendly output
- [ ] Respect `NO_COLOR` environment variable
- [ ] Support for high contrast terminals

### Exit Codes
| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | General error |
| 2 | Invalid arguments |
| 3 | Authentication error |
| 4 | Network error |
| 5 | Copilot SDK error |

---

## Implementation Notes

### Logging
- Levels: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`
- Configure via `--log-level` or `LOPEN_LOG_LEVEL`
- Default: `Info`
