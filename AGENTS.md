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
| Test Framework | xUnit, Shouldly                      |
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

1. Check `docs/requirements/README.md` before implementing features
2. All core business logic must have tests
3. Use `--long-flag` and `-s` short flags
4. See `docs/requirements/jobs-to-be-done.json` for prioritized tasks
5. Current focus in `docs/requirements/IMPLEMENTATION_PLAN.md`
6. Avoid adding additional documentation that consume context. Stick to SPECIFICATION.md and RESEARCH.md

## CLI Patterns

- System.CommandLine 2.0 provides `--help`, `-h`, `-?` and `--version` automatically
- Use `RootCommand.Parse(args).Invoke()` pattern
- Subcommands via `Command` class with `SetAction(parseResult => ...)` handlers
- Access option values with `parseResult.GetValue(option)`
- Async actions use `SetAction(async parseResult => { ... })`

## Key Learnings

- **Lopen Login**: You can find the session token in ~/.copilot/config.json under the copilot_tokens
- **System.CommandLine 2.0**: Now GA (not beta); API uses `SetAction()` with `ParseResult` parameter
- **Option API**: Use `new Option<T>("--name") { Description = "...", DefaultValueFactory = _ => "default" }` and `option.Aliases.Add("-n")` for aliases
- **Spectre.Console MultiSelectionPrompt**: Use `AddChoiceGroup()` for grouped selections, `.Select()` to pre-select items, `.Required()` to require at least one selection. Result is `List<T>` of selected items
- **Spectre.Console SelectionPrompt**: Use for single-item selection; reorder choices to set default (first item is highlighted)
- **Spectre.Console ConfirmationPrompt**: Use `DefaultValue = true` to set default, returns bool
- **Terminal Detection**: Use `Console.IsInputRedirected` to check if running in interactive terminal before showing prompts
