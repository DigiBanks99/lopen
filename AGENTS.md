# Agents instructions

IMPORTANT:

- Update this document with learnings.
- Keep it brief and concise.

**Lopen** is a .NET 10 CLI application with REPL capabilities for GitHub Copilot integration.

## Quick Reference

| Aspect         | Value                                |
| -------------- | ------------------------------------ |
| Language       | C# / .NET 10.0.100                   |
| CLI Pattern    | Subcommands (`lopen <cmd> <subcmd>`) |
| Argument Style | POSIX (`--flag`, `-f`)               |
| Test Framework | xUnit                                |
| Output Formats | Plain text, JSON                     |

---

## Project Structure (Target)

```tree
lopen/
├── src/
│   ├── Lopen.Cli/              # CLI entry point, command definitions
│   ├── Lopen.Core/             # Core business logic (100% test coverage)
│   └── Lopen.Repl/             # REPL implementation
├── tests/
│   ├── Lopen.Core.Tests/       # Unit tests for core logic
│   ├── Lopen.Cli.Tests/        # CLI command tests
│   └── Lopen.Integration.Tests/# Integration tests
├── docs/
│   └── requirements/           # Requirements documentation
├── AGENTS.md                   # This file
└── README.md                   # Project readme
```

## Build & Test Commands

```bash
# Build
dotnet build

# Test with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run CLI
dotnet run --project src/Lopen.Cli
```

## Important

1. **Requirements First**: Check `docs/requirements/README.md` before implementing features
1. **100% Core Coverage**: All core business logic must have tests
1. **POSIX Arguments**: Use `--long-flag` and `-s` short flags
1. **Jobs Tracking**: See `docs/requirements/jobs-to-be-done.json` for prioritized tasks
1. **Implementation Plan**: Current focus in `docs/requirements/IMPLEMENTATION_PLAN.md`

## Project Status

- **State**: Phase 1 foundation complete (JTBD-001 through JTBD-005)
- **Tests**: 38 tests passing (22 Core, 16 CLI)
- **Next**: JTBD-006 - Modern TUI Patterns

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| System.CommandLine | 2.0.2 (GA) | CLI parsing, subcommands, help/version |
| Spectre.Console | 0.54.0 | TUI output, colors, progress |
| FluentAssertions | 8.8.0 | Test assertions |
| coverlet.collector | latest | Code coverage |

## CLI Patterns

- System.CommandLine 2.0 provides `--help`, `-h`, `-?` and `--version` automatically
- Use `RootCommand.Parse(args).Invoke()` pattern
- Subcommands via `Command` class with `SetAction(parseResult => ...)` handlers
- Access option values with `parseResult.GetValue(option)`
- Async actions use `SetAction(async parseResult => { ... })`

## Key Learnings

- **No Copilot SDK**: No official `GitHub.Copilot.SDK` NuGet package exists; use GitHub OAuth2 device flow directly
- **System.CommandLine 2.0**: Now GA (not beta); API uses `SetAction()` with `ParseResult` parameter
- **.NET 10**: SDK 10.0.100 available and confirmed working in environment
- **Option API**: Use `new Option<T>("--name") { Description = "...", DefaultValueFactory = _ => "default" }` and `option.Aliases.Add("-n")` for aliases
- **Option Constructor**: First string param is name, subsequent strings are aliases (not description!). Set description via property.
- **File Permissions**: Use `File.SetUnixFileMode()` for credential files on Unix
- **Trimming**: Use `SuppressTrimAnalysisWarnings` for JSON serialization until source generators added
