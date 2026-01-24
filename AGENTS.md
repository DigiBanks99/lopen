# Agents instructions

IMPORTANT:

- Update this document with learnings.
- Keep it brief and concise.

**Lopen** is a .NET 10 CLI application with REPL capabilities for GitHub Copilot SDK integration.

## Quick Reference

| Aspect         | Value                                |
| -------------- | ------------------------------------ |
| Language       | C# / .NET 10                         |
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
│   ├── Lopen.Repl/             # REPL implementation
│   └── Lopen.Sdk/              # Copilot SDK integration
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

# Publish single executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Important

1. **Requirements First**: Check `docs/requirements/README.md` before implementing features
1. **100% Core Coverage**: All core business logic must have tests
1. **POSIX Arguments**: Use `--long-flag` and `-s` short flags
1. **Jobs Tracking**: See `docs/requirements/jobs-to-be-done.json` for prioritized tasks
1. **Implementation Plan**: Current focus in `docs/requirements/IMPLEMENTATION_PLAN.md`

## Project Status

- **State**: Greenfield - no source code exists yet
- **Next Step**: Initialize .NET 10 solution structure (JTBD-001)
- **Research**: Implementation details in `docs/requirements/*/RESEARCH.md`

## Key Dependencies

| Package | Purpose |
|---------|---------|
| System.CommandLine | CLI parsing, subcommands, help/version |
| Spectre.Console | TUI output, colors, progress |
| FluentAssertions | Test assertions |
| coverlet.collector | Code coverage |

## CLI Patterns

- System.CommandLine provides `--help`, `-h`, `-?` and `--version` automatically
- Use `RootCommand.Parse(args).Invoke()` pattern
- Subcommands via `Command` class with `SetAction()` for handlers
