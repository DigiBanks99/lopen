# Implementation Plan

> Priority: **JTBD-004** - Cross-Platform Build Configuration (NFR-002)
> Last updated: 2026-01-24

## Completed

### JTBD-001 - Initialize .NET 10 Solution ✅
- Solution structure with Lopen.Cli, Lopen.Core, test projects
- System.CommandLine 2.0.2, Spectre.Console 0.54.0, FluentAssertions 8.8.0

### JTBD-002 - Version Command (REQ-001) ✅
- `lopen version` subcommand with `--format` / `-f` option
- Text format: `lopen version X.Y.Z`
- JSON format: `{"version":"X.Y.Z"}`

### JTBD-003 - Help Command (REQ-002) ✅
- `lopen help` subcommand with `--format` option
- `lopen help <command>` for specific command help
- JSON format support
- 22 tests passing (10 Core, 12 CLI)

## Current Structure

```
lopen/
├── src/
│   ├── Lopen.Cli/        # CLI entry, version/help commands
│   └── Lopen.Core/       # VersionService, HelpService
├── tests/
│   ├── Lopen.Cli.Tests/  # 12 CLI tests
│   └── Lopen.Core.Tests/ # 10 unit tests
├── Directory.Build.props
└── Lopen.sln
```

## Next: JTBD-004 (NFR-002) - Cross-Platform Build

### Steps

1. Configure multi-RID publishing in Directory.Build.props
2. Add publish profiles for Windows, macOS, Linux
3. Configure single-file, self-contained builds
4. Test builds on each platform

### Acceptance Criteria

- [ ] Single-file executable for each platform
- [ ] Self-contained (no .NET runtime required)
- [ ] Builds for win-x64, linux-x64, osx-x64, osx-arm64

## Later

→ JTBD-005 (REQ-003): GitHub OAuth2 Authentication
→ JTBD-006 (REQ-014): Modern TUI Patterns
