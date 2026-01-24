# Implementation Plan

> Priority: **JTBD-002** - Version Command Enhancements (REQ-001)
> Last updated: 2026-01-24

## Completed: JTBD-001 - Initialize .NET 10 Solution ✅

- Created solution structure with Lopen.Cli, Lopen.Core, test projects
- Added System.CommandLine 2.0.2, Spectre.Console 0.54.0, FluentAssertions 8.8.0
- Implemented VersionService in Lopen.Core with text/JSON formatting
- 8 tests passing (5 Core, 3 CLI)
- `--version` and `--help` working via System.CommandLine built-in support

## Current Structure

```
lopen/
├── src/
│   ├── Lopen.Cli/        # CLI entry (System.CommandLine 2.0.2)
│   └── Lopen.Core/       # Business logic (VersionService)
├── tests/
│   ├── Lopen.Cli.Tests/  # CLI integration tests
│   └── Lopen.Core.Tests/ # Unit tests
├── Directory.Build.props # Shared .NET 10 settings
└── Lopen.sln
```

## Next: JTBD-002 (REQ-001) - Version Command Enhancements

### Steps

1. Add `-v` short alias for `--version`
2. Add `--format` option (text/json)
3. Update output format: `lopen version X.Y.Z`
4. Add tests for format options
5. Update CLI tests

### Acceptance Criteria (from REQ-001)

- [x] Displays semantic version (e.g., `0.1.0`)
- [ ] Supports both `--version` and `-v` flags
- [ ] Output format: `lopen version X.Y.Z`
- [ ] JSON format: `{"version": "X.Y.Z"}` with `--format json`
- [x] Exits with code 0 on success

## Dependencies (Verified)

| Package | Version | Status |
|---------|---------|--------|
| System.CommandLine | 2.0.2 | ✅ Installed |
| Spectre.Console | 0.54.0 | ✅ Installed |
| FluentAssertions | 8.8.0 | ✅ Installed |
| .NET SDK | 10.0.100 | ✅ Available |

## Later

→ JTBD-003 (REQ-002): Help subcommand
→ JTBD-004 (NFR-002): Cross-platform build configuration
