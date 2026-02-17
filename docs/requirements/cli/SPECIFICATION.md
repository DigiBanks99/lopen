---
name: cli
description: The CLI command interface requirements of Lopen
---

# CLI Specification

## Overview

Lopen is a .NET CLI application. The CLI is the entry point for all user interactions — both interactive (TUI) and headless (CI/scripting). By default, all commands launch the TUI. Headless mode is opt-in via `--headless` / `--quiet` / `-q`.

### Design Principles

1. **TUI by Default** — Every command opens the TUI unless `--headless` is specified
2. **Phase Commands** — The three workflow phases are first-class subcommands
3. **Prompt Injection** — A `--prompt` flag enables programmatic instruction of Lopen
4. **Consistent Flags** — Global flags work across all commands (where applicable)
5. **Conventional CLI** — Follows standard CLI conventions (`--help`, `--version`, subcommands)

---

## Commands

### Root Command

```sh
lopen [options]
```

Starts the TUI with the full workflow. If an active session exists, offers to resume it. Otherwise, begins a new session.

In headless mode (`--headless`), runs the full workflow autonomously. Requires `--prompt` or an existing session to know what to do.

---

### Phase Commands

These commands scope Lopen to a specific workflow phase (see [Core § The 3 Phases](../core/SPECIFICATION.md#the-3-phases)):

#### `lopen spec`

```sh
lopen spec [options]
```

Runs the **Requirement Gathering** phase (step 1). Conducts a guided conversation to draft or refine a module specification.

- In TUI mode: Interactive conversation with the user
- In headless mode: Requires `--prompt` to provide the initial idea/requirements

#### `lopen plan`

```sh
lopen plan [options]
```

Runs the **Planning** phase (steps 2–5). Determines dependencies, identifies components, selects the next component, and breaks it into tasks.

- Requires an existing specification (errors if none found for the target module)
- In TUI mode: Shows planning progress, allows user review
- In headless mode: Runs planning autonomously, outputs results

#### `lopen build`

```sh
lopen build [options]
```

Runs the **Building** phase (steps 6–7). Iteratively builds components by executing tasks and repeating the component loop.

- Requires an existing specification and plan
- In TUI mode: Shows build progress, handles user intervention on failures
- In headless mode: Runs autonomously; failure intervention controlled by `--unattended` and [Configuration § failure_threshold](../configuration/SPECIFICATION.md)

---

### Utility Commands

#### `lopen auth`

Authentication management for the GitHub Copilot SDK (see [Auth Specification](../auth/SPECIFICATION.md)):

```sh
lopen auth login          # Authenticate with GitHub
lopen auth status         # Check current authentication state
lopen auth logout         # Clear stored credentials
```

`--prompt` is **not** applicable to auth commands.

#### `lopen session`

Session management (see [Storage § Session Persistence](../storage/SPECIFICATION.md#session-persistence)):

```sh
lopen session list                  # List all sessions (active and completed)
lopen session show [session-id]     # Show details of a session (latest if no ID)
lopen session show --format <fmt>   # Output as md, json, or yaml
lopen session resume [session-id]   # Resume a specific session
lopen session delete <session-id>   # Delete a session
lopen session prune                 # Remove completed sessions beyond retention limit
```

#### `lopen revert`

```sh
lopen revert [options]
```

Rolls back to the last known-good commit (the most recent task-completion commit). See [Core § Git Safety & Rollback](../core/SPECIFICATION.md#git-safety--rollback).

- Identifies the last task-completion commit on the current module branch
- Performs a `git revert` or `git reset` to that commit
- Updates session state to reflect the rollback
- Informs the LLM of the rollback in the next iteration context
- In headless mode: outputs the reverted commit SHA and exits
- Errors if no task-completion commits exist or if the working tree has uncommitted changes

#### `lopen config`

Configuration inspection (see [Configuration Specification](../configuration/SPECIFICATION.md)):

```sh
lopen config show                   # Display resolved config with sources
lopen config show --json            # Machine-readable output
```

---

## Global Flags

These flags are available on all commands (unless noted):

| Flag                   | Short | Description                                        | Applicable To           |
| ---------------------- | ----- | -------------------------------------------------- | ----------------------- |
| `--headless`           | `-q`  | Run without TUI; output to stdout                  | All except `auth`       |
| `--quiet`              |       | Alias for `--headless`                             | All except `auth`       |
| `--prompt <text>`      | `-p`  | Inject instructions for the LLM                    | Root, spec, plan, build |
| `--model <name>`       |       | Override model for all phases                      | Root, spec, plan, build |
| `--unattended`         |       | Suppress intervention prompts on repeated failures | Root, spec, plan, build |
| `--resume <id>`        |       | Resume a specific session                          | Root, spec, plan, build |
| `--no-resume`          |       | Force a new session (skip resume prompt)           | Root, spec, plan, build |
| `--max-iterations <n>` |       | Maximum loop iterations before pausing             | Root, build             |
| `--help`               | `-h`  | Show help for command                              | All                     |
| `--version`            |       | Show Lopen version                                 | Root only               |

---

## Headless Mode Behavior

When `--headless` / `--quiet` / `-q` is specified:

- **No TUI** — Output is plain text to stdout/stderr
- **Non-interactive** — Lopen does not prompt the user for input
- **Prompt required** — For commands that need user input (e.g., `lopen spec`), `--prompt` provides the initial instruction
- **Session selection** — Uses `--resume <id>` or the latest active session. If no session and no `--prompt`, errors with guidance
- **Progress output** — Emits structured progress messages (step transitions, task completions, errors) to stdout
- **Exit codes** — `0` for success, `1` for failure, `2` for user intervention required (only in `--unattended` mode when threshold hit)

### Headless + Prompt Injection

The `--prompt` flag accepts a text string that is injected into the LLM context as user instructions:

```sh
# Start spec gathering with an initial idea
lopen spec --headless --prompt "Build an authentication module using JWT tokens"

# Continue building with specific guidance
lopen build --headless --resume auth-20260214-1357 --prompt "Focus on the session management component next"
```

In TUI mode, `--prompt` provides an initial message that populates the input — the user can edit before sending.

---

## Project Structure

Lopen is a .NET 10.0 solution. The project structure follows standard .NET conventions:

### Solution Layout

```
Lopen.sln
Directory.Build.props          # Shared build properties (LangVersion, Nullable, TreatWarningsAsErrors)
Directory.Packages.props       # Centralized package version management
src/
  Lopen/                       # CLI entry point (console application)
  Lopen.Core/                  # Core orchestration (workflow, task management, back-pressure)
  Lopen.Llm/                   # LLM/Copilot SDK integration
  Lopen.Storage/               # Storage and persistence
  Lopen.Configuration/         # Configuration hierarchy
  Lopen.Auth/                  # Authentication
  Lopen.Tui/                   # Terminal UI components
  Lopen.Otel/                  # OpenTelemetry instrumentation
  Lopen.AppHost/               # Aspire AppHost for local development
tests/
  Lopen.Core.Tests/            # Core module tests
  Lopen.Llm.Tests/             # LLM module tests
  Lopen.Storage.Tests/         # Storage module tests
  Lopen.Configuration.Tests/   # Configuration module tests
  Lopen.Auth.Tests/            # Auth module tests
  Lopen.Tui.Tests/             # TUI module tests
  Lopen.Otel.Tests/            # OTEL module tests
  Lopen.Cli.Tests/             # CLI integration tests
```

### Build & Test

- `dotnet build` — Compile all projects
- `dotnet test` — Run all tests
- `dotnet format --verify-no-changes` — Verify code formatting
- `dotnet publish -c Release` — Publish release build

### Hosting Model

The CLI entry point uses `Microsoft.Extensions.Hosting` for dependency injection, configuration, and logging:

- `IHostBuilder` configures services, configuration, and logging
- Services are registered via dependency injection
- The host is built and run synchronously for CLI execution

---

## Notes

This specification defines the **CLI command structure and flags**. It does not define what happens inside each command — that's the [Core Workflow](../core/SPECIFICATION.md). It does not define how the TUI renders — that's the [TUI Specification](../tui/SPECIFICATION.md).

---

## Acceptance Criteria

- [x] [CLI-01] `lopen` (root command) starts the TUI with full workflow; offers session resume if active session exists
- [x] [CLI-02] `lopen --headless` runs the full workflow autonomously with plain text output to stdout/stderr
- [x] [CLI-03] `lopen spec` runs the Requirement Gathering phase (step 1) with guided conversation
- [x] [CLI-04] `lopen plan` runs the Planning phase (steps 2–5); errors if no specification exists for the target module
- [x] [CLI-05] `lopen build` runs the Building phase (steps 6–7); errors if no specification and plan exist
- [x] [CLI-06] `lopen auth login` initiates the Copilot SDK device flow
- [x] [CLI-07] `lopen auth status` reports current authentication state
- [x] [CLI-08] `lopen auth logout` clears SDK-managed credentials
- [x] [CLI-09] `lopen session list` lists all sessions (active and completed)
- [x] [CLI-10] `lopen session show` displays session details with optional `--format` flag
- [x] [CLI-11] `lopen session resume [id]` resumes a specific session
- [x] [CLI-12] `lopen session delete <id>` deletes a session
- [x] [CLI-13] `lopen session prune` removes completed sessions beyond retention limit
- [x] [CLI-14] `lopen config show` displays resolved configuration with sources
- [x] [CLI-15] `lopen revert` rolls back to the last task-completion commit and updates session state
- [x] [CLI-16] `--headless` / `--quiet` / `-q` disables TUI entirely; output is plain text to stdout/stderr
- [x] [CLI-17] `--prompt <text>` injects user instructions into LLM context
- [x] [CLI-18] `--prompt` in TUI mode populates the input field for user review before sending
- [x] [CLI-19] Headless mode without `--prompt` and without an active session errors with guidance
- [x] [CLI-20] Exit codes: `0` success, `1` failure, `2` user intervention required (headless + unattended)
- [ ] [CLI-21] `--help` and `--version` flags work as expected
- [x] [CLI-22] .NET solution builds successfully with `dotnet build`
- [x] [CLI-23] All test projects run successfully with `dotnet test`
- [x] [CLI-24] Code formatting passes with `dotnet format --verify-no-changes`
- [x] [CLI-25] CLI entry point uses `Microsoft.Extensions.Hosting` for dependency injection
- [x] [CLI-26] CLI discovers the project root directory (nearest parent containing `.lopen/` or `.git/`, falling back to CWD) and passes it to `AddLopenCore(projectRoot)` and `AddLopenStorage(projectRoot)` so all path-dependent services are registered
- [x] [CLI-27] `--no-welcome` flag suppresses the TUI landing page modal on startup

---

## Dependencies

- **[Core module](../core/SPECIFICATION.md)** — Workflow phases, step logic, session management
- **[LLM module](../llm/SPECIFICATION.md)** — SDK invocation for prompt injection and model override
- **[Auth module](../auth/SPECIFICATION.md)** — Authentication commands (`lopen auth` subcommands)
- **[Storage module](../storage/SPECIFICATION.md)** — Session persistence for resume/list/show/delete/prune commands
- **[Configuration module](../configuration/SPECIFICATION.md)** — Settings resolution and `lopen config` commands
- **[TUI module](../tui/SPECIFICATION.md)** — Interactive terminal UI (default mode)
- **System.CommandLine** — .NET CLI parsing library (or equivalent)

---

## Skills & Hooks

- **verify-cli-parse**: Validate that all registered commands and flags parse correctly without runtime errors

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow phases and steps
- [LLM Specification](../llm/SPECIFICATION.md) — Model selection, prompt construction
- [Storage Specification](../storage/SPECIFICATION.md) — Session management
- [Configuration Specification](../configuration/SPECIFICATION.md) — Settings and defaults
- [Auth Specification](../auth/SPECIFICATION.md) — Authentication commands
- [TUI Specification](../tui/SPECIFICATION.md) — Interactive UI rendering
