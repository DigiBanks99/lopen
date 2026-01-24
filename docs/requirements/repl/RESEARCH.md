# REPL Module Research

> Research for REQ-010: REPL Mode implementation

## Approach

### 1. Architecture Overview

The REPL will be implemented as:
- `ReplService` in Lopen.Core - Core REPL logic (testable)
- `repl` command in Lopen.Cli - CLI entry point

### 2. Design Decisions

#### Input/Output Abstraction
Use interfaces to make REPL testable:
- `IConsoleInput` - Abstracts Console.ReadLine()
- Reuse existing `ConsoleOutput` for output

#### Command Execution
Leverage System.CommandLine:
- `RootCommand.Parse(args).Invoke()` can execute user input
- Split user input into args array for parsing
- Handle built-in REPL commands (exit, quit) separately

#### Signal Handling
- `Console.CancelKeyPress` for Ctrl+C
- Return gracefully instead of Environment.Exit()
- Use CancellationToken for clean shutdown

### 3. Component Design

```csharp
// Input abstraction for testing
public interface IConsoleInput
{
    string? ReadLine();
    event ConsoleCancelEventHandler? CancelKeyPress;
}

// REPL Service
public class ReplService
{
    public ReplService(IConsoleInput input, ConsoleOutput output) { }
    
    public async Task<int> RunAsync(
        Func<string[], Task<int>> commandExecutor,
        CancellationToken cancellationToken = default) { }
}
```

### 4. Exit Conditions
- User types "exit" or "quit"
- User presses Ctrl+C
- User presses Ctrl+D (EOF)

### 5. Prompt Display
Format: `lopen> ` or customizable prompt

### 6. Test Strategy
- Unit test ReplService with mock IConsoleInput
- Use Spectre.Console.Testing.TestConsole for output
- CLI integration tests for `lopen repl` command

## Implementation Notes

- Use `Console.CancelKeyPress += handler` with `e.Cancel = true` to prevent immediate exit
- Split input using simple space-based tokenization (later: proper shell parsing)
- Ignore empty input (user just presses Enter)
- Handle whitespace-only input gracefully

## References

- Spectre.Console.Testing for IAnsiConsole mocking
- System.CommandLine ParseResult for command execution
