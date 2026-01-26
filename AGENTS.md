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

- **State**: Phase 4 - Quality & Enhancements
- **Tests**: 248 tests passing
- **Features**: CLI, REPL, Copilot Integration, Loop Command
- **Next**: TUI Spinners (JTBD-026) or Verification Agent (JTBD-025)

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| System.CommandLine | 2.0.2 (GA) | CLI parsing, subcommands, help/version |
| Spectre.Console | 0.54.0 | TUI output, colors, progress |
| GitHub.Copilot.SDK | 0.1.17 | Copilot CLI integration |
| Microsoft.Extensions.AI | 10.1.1 | AIFunctionFactory for tools |
| Shouldly | 4.3.0 | Test assertions (MIT license) |
| coverlet.collector | latest | Code coverage |

## CLI Patterns

- System.CommandLine 2.0 provides `--help`, `-h`, `-?` and `--version` automatically
- Use `RootCommand.Parse(args).Invoke()` pattern
- Subcommands via `Command` class with `SetAction(parseResult => ...)` handlers
- Access option values with `parseResult.GetValue(option)`
- Async actions use `SetAction(async parseResult => { ... })`

## Key Learnings

- **Copilot SDK Available**: `GitHub.Copilot.SDK` v0.1.17 on NuGet; wraps Copilot CLI via JSON-RPC, auth via `gh auth`
- **Copilot CLI Required**: SDK spawns `copilot` CLI process; must be in PATH. Installed: v0.0.394
- **SDK Patterns**: `CopilotClient` → `CopilotSession` → events (`AssistantMessageDeltaEvent` for streaming)
- **SDK Error Handling**: Uses standard .NET exceptions (FileNotFoundException, InvalidOperationException, TimeoutException)
- **SDK API Details**: `ListModelsAsync()` returns `List<ModelInfo>`, `AssistantMessageData.Content` (not Message), param is `cancellationToken`
- **Streaming**: Subscribe with `session.On(evt => ...)`, yield deltas via `Channel<string>` for `IAsyncEnumerable`
- **System.CommandLine 2.0**: Now GA (not beta); API uses `SetAction()` with `ParseResult` parameter
- **.NET 10**: SDK 10.0.100 available and confirmed working in environment
- **Option API**: Use `new Option<T>("--name") { Description = "...", DefaultValueFactory = _ => "default" }` and `option.Aliases.Add("-n")` for aliases
- **Option Constructor**: First string param is name, subsequent strings are aliases (not description!). Set description via property.
- **File Permissions**: Use `File.SetUnixFileMode()` for credential files on Unix
- **Trimming**: Use `SuppressTrimAnalysisWarnings` for JSON serialization until source generators added
- **REPL Testing**: Use interface abstraction (IConsoleInput) for Console.ReadLine() to enable unit testing; Spectre.Console.Testing.TestConsole for output mocking
- **REPL Command Execution**: System.CommandLine `RootCommand.Parse(args).InvokeAsync()` can be called in a loop for REPL command execution
- **Console Line Editing**: Custom `Console.ReadKey(intercept: true)` loop allows full line editing (arrows, Home/End, Delete) and history navigation without external libraries
- **AIFunctionFactory**: Use `AIFunctionFactory.Create(func, name, description)` from Microsoft.Extensions.AI; result `.Name` property (not `.Metadata.Name`)
- **AIFunction InvokeAsync**: Returns JsonElement for non-string types; use `new AIFunctionArguments { ["param"] = value }` for arguments
- **Session Management**: SDK handles persistence via `ResumeSessionAsync`, `ListSessionsAsync`, `DeleteSessionAsync`
- **Spectre.Console NO_COLOR**: Automatically respects `NO_COLOR` env var; no special code needed
- **Spectre.Console Tables**: Use `new Table().RoundedBorder()` for session lists; `.AsciiBorder()` for accessibility
- **Spectre.Console Spinners**: Use `AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync()` for async ops
- **Shouldly vs FluentAssertions**: Shouldly has MIT license, simpler API, better error messages showing variable names
- **Shouldly Patterns**: `.ShouldBe()`, `.ShouldContain()`, `Should.Throw<T>(action)` for exceptions, `.ShouldNotBeNull()` only for reference types
- **Shouldly Collections**: Use `.ShouldBe(expected, ignoreOrder: true)` for unordered collection comparison, `.Count.ShouldBe(n)` (property not method)
- **Argument Parsing**: For quoted strings, use state machine approach (not regex) following CommandLineToArgvW conventions
- **OAuth Device Flow**: GitHub device flow doesn't require client_secret; poll with backoff per RFC 8628
- **Loop Command**: LoopService orchestrates plan/build phases; LoopConfigService merges user/project configs; LoopStateManager tracks lopen.loop.done file
- **Config Merge Pattern**: Use record `with` expressions and MergeWith() for config precedence (defaults → user → project → custom)
- **Spectre.Console Rule**: Use `new Rule(title)` for horizontal separators; ConsoleOutput.Rule() wraps for NO_COLOR support
