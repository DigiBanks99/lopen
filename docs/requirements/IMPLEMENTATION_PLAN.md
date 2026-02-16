# Implementation Plan

## Current Job: JOB-079 — Implement `lopen auth` CLI Subcommands

**Module**: cli  
**Priority**: P4  
**Description**: Implement `lopen auth login`, `lopen auth status`, and `lopen auth logout` subcommands wired to the Auth module's `IAuthService`. This is the first CLI command implementation and establishes the command pattern for all future commands.

### Acceptance Criteria

- AC1: `lopen auth login` initiates the Copilot SDK device flow via `IAuthService.LoginAsync`
- AC2: `lopen auth status` reports current authentication state via `IAuthService.GetStatusAsync`
- AC3: `lopen auth logout` clears SDK-managed credentials via `IAuthService.LogoutAsync`
- AC4: Exit code 0 on success, 1 on failure for all auth subcommands
- AC5: The CLI command pattern (closure-captured `IServiceProvider`, thin handlers) is established and reusable

### Tasks

- [x] **1. Create `AuthCommand.cs`** in `src/Lopen/Commands/` — static factory method that builds the `auth` parent `Command` with three subcommands (`login`, `status`, `logout`). Each subcommand's `SetAction` resolves `IAuthService` from the closure-captured `IServiceProvider` and delegates to the corresponding method. Returns exit code 0 on success, 1 on exception.
- [x] **2. Update `Program.cs`** — add `auth` command to `rootCommand` via `rootCommand.Add(AuthCommand.Create(host.Services))`. Import `Lopen.Commands` namespace.
- [x] **3. Format status output** — `lopen auth status` writes State, Source, User (if present), Error (if present) to stdout.
- [x] **4. Write unit tests** in `tests/Lopen.Cli.Tests/Commands/AuthCommandTests.cs` — 8 tests covering login/status/logout success and failure paths with FakeAuthService.
- [x] **5. Validate** — `dotnet build` and `dotnet test` pass (391 tests total).

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| Closure-captured `IServiceProvider` | `System.CommandLine.Hosting` integration is deprecated in 2.0.0-beta5; passing the provider via closure is the simplest pattern that works with `SetAction` |
| Static factory `AuthCommand.Create(IServiceProvider)` | Keeps command construction testable and separate from `Program.cs`; returns a fully-configured `Command` |
| `src/Lopen/Commands/` directory | Groups CLI command definitions; mirrors the convention of one file per command group |
| Thin handlers, all logic in `IAuthService` | CLI layer is a pass-through; no business logic in command handlers. Matches the spec's "thin CLI commands" principle |
| `Console.Out` for status output (no `IConsole`) | Auth commands don't need TUI; simple console writes are sufficient and avoid unnecessary abstraction at this stage |
| Hand-rolled `FakeAuthService` | Matches existing test patterns (no mocking library); fake can record calls and return canned results |
| Test via `CommandLineConfiguration.InvokeAsync` | Tests the full command parsing + dispatch pipeline, not just handler methods |

### Files to Create/Modify

| File | Action |
|------|--------|
| `src/Lopen/Commands/AuthCommand.cs` | **Create** — `auth` parent command with `login`, `status`, `logout` subcommands |
| `src/Lopen/Program.cs` | **Modify** — wire `AuthCommand.Create(host.Services)` into `rootCommand` |
| `tests/Lopen.Cli.Tests/Commands/AuthCommandTests.cs` | **Create** — unit/integration tests for all three subcommands |
| `tests/Lopen.Cli.Tests/Fakes/FakeAuthService.cs` | **Create** — hand-rolled test double for `IAuthService` |

### Command Pattern Reference

This is the pattern all future CLI commands should follow:

```csharp
// src/Lopen/Commands/AuthCommand.cs
public static class AuthCommand
{
    public static Command Create(IServiceProvider services)
    {
        var auth = new Command("auth", "Manage authentication");

        var login = new Command("login", "Authenticate via Copilot SDK device flow");
        login.SetAction(async (parseResult, cancellationToken) =>
        {
            var authService = services.GetRequiredService<IAuthService>();
            try
            {
                await authService.LoginAsync(cancellationToken);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        });

        auth.AddCommand(login);
        // ... status, logout follow same pattern
        return auth;
    }
}
```

```csharp
// Program.cs addition
rootCommand.AddCommand(AuthCommand.Create(host.Services));
```

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-079 | cli | Auth CLI subcommands (login/status/logout) |
| JOB-075 | core | Core AC tests (all 24 ACs) |
| JOB-018 | llm | Automatic token renewal and failed renewal handling |
| JOB-057 | llm | LLM AC tests (all 14 ACs) |