# Implementation Plan

> Priority: **JTBD-005** - GitHub OAuth2 Authentication (REQ-003)
> Last updated: 2026-01-24

## Completed

### JTBD-001 - Initialize .NET 10 Solution ✅
### JTBD-002 - Version Command (REQ-001) ✅
### JTBD-003 - Help Command (REQ-002) ✅
### JTBD-004 - Cross-Platform Build (NFR-002) ✅
- Single-file, self-contained publish configuration
- Linux-x64 tested (~13MB executable)
- All platform RIDs configured

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

## Next: JTBD-005 (REQ-003) - GitHub OAuth2 Authentication

### Steps

1. Research GitHub OAuth2 device flow
2. Create AuthService in Lopen.Core
3. Add auth commands (login, logout, status)
4. Implement token storage
5. Add tests

### Acceptance Criteria (from REQ-003)

- [ ] Device flow authentication to GitHub
- [ ] Secure token storage
- [ ] `lopen auth login` initiates flow
- [ ] `lopen auth status` shows current auth state
- [ ] `lopen auth logout` clears credentials

## Later

→ JTBD-006 (REQ-014): Modern TUI Patterns
→ JTBD-007 (REQ-010): REPL Mode
