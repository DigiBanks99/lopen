# Implementation Plan

> Priority: **JTBD-003** - Help Subcommand (REQ-002)
> Last updated: 2026-01-24

## Completed

### JTBD-001 - Initialize .NET 10 Solution ✅
- Solution structure with Lopen.Cli, Lopen.Core, test projects
- System.CommandLine 2.0.2, Spectre.Console 0.54.0, FluentAssertions 8.8.0

### JTBD-002 - Version Command (REQ-001) ✅
- `lopen version` subcommand with `--format` / `-f` option
- Text format: `lopen version X.Y.Z`
- JSON format: `{"version":"X.Y.Z"}`
- 13 tests passing (5 Core, 8 CLI)

## Current Structure

```
lopen/
├── src/
│   ├── Lopen.Cli/        # CLI entry, version command
│   └── Lopen.Core/       # VersionService
├── tests/
│   ├── Lopen.Cli.Tests/  # 8 CLI tests
│   └── Lopen.Core.Tests/ # 5 unit tests
├── Directory.Build.props
└── Lopen.sln
```

## Next: JTBD-003 (REQ-002) - Help Subcommand

### Steps

1. Add `help` subcommand
2. Support `lopen help <command>` for detailed command help
3. Add JSON output format via `--format json`
4. Add tests for help command variants

### Acceptance Criteria (from REQ-002)

- [ ] `lopen help` lists all available commands
- [ ] `lopen help <command>` shows command details
- [ ] `--format json` outputs JSON structure
- [ ] Built-in `--help` / `-h` continue to work

## Later

→ JTBD-004 (NFR-002): Cross-platform build configuration
→ JTBD-005 (REQ-003): GitHub OAuth2 Authentication
