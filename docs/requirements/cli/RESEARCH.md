# Research: CLI Module — .NET CLI Implementation

> **Date:** 2026-02-15
> **Sources:** [dotnet/command-line-api](https://github.com/dotnet/command-line-api), [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console), Microsoft .NET documentation, CLI specification

---

## 1. System.CommandLine

### Package & Version

| Property       | Value                         |
| -------------- | ----------------------------- |
| NuGet Package  | `System.CommandLine`          |
| Latest Version | `2.0.3` (stable GA)          |
| Status         | **Stable** — GA since 2025   |
| Repository     | `dotnet/command-line-api`     |

`System.CommandLine` is the official Microsoft CLI parsing library. It reached GA with version 2.0.0 and the API surface is stable. Note: the GA release introduced significant breaking changes from the beta4 API (see below).

### Installation

```sh
dotnet add package System.CommandLine
```

### Basic Usage

```csharp
using System.CommandLine;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI agent");

var headlessOption = new Option<bool>("--headless", "-q")
{
    Description = "Run without TUI"
};

rootCommand.Options.Add(headlessOption);

rootCommand.SetAction((parseResult) =>
{
    bool headless = parseResult.GetValue(headlessOption);
    // Entry point logic
});

return rootCommand.Parse(args).Invoke();
```

### Handler Model

Handlers bind parsed options/arguments to method parameters via `SetAction`. Use `ParseResult` to access parsed values, and `CancellationToken` for async handlers:

```csharp
command.SetAction(async (parseResult, cancellationToken) =>
{
    var model = parseResult.GetValue(modelOption);
    // cancellationToken is triggered by Ctrl+C
    return 0; // exit code
});
```

### Relevance to Lopen

System.CommandLine maps directly to the CLI specification's command structure. It supports subcommands, global options (via `Recursive = true`), aliases, help generation, version display, and exit codes out of the box. Async `SetAction` handlers receive a `CancellationToken` which is essential for TUI cleanup on Ctrl+C.

---

## 2. .NET CLI Project Structure

### Recommended Solution Layout

```
lopen/
├── lopen.sln
├── Directory.Build.props          # Shared MSBuild properties
├── Directory.Packages.props       # Central Package Management
├── global.json                    # Pin .NET 10.0 SDK
├── src/
│   ├── Lopen/                     # CLI entry point (executable)
│   ├── Lopen.Core/                # Workflow orchestration
│   ├── Lopen.Llm/                 # Copilot SDK integration
│   ├── Lopen.Storage/             # Session persistence
│   ├── Lopen.Configuration/       # Settings resolution
│   ├── Lopen.Auth/                # GitHub authentication
│   ├── Lopen.Tui/                 # Terminal UI (Spectre.Console)
│   ├── Lopen.Otel/                # OpenTelemetry integration
│   └── Lopen.AppHost/             # Aspire AppHost for local development
├── tests/
│   ├── Lopen.Cli.Tests/
│   ├── Lopen.Core.Tests/
│   └── ...
└── docs/
```

### Key Conventions

- **Thin CLI, fat libraries** — `Lopen` (the CLI project) is command definitions + DI wiring only. All logic lives in library projects.
- **1:1 module mapping** — Each `docs/requirements/<module>/` maps to a `src/Lopen.<Module>/` project.
- **Central Package Management** — `Directory.Packages.props` at root for NuGet version consistency.
- **`Microsoft.NET.Sdk`** — Use the standard SDK (not `Microsoft.NET.Sdk.Web`) for CLI apps.

### Project File

```xml
<!-- src/Lopen/Lopen.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### CLI Project Internal Structure

```
src/Lopen/
├── Program.cs                     # Entry point, host builder
├── Commands/
│   ├── RootCommand.cs             # lopen [options]
│   ├── SpecCommand.cs             # lopen spec
│   ├── PlanCommand.cs             # lopen plan
│   ├── BuildCommand.cs            # lopen build
│   ├── RevertCommand.cs           # lopen revert
│   ├── Auth/
│   │   ├── AuthCommand.cs         # lopen auth (parent)
│   │   ├── LoginCommand.cs        # lopen auth login
│   │   ├── StatusCommand.cs       # lopen auth status
│   │   └── LogoutCommand.cs       # lopen auth logout
│   ├── Session/
│   │   ├── SessionCommand.cs      # lopen session (parent)
│   │   ├── ListCommand.cs         # lopen session list
│   │   ├── ShowCommand.cs         # lopen session show
│   │   ├── ResumeCommand.cs       # lopen session resume
│   │   ├── DeleteCommand.cs       # lopen session delete
│   │   └── PruneCommand.cs        # lopen session prune
│   └── Config/
│       ├── ConfigCommand.cs       # lopen config (parent)
│       └── ShowCommand.cs         # lopen config show
└── Lopen.csproj
```

### Relevance to Lopen

The project-per-module approach mirrors the existing `docs/requirements/` structure. The CLI project stays thin — it only wires up commands and DI, delegating all behavior to module libraries. This enables independent testing of each module and potential future reuse.

---

## 3. Subcommand Pattern

### Adding Subcommands

```csharp
var rootCommand = new RootCommand("Lopen");

// Phase commands (top-level)
rootCommand.Subcommands.Add(new Command("spec", "Run requirement gathering"));
rootCommand.Subcommands.Add(new Command("plan", "Run planning phase"));
rootCommand.Subcommands.Add(new Command("build", "Run building phase"));
rootCommand.Subcommands.Add(new Command("revert", "Rollback to last good commit"));
```

### Nested Subcommands

For `auth` and `session` which have their own sub-hierarchy:

```csharp
// auth login | auth status | auth logout
var authCommand = new Command("auth", "Authentication management");
authCommand.Subcommands.Add(new Command("login", "Authenticate with GitHub"));
authCommand.Subcommands.Add(new Command("status", "Check authentication state"));
authCommand.Subcommands.Add(new Command("logout", "Clear credentials"));
rootCommand.Subcommands.Add(authCommand);

// session list | session show | session resume | session delete | session prune
var sessionCommand = new Command("session", "Session management");
sessionCommand.Subcommands.Add(new Command("list", "List all sessions"));
sessionCommand.Subcommands.Add(new Command("show", "Show session details"));
sessionCommand.Subcommands.Add(new Command("resume", "Resume a session"));
sessionCommand.Subcommands.Add(new Command("delete", "Delete a session"));
sessionCommand.Subcommands.Add(new Command("prune", "Remove old sessions"));
rootCommand.Subcommands.Add(sessionCommand);

// config show
var configCommand = new Command("config", "Configuration inspection");
configCommand.Subcommands.Add(new Command("show", "Display resolved config"));
rootCommand.Subcommands.Add(configCommand);
```

### Command-Specific Options

Options can be scoped to specific commands. For example, `session show --format`:

```csharp
var showCommand = new Command("show", "Show session details");
var formatOption = new Option<string>("--format")
{
    Description = "Output format (md, json, yaml)",
    DefaultValueFactory = _ => "md"
};
showCommand.Options.Add(formatOption);

var sessionIdArg = new Argument<string?>("session-id")
{
    Description = "Session ID (latest if omitted)",
    Arity = ArgumentArity.ZeroOrOne
};
showCommand.Arguments.Add(sessionIdArg);
```

### Relevance to Lopen

Every command in the CLI specification maps to a `Command` instance. Nested commands (`auth login`, `session list`, `config show`) use `Subcommands.Add()` on parent commands. This gives automatic `--help` generation at every level.

---

## 4. Global Flags

### Recursive Option (Global)

Setting `Recursive = true` on an option makes it available to **all** subcommands automatically:

```csharp
var rootCommand = new RootCommand("Lopen");

// Global options from CLI specification
var headlessOption = new Option<bool>("--headless", "-q")
{
    Description = "Run without TUI; output to stdout",
    Recursive = true
};
rootCommand.Options.Add(headlessOption);

var quietOption = new Option<bool>("--quiet")
{
    Description = "Alias for --headless",
    Recursive = true
};
rootCommand.Options.Add(quietOption);

var promptOption = new Option<string?>("--prompt", "-p")
{
    Description = "Inject instructions for the LLM",
    Recursive = true
};
rootCommand.Options.Add(promptOption);

var modelOption = new Option<string?>("--model")
{
    Description = "Override model for all phases",
    Recursive = true
};
rootCommand.Options.Add(modelOption);

var unattendedOption = new Option<bool>("--unattended")
{
    Description = "Suppress intervention prompts",
    Recursive = true
};
rootCommand.Options.Add(unattendedOption);

var resumeOption = new Option<string?>("--resume")
{
    Description = "Resume a specific session",
    Recursive = true
};
rootCommand.Options.Add(resumeOption);

var noResumeOption = new Option<bool>("--no-resume")
{
    Description = "Force a new session",
    Recursive = true
};
rootCommand.Options.Add(noResumeOption);

var maxIterationsOption = new Option<int?>("--max-iterations")
{
    Description = "Max loop iterations before pausing",
    Recursive = true
};
rootCommand.Options.Add(maxIterationsOption);
```

### Accessing Global Options in Handlers

```csharp
specCommand.SetAction((parseResult) =>
{
    bool headless = parseResult.GetValue(headlessOption);
    string? prompt = parseResult.GetValue(promptOption);
    string? model = parseResult.GetValue(modelOption);

    // All recursive options are accessible from any subcommand handler
});
```

### Applicability Constraints

The specification notes that some global flags don't apply to all commands (e.g., `--prompt` is not applicable to `auth`). System.CommandLine does not enforce this — validation must be done in the handler or via a custom validator:

```csharp
// Option: validate in handler
authLoginCommand.SetAction((parseResult) =>
{
    if (parseResult.GetValue(promptOption) is not null)
    {
        Console.Error.Write("--prompt is not applicable to auth commands");
        return 1;
    }
    return 0;
});
```

### Relevance to Lopen

All global flags from the specification (`--headless`, `--quiet`, `--prompt`, `--model`, `--unattended`, `--resume`, `--no-resume`, `--max-iterations`) map to options with `Recursive = true`. Applicability constraints (e.g., `--prompt` not valid for `auth`) need explicit validation. `RootCommand` automatically provides `--help` and `--version`.

---

## 5. Exit Codes

### Standard Conventions

| Code    | Meaning                                         |
| ------- | ----------------------------------------------- |
| `0`     | Success                                         |
| `1`     | General failure                                 |
| `2`     | Misuse / user intervention required             |
| `130`   | Terminated by SIGINT (Ctrl+C)                   |

### Lopen Exit Codes (from specification)

| Code | Meaning                                                       |
| ---- | ------------------------------------------------------------- |
| `0`  | Success                                                       |
| `1`  | Failure                                                       |
| `2`  | User intervention required (headless + unattended mode only)  |

### Setting Exit Codes in System.CommandLine

```csharp
// Via return value from SetAction (preferred)
command.SetAction((parseResult) =>
{
    return 0; // or 1, or 2
});

// Via return value from Main
static int Main(string[] args)
{
    var root = BuildRootCommand();
    return root.Parse(args).Invoke();
    // Invoke() returns the handler's exit code
}
```

### Exception-to-Exit-Code Mapping

```csharp
command.SetAction(async (parseResult, cancellationToken) =>
{
    try
    {
        await RunWorkflowAsync(parseResult, cancellationToken);
        return 0;
    }
    catch (UserInterventionRequiredException)
    {
        return 2;
    }
    catch (OperationCanceledException)
    {
        return 130;
    }
    catch (Exception)
    {
        return 1;
    }
});
```

### Relevance to Lopen

Use `return root.Parse(args).Invoke()` from `Main` to propagate exit codes. Map domain exceptions to exit codes in each handler. The exit code `2` only applies in headless + unattended mode when the failure threshold is reached.

---

## 6. Headless vs TUI Mode

### Architecture

The specification requires two output modes:

1. **TUI mode** (default) — Interactive terminal UI using Spectre.Console
2. **Headless mode** (`--headless` / `--quiet` / `-q`) — Plain text to stdout/stderr, non-interactive

### Spectre.Console Capability Detection

Spectre.Console can auto-detect terminal capabilities:

```csharp
AnsiConsole.Profile.Capabilities.Interactive // true if TTY (can prompt)
AnsiConsole.Profile.Capabilities.Ansi        // true if ANSI escapes supported
AnsiConsole.Profile.ColorSystem              // None, Legacy, Standard, EightBit, TrueColor
```

### IAnsiConsole Abstraction

`IAnsiConsole` enables DI-based mode switching:

```csharp
// TUI mode — use the default interactive console
IAnsiConsole tuiConsole = AnsiConsole.Console;

// Headless mode — create a plain-text, non-interactive console
IAnsiConsole headlessConsole = AnsiConsole.Create(new AnsiConsoleSettings
{
    Ansi = AnsiSupport.No,
    ColorSystem = ColorSystemSupport.NoColors,
    Interactive = InteractionSupport.No,
    Out = new AnsiConsoleOutput(Console.Out)
});
```

### Recommended Pattern: Output Abstraction

Define an interface that each mode implements:

```csharp
public interface IOutputRenderer
{
    Task RenderProgressAsync(string phase, string step, double progress);
    Task RenderErrorAsync(string message, Exception? ex = null);
    Task RenderResultAsync(string result);
    Task<string?> PromptAsync(string message); // returns null in headless
}

// TUI implementation uses Spectre.Console widgets (Status, Progress, etc.)
public class TuiRenderer : IOutputRenderer { /* ... */ }

// Headless implementation writes plain text to stdout/stderr
public class HeadlessRenderer : IOutputRenderer { /* ... */ }
```

### DI Registration Based on --headless Flag

```csharp
services.AddSingleton<IOutputRenderer>(sp =>
{
    var options = sp.GetRequiredService<GlobalOptions>();
    return options.Headless
        ? new HeadlessRenderer()
        : new TuiRenderer(AnsiConsole.Console);
});
```

### Relevance to Lopen

The `IOutputRenderer` abstraction (or similar) is the key architectural decision. Commands never write directly to `Console` or `AnsiConsole` — they use the injected renderer. This cleanly separates the TUI module from the CLI module. In headless mode, progress is emitted as structured text lines; in TUI mode, it's Spectre.Console widgets.

---

## 7. Recommended NuGet Packages

### Core CLI Packages

| Package                         | Purpose                                          | Version Note    |
| ------------------------------- | ------------------------------------------------ | --------------- |
| `System.CommandLine`            | CLI parsing, subcommands, options, help           | `2.0.3` (stable GA) |
| `Microsoft.Extensions.Hosting`  | DI container, configuration, logging              | `10.0.3` (stable) |
| `Microsoft.Extensions.Options`  | Strongly-typed configuration binding              | `10.0.3` (stable) |

> **Note:** `System.CommandLine.Hosting` (the bridge between System.CommandLine and Generic Host) is deprecated. Wire up DI directly using `Microsoft.Extensions.Hosting` and pass the `IServiceProvider` into command handlers via closure or a custom command base class.

### TUI Packages

| Package                    | Purpose                                    | Version Note |
| -------------------------- | ------------------------------------------ | ------------ |
| `Spectre.Console`          | Rich terminal rendering (tables, progress) | `0.54.0` (stable) |
| `Spectre.Console.Json`     | JSON syntax highlighting in terminal       | `0.54.0` (stable) |

> **Note:** `Spectre.Console.Cli` is a separate CLI parsing library from Spectre. We are **not** using it — System.CommandLine handles CLI parsing. We only use `Spectre.Console` for rendering.

### LLM & Auth Packages

| Package                          | Purpose                         |
| -------------------------------- | ------------------------------- |
| `Microsoft.Extensions.AI`        | LLM abstraction layer           |
| `Azure.Identity` (or equivalent) | GitHub Copilot authentication   |

### Telemetry Packages

| Package                                         | Purpose                 |
| ------------------------------------------------ | ----------------------- |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol`   | OTLP export             |
| `OpenTelemetry.Extensions.Hosting`               | Host integration        |
| `OpenTelemetry.Instrumentation.Http`             | HttpClient tracing      |
| `OpenTelemetry.Instrumentation.Runtime`          | .NET runtime metrics    |

### Relevance to Lopen

The `System.CommandLine` package (now GA) provides CLI parsing with full DI support via manual wiring. The deprecated `System.CommandLine.Hosting` bridge package is no longer needed — build a `HostApplicationBuilder`, configure services, and pass the `IServiceProvider` into handlers. Spectre.Console handles TUI rendering only (not CLI parsing). The `Microsoft.Extensions.Hosting` stack provides the DI container, configuration, and logging infrastructure that all modules plug into.

---

## 8. Implementation Approach

### Step 1: Scaffold the Solution

```sh
# Create solution and projects
dotnet new sln -n lopen
dotnet new console -n Lopen -o src/Lopen
dotnet new classlib -n Lopen.Core -o src/Lopen.Core

dotnet sln add src/Lopen
dotnet sln add src/Lopen.Core
dotnet add src/Lopen reference src/Lopen.Core
```

### Step 2: Pin the SDK

```jsonc
// global.json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  }
}
```

### Step 3: Central Package Management

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="System.CommandLine" Version="2.0.3" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.3" />
    <PackageVersion Include="Spectre.Console" Version="0.54.0" />
  </ItemGroup>
</Project>
```

### Step 4: Shared Build Properties

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### Step 5: Minimal Program.cs

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Build the DI container
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton</* module services */>();

using var host = builder.Build();

// Build the CLI command tree
var rootCommand = new LopenRootCommand(host.Services);

return rootCommand.Parse(args).Invoke();
```

### Step 6: Command Registration Pattern

Each command is a self-contained class:

```csharp
public class LopenRootCommand : RootCommand
{
    public LopenRootCommand(IServiceProvider services)
        : base("Lopen - GitHub Copilot workflow agent")
    {
        // Global options (Recursive = true makes them available to all subcommands)
        Options.Add(GlobalOptions.Headless);
        Options.Add(GlobalOptions.Prompt);
        Options.Add(GlobalOptions.Model);
        Options.Add(GlobalOptions.Unattended);
        Options.Add(GlobalOptions.Resume);
        Options.Add(GlobalOptions.NoResume);
        Options.Add(GlobalOptions.MaxIterations);

        // Phase commands
        Subcommands.Add(new SpecCommand(services));
        Subcommands.Add(new PlanCommand(services));
        Subcommands.Add(new BuildCommand(services));

        // Utility commands
        Subcommands.Add(new AuthCommand(services));
        Subcommands.Add(new SessionCommand(services));
        Subcommands.Add(new RevertCommand(services));
        Subcommands.Add(new ConfigCommand(services));
    }
}
```

### Build Order

1. **Lopen** — Command definitions, `Program.cs`, DI wiring (this module)
2. **Lopen.Core** — Workflow orchestration (depends on specification)
3. **Lopen.Auth** — Authentication (early dependency — needed for LLM calls)
4. **Lopen.Configuration** — Settings resolution
5. **Lopen.Storage** — Session persistence
6. **Lopen.Llm** — Copilot SDK integration
7. **Lopen.Tui** — Terminal UI rendering
8. **Lopen.Otel** — Telemetry (can be added at any point)

### Relevance to Lopen

Start with the CLI skeleton and Core library. The CLI module is the entry point but should be implemented early because it defines the DI container and command structure that all other modules plug into. Each module registers its services via `IServiceCollection` extensions, keeping the CLI project thin.

---

## 9. Headless Workflow Runner (`--headless`)

### Problem

`lopen --headless` must run the full 7-step workflow autonomously, writing plain text to stdout/stderr with no TUI. The current `RootCommandHandler` resolves `ITuiApplication` and calls `RunAsync()` for all cases. Headless mode needs to bypass the TUI and drive the workflow directly.

### Existing Infrastructure

The codebase already provides the pieces needed:

| Component | Location | Purpose |
| --- | --- | --- |
| `IWorkflowEngine` | `Lopen.Core.Workflow` | Stateless-based 7-step state machine with `InitializeAsync`, `Fire`, `GetPermittedTriggers`, `IsComplete` |
| `IStateAssessor` | `Lopen.Core.Workflow` | Determines current step from codebase state |
| `IPhaseTransitionController` | `Lopen.Core.Workflow` | Human-gated (spec→planning) and auto transitions |
| `IFailureHandler` | `Lopen.Core.Workflow` | Failure classification with threshold-based escalation |
| `IOutputRenderer` | `Lopen.Core` | Abstracts headless vs TUI output (`RenderProgressAsync`, `RenderErrorAsync`, `RenderResultAsync`, `PromptAsync`) |
| `HeadlessRenderer` | `Lopen.Core` | Plain text implementation — writes `[phase] step (pct%)` to stdout, errors to stderr |
| `ISessionManager` | `Lopen.Storage` | Session creation, resume, state persistence |
| `ILlmService` | `Lopen.Llm` | LLM invocation per phase |
| `ExitCodes` | `Lopen.Commands` | `Success=0`, `Failure=1`, `UserInterventionRequired=2` |

### Wiring the WorkflowEngine from the CLI

The `IWorkflowEngine` is registered via `AddLopenCore()` but is currently `internal`. The headless runner resolves it from DI and drives the loop:

```csharp
// In RootCommandHandler — headless branch
if (headless)
{
    var engine = services.GetRequiredService<IWorkflowEngine>();
    var renderer = services.GetRequiredService<IOutputRenderer>();
    var failureHandler = services.GetRequiredService<IFailureHandler>();
    var prompt = parseResult.GetValue(GlobalOptions.Prompt);

    // Initialize from codebase state assessment
    var moduleName = await ResolveModuleNameAsync(services, cancellationToken);
    await engine.InitializeAsync(moduleName, cancellationToken);

    // Main workflow loop
    while (!engine.IsComplete && !cancellationToken.IsCancellationRequested)
    {
        await renderer.RenderProgressAsync(
            engine.CurrentPhase.ToString(),
            engine.CurrentStep.ToString(),
            -1, // indeterminate
            cancellationToken);

        var result = await ExecuteStepAsync(engine, services, prompt, cancellationToken);

        if (!result.Success)
        {
            var classification = failureHandler.RecordFailure(result.TaskId, result.ErrorMessage);
            if (classification.Action == FailureAction.PromptUser)
            {
                // In headless+unattended, exit with code 2
                if (parseResult.GetValue(GlobalOptions.Unattended))
                {
                    await renderer.RenderErrorAsync(classification.Message, cancellationToken: cancellationToken);
                    return ExitCodes.UserInterventionRequired;
                }
                // In headless (not unattended), prompt still returns null
                // so we also exit with code 2
                return ExitCodes.UserInterventionRequired;
            }
            // Self-correct: continue loop
            continue;
        }

        // Fire the appropriate trigger to advance the state machine
        var triggers = engine.GetPermittedTriggers();
        if (triggers.Count > 0)
            engine.Fire(triggers[0]);
    }

    return ExitCodes.Success;
}
```

### Output to stdout/stderr

The `HeadlessRenderer` is already registered as the default `IOutputRenderer` via `TryAddSingleton` in `AddLopenCore()`. When TUI is not active, it remains the active renderer. Its output format:

```
[RequirementGathering] DraftSpecification
[Planning] DetermineDependencies (25%)
[Planning] IdentifyComponents (50%)
[Building] IterateThroughTasks (75%)
Error: Build failed for component AuthService
  InvalidOperationException: Missing dependency
```

Errors go to stderr. Progress and results go to stdout. This enables `lopen --headless 2>/dev/null` to get clean output and `lopen --headless 1>/dev/null` to see only errors.

### Prompt Injection in Headless Mode

When `--prompt` is provided, the text is injected into the LLM context for the current phase. The flow:

1. CLI parses `--prompt` via `GlobalOptions.Prompt`
2. The headless runner passes the prompt string to `ILlmService` as additional user context
3. For `lopen spec --headless --prompt "Build JWT auth"` — the prompt seeds the requirement-gathering conversation
4. For `lopen build --headless --resume <id> --prompt "Focus on session management"` — the prompt provides guidance for the build phase

If `--headless` is specified without `--prompt` and no active session exists, the existing `ValidateHeadlessPromptAsync` already validates and returns an error message with exit code 1.

### Exit Code Handling

```
┌──────────────────────────────────┐
│ Headless Workflow Exit Codes     │
├──────┬───────────────────────────┤
│  0   │ Workflow completed        │
│  1   │ Unrecoverable error       │
│  2   │ Intervention needed       │
│      │ (failure threshold hit    │
│      │  in headless mode)        │
│ 130  │ SIGINT (Ctrl+C)           │
└──────┴───────────────────────────┘
```

Exit code `2` is only returned when the `IFailureHandler` classifies a failure as `PromptUser` (consecutive failures ≥ threshold). In headless mode, since `PromptAsync` returns `null`, the runner cannot get user guidance and must exit. The existing `ExitCodes.UserInterventionRequired` constant maps to this case. Exception-to-exit-code mapping in the handler's try/catch:

```csharp
catch (OperationCanceledException)
{
    return 130; // SIGINT
}
catch (Exception ex)
{
    await renderer.RenderErrorAsync(ex.Message, ex, cancellationToken);
    return ExitCodes.Failure;
}
```

### Relevance to Lopen

The headless runner is the minimal orchestration loop that connects `IWorkflowEngine.Fire()` to `ILlmService` invocations, using `IOutputRenderer` for all output. It does not need a separate orchestrator class — the `RootCommandHandler` (or a small `HeadlessWorkflowRunner` helper) can host the loop directly, keeping the CLI thin. The `IPhaseTransitionController` handles the spec→planning human gate; in headless mode, the spec phase auto-approves when the LLM signals completion.

---

## 10. Phase Command Wiring

### Problem

The `spec`, `plan`, and `build` commands in `PhaseCommands.cs` currently print "Workflow engine not yet wired to CLI." They need to resolve the workflow engine from DI and invoke specific phases rather than the full workflow.

### Resolving Services from DI

The `IServiceProvider` is already passed to each `Create*` method via closure from `Program.cs`:

```csharp
// Current: services is available in the closure
rootCommand.Add(PhaseCommands.CreateSpec(host.Services));
```

Services are resolved with standard DI patterns:

```csharp
var engine = services.GetRequiredService<IWorkflowEngine>();
var renderer = services.GetRequiredService<IOutputRenderer>();
var sessionManager = services.GetRequiredService<ISessionManager>();
var transitionController = services.GetRequiredService<IPhaseTransitionController>();
```

The `IWorkflowEngine` is currently registered as `internal` in `AddLopenCore()`. To resolve it from the CLI, either:
1. **Make registration public** — change `WorkflowEngine` to `public` or register the interface explicitly
2. **Add a factory method** — `AddLopenCore()` already adds `IWorkflowEngine` to DI; ensure the interface is resolvable

Option 1 is preferred since `IWorkflowEngine` is already a public interface.

### Invoking Specific Phases

Each phase command initializes the engine and runs only the steps belonging to that phase. The `WorkflowPhase` enum and `WorkflowEngine.MapStepToPhase()` provide the mapping:

| Command | Phase | Steps | Triggers to Fire |
| --- | --- | --- | --- |
| `spec` | `RequirementGathering` | `DraftSpecification` | `SpecApproved` |
| `plan` | `Planning` | `DetermineDependencies` → `IdentifyComponents` → `SelectNextComponent` → `BreakIntoTasks` | `DependenciesDetermined`, `ComponentsIdentified`, `ComponentSelected`, `TasksBrokenDown` |
| `build` | `Building` | `IterateThroughTasks` → `Repeat` (loop) | `TaskIterationComplete`, `ComponentComplete`, `ModuleComplete` |

Implementation pattern for each phase command:

```csharp
// spec command — replace the stub
spec.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    // ... existing validation (headless prompt, session resolution) ...

    var engine = services.GetRequiredService<IWorkflowEngine>();
    var renderer = services.GetRequiredService<IOutputRenderer>();
    var moduleName = await ResolveModuleNameAsync(services, cancellationToken);

    await engine.InitializeAsync(moduleName, cancellationToken);

    // Run only the RequirementGathering phase
    while (engine.CurrentPhase == WorkflowPhase.RequirementGathering
           && !cancellationToken.IsCancellationRequested)
    {
        await renderer.RenderProgressAsync("Spec", engine.CurrentStep.ToString(), -1, cancellationToken);

        var stepResult = await ExecuteCurrentStepAsync(engine, services, parseResult, cancellationToken);
        if (!stepResult.Success)
        {
            await renderer.RenderErrorAsync(stepResult.ErrorMessage, cancellationToken: cancellationToken);
            return ExitCodes.Failure;
        }

        // Fire the trigger to advance
        var triggers = engine.GetPermittedTriggers();
        if (triggers.Count > 0)
            engine.Fire(triggers[0]);
    }

    await renderer.RenderResultAsync("Specification phase complete.", cancellationToken);
    return ExitCodes.Success;
});
```

For `plan` and `build`, the same pattern applies but the `while` loop condition checks `engine.CurrentPhase == WorkflowPhase.Planning` or `WorkflowPhase.Building` respectively.

### Precondition Checking

The existing validation helpers in `PhaseCommands` already handle preconditions:

| Command | Precondition | Validator | Error Message |
| --- | --- | --- | --- |
| `spec` | None (creates spec) | — | — |
| `plan` | Specification must exist | `ValidateSpecExistsAsync` | "No specification found for module '{module}'. Run 'lopen spec' first." |
| `build` | Spec + Plan must exist | `ValidateSpecExistsAsync` + `ValidatePlanExistsAsync` | "No plan found for module '{module}'. Run 'lopen plan' first." |

These validators are already called in the correct order in the existing stubs:
- `CreatePlan` calls `ValidateSpecExistsAsync` before proceeding
- `CreateBuild` calls both `ValidateSpecExistsAsync` and `ValidatePlanExistsAsync`

The validators resolve `ISessionManager` and `IModuleScanner` from DI to check for spec/plan artifacts on disk. They return `null` on success or an error string on failure, which the handler writes to stderr and returns `ExitCodes.Failure`.

### Phase Transition Controller Integration

The `IPhaseTransitionController` manages transitions between phases:

```csharp
// spec → plan transition (human-gated)
var controller = services.GetRequiredService<IPhaseTransitionController>();

// In headless mode, auto-approve when LLM signals spec is complete
controller.ApproveSpecification();

// plan → build transition (automatic)
if (controller.CanAutoTransitionToBuilding(hasComponents, hasTasks))
{
    // Proceed to building
}

// build → complete transition (automatic)
if (controller.CanAutoTransitionToComplete(allBuilt, allACsPassed))
{
    // Mark module complete
}
```

### TUI vs Headless Branching

When a phase command runs without `--headless`, it should launch the TUI scoped to that phase. The branching follows the same pattern as `RootCommandHandler`:

```csharp
var headless = parseResult.GetValue(GlobalOptions.Headless);
if (headless)
{
    // Drive workflow engine directly with IOutputRenderer
    // ... (headless loop as shown above)
}
else
{
    // Launch TUI with phase scope
    var app = services.GetRequiredService<ITuiApplication>();
    await app.RunAsync(cancellationToken);
}
```

### Relevance to Lopen

The phase commands are thin wrappers that: (1) validate preconditions using the existing helpers, (2) resolve `IWorkflowEngine` from DI, (3) run a `while` loop bounded by `engine.CurrentPhase`, and (4) delegate output to `IOutputRenderer`. No new abstractions are needed — the existing `IWorkflowEngine`, `IPhaseTransitionController`, and `IOutputRenderer` interfaces provide everything required.

---

## 11. `lopen test tui` Command (TUI-42)

### Problem

The TUI specification requires a `lopen test tui` command that launches an interactive component gallery for browsing and previewing all registered TUI components with mock data. This is a development/testing command not listed in the CLI spec's command structure — it is owned by the TUI module but wired through the CLI.

### Existing Infrastructure

The gallery infrastructure is already built:

| Component | Location | Status |
| --- | --- | --- |
| `IComponentGallery` | `Lopen.Tui` | ✅ Interface with `Register()`, `GetAll()`, `GetByName()` |
| `ComponentGallery` | `Lopen.Tui` | ✅ In-memory registry implementation |
| `GalleryListComponent` | `Lopen.Tui` | ✅ Renders selectable list with `▶` marker; `FromGallery()` factory |
| `IPreviewableComponent` | `Lopen.Tui` | ✅ Optional interface with `RenderPreview(width, height) → string[]` |
| `ITuiComponent` | `Lopen.Tui` | ✅ All 14 components have `Name` and `Description` |
| `AddLopenTui()` | `Lopen.Tui` | ✅ Registers all 14 components in the gallery singleton |

### Command Structure

The command is nested under a `test` parent command: `lopen test tui`. This follows the convention of grouping development/debugging commands under `test`:

```csharp
// In Program.cs — add after existing command registrations
var testCommand = new Command("test", "Development and testing commands");
testCommand.Add(TestTuiCommand.Create(host.Services));
rootCommand.Add(testCommand);
```

### Command Implementation

```csharp
// src/Lopen/Commands/TestTuiCommand.cs
using System.CommandLine;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

public static class TestTuiCommand
{
    public static Command Create(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var command = new Command("tui", "Launch the TUI component gallery");

        var componentArg = new Argument<string?>("component")
        {
            Description = "Component name to preview directly (skips gallery list)",
            Arity = ArgumentArity.ZeroOrOne
        };
        command.Arguments.Add(componentArg);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                var gallery = services.GetRequiredService<IComponentGallery>();
                var components = gallery.GetAll();

                if (components.Count == 0)
                {
                    await stderr.WriteLineAsync("No components registered in gallery.");
                    return ExitCodes.Failure;
                }

                var targetName = parseResult.GetValue(componentArg);
                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    var component = gallery.GetByName(targetName);
                    if (component is null)
                    {
                        await stderr.WriteLineAsync(
                            $"Component '{targetName}' not found. Available: {string.Join(", ", components.Select(c => c.Name))}");
                        return ExitCodes.Failure;
                    }
                }

                // Launch the gallery app (fullscreen TUI)
                var galleryApp = new ComponentGalleryApp(gallery);
                await galleryApp.RunAsync(cancellationToken);

                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return ExitCodes.Failure;
            }
        });

        return command;
    }
}
```

### ComponentGalleryApp

The gallery app is a self-contained fullscreen TUI (separate from the main workflow TUI) that uses the same rendering infrastructure. The TUI RESEARCH.md (section 14) provides the reference implementation using `Terminal.Create(new FullscreenMode())` and a render loop with:

- **Left pane** (1/3 width): `GalleryListComponent` showing all registered components with selection
- **Right pane** (2/3 width): Preview of the selected component via `IPreviewableComponent.RenderPreview()`
- **Navigation**: ↑↓ to select, Q/Esc to quit
- **Divider**: Vertical `│` between panes
- **Footer**: Keyboard hint bar

Components that don't implement `IPreviewableComponent` show name + description + "(No preview available)".

### Where ComponentGalleryApp Lives

Since the gallery app is a TUI concern, `ComponentGalleryApp` should live in `Lopen.Tui`:

```
src/Lopen.Tui/
├── ComponentGallery.cs          # Registry (exists)
├── ComponentGalleryApp.cs       # Gallery launcher (new)
├── GalleryListComponent.cs      # List renderer (exists)
└── IComponentGallery.cs         # Interface (exists)
```

The CLI command (`TestTuiCommand`) stays in `Lopen/Commands/` and resolves `IComponentGallery` from DI to construct the app.

### Incremental Gallery Features

Per the TUI research, the gallery evolves through versions:

1. **v1**: List + preview pane with `IPreviewableComponent`. Arrow key navigation, Q to quit.
2. **v2**: Multiple stub scenarios per component (empty, in progress, error, loading).
3. **v3**: Live resize testing — gallery re-renders as terminal is resized.
4. **v4**: Side-by-side comparison — show two components simultaneously.

v1 is the minimum viable implementation for TUI-42.

### Registration in Program.cs

```csharp
// Program.cs — after existing commands
var testCommand = new Command("test", "Development and testing commands");
testCommand.Add(TestTuiCommand.Create(host.Services));
rootCommand.Add(testCommand);
```

This adds the command tree:
```
lopen
├── test
│   └── tui [component]    # Launch component gallery
├── spec
├── plan
├── build
└── ...
```

### Relevance to Lopen

The `lopen test tui` command is a thin CLI wrapper around the existing `IComponentGallery` infrastructure. All gallery logic lives in `Lopen.Tui`; the CLI only provides the command entry point and DI resolution. The command follows the established pattern of `PhaseCommands.Create*` — a static factory method that captures `IServiceProvider` via closure. The optional `component` argument enables direct preview of a specific component for quick iteration during development.

---

## 10. CLI-26: Project Root Discovery

> **Date:** 2026-07-25
> **Ticket:** CLI-26
> **Goal:** Automatically discover the project root at startup so `AddLopenCore(projectRoot)` and `AddLopenStorage(projectRoot)` receive a valid path.

### Current State

Currently, `Program.cs` calls both registration methods with **no arguments**:

```csharp
builder.Services.AddLopenCore();   // projectRoot defaults to null
builder.Services.AddLopenStorage(); // projectRoot defaults to null
```

When `projectRoot` is `null`, critical services are **not registered**: `IGitService`, `IWorkflowOrchestrator`, `IToolHandlerBinder`, `IModuleScanner`, `ISessionManager`, `IAutoSaveService`, `IPlanManager`, `ISectionCache`, and `IAssessmentCache`. This causes runtime failures when any command requires project context (e.g., `"Workflow engine not available. Ensure project root is configured."` in `RootCommandHandler.RunHeadlessAsync`).

The **only** existing upward-walk logic is `LopenConfigurationBuilder.DiscoverProjectConfigPath(string startDirectory)` in `Lopen.Configuration`, which searches for `.lopen/config.json` specifically — not for `.lopen/` or `.git/` as directory markers.

### Algorithm Design

The project root discovery should walk up the directory tree from CWD, checking for marker directories in priority order:

1. **`.lopen/`** — First priority. If present, this directory *is* the project root. This is the strongest signal because the user has explicitly initialized Lopen here.
2. **`.git/`** — Second priority. Most projects will have a git root but may not yet have `.lopen/`. Using `.git/` as a fallback ensures Lopen works on first run in any git repository.

The walk terminates at the filesystem root. If neither marker is found, `projectRoot` remains `null` and services degrade gracefully (current behavior).

```csharp
// Proposed: src/Lopen/ProjectRootDiscovery.cs
namespace Lopen;

/// <summary>
/// Walks up the directory tree from a starting directory to locate the project root.
/// Checks for <c>.lopen/</c> first, then <c>.git/</c> as fallback markers.
/// </summary>
internal static class ProjectRootDiscovery
{
    /// <summary>
    /// Discovers the project root by walking up from <paramref name="startDirectory"/>.
    /// Returns <c>null</c> if no marker directory is found.
    /// </summary>
    internal static string? FindProjectRoot(string startDirectory)
    {
        // First pass: look for .lopen/ (strongest signal)
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".lopen")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // Second pass: fall back to .git/
        dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}
```

### Integration with Program.cs

The discovery runs **before** DI registration, since `AddLopenCore` and `AddLopenStorage` need the path at registration time:

```csharp
// Program.cs — updated
var builder = Host.CreateApplicationBuilder(args);

var projectRoot = ProjectRootDiscovery.FindProjectRoot(Directory.GetCurrentDirectory());

builder.Services.AddLopenConfiguration();
builder.Services.AddLopenAuth();
builder.Services.AddLopenCore(projectRoot);
builder.Services.AddLopenStorage(projectRoot);
builder.Services.AddLopenLlm();
builder.Services.AddLopenTui();
builder.Services.UseRealTui();
builder.Services.AddTopPanelDataProvider();
builder.Services.AddLopenOtel(builder.Configuration);

using var host = builder.Build();
// ... rest unchanged
```

This is safe because `AddLopenCore` and `AddLopenStorage` already handle `null` gracefully — they simply skip project-aware registrations.

### Edge Cases

| Scenario | Behavior |
|---|---|
| **No `.lopen/` or `.git/` found** | `FindProjectRoot` returns `null`. Services register without project context. Commands requiring project context fail with clear error messages (existing behavior). |
| **Running from home directory** | Walk reaches filesystem root without finding markers. Returns `null`. No change from current behavior. |
| **Symlinks in path** | `DirectoryInfo` follows symlinks by default on .NET. `dir.FullName` resolves to the physical path. This is correct — the resolved path should be used for consistent storage paths. |
| **`.lopen/` and `.git/` at different levels** | `.lopen/` wins because it's checked first in a separate pass. If `.lopen/` is in a subdirectory of the `.git/` root, it correctly identifies the Lopen project root as distinct from the git root. |
| **Nested git repositories (submodules)** | The walk finds the *nearest* `.git/` to CWD, which is the correct behavior for submodules. |
| **Mounted/network filesystems** | `DirectoryInfo.Parent` handles mount boundaries correctly. Performance is bounded by directory depth (typically < 20 hops). |

### Relationship to DiscoverProjectConfigPath

The existing `LopenConfigurationBuilder.DiscoverProjectConfigPath` searches for `.lopen/config.json` (a *file*), not `.lopen/` (a *directory*). These are complementary:

- `ProjectRootDiscovery.FindProjectRoot` — finds the project root for DI registration. Runs once at startup.
- `DiscoverProjectConfigPath` — finds a specific config file for the configuration builder. Already called inside `AddLopenConfiguration()`.

The two-pass approach (`.lopen/` first, then `.git/`) avoids combining them into a single walk, keeping the logic simple and the priority order explicit.

### Testing Strategy

- Unit tests with a temporary directory tree containing various marker combinations.
- Test the `null` case (no markers found).
- Test nested markers (`.lopen/` in subdirectory of `.git/` root).
- No integration test needed — the function is pure directory traversal with no side effects.

---

## 11. CLI-27: `--no-welcome` Global Option

> **Date:** 2026-07-25
> **Ticket:** CLI-27
> **Goal:** Add a `--no-welcome` global option that suppresses the TUI landing page modal on startup.

### Current State

`TuiApplication` already accepts a `bool showLandingPage = true` constructor parameter (line 99 of `TuiApplication.cs`) and the comment at line 139 explicitly references `--no-welcome`:

```csharp
// Show landing page on startup (unless disabled via --no-welcome)
if (_showLandingPage)
    _modalState = TuiModalState.LandingPage;
else
    await CheckForActiveSessionAsync(ct).ConfigureAwait(false);
```

The wiring is **not yet connected** — there is no CLI option, and `showLandingPage` always defaults to `true` because the DI container uses `ActivatorUtilities` which passes `true` for the optional parameter.

### Implementation Plan

#### Step 1: Add Global Option to `GlobalOptions.cs`

Follow the exact pattern used by `--headless`, `--no-resume`, etc.:

```csharp
// In GlobalOptions.cs — add after NoResume
/// <summary>Suppresses the TUI landing page on startup.</summary>
public static Option<bool> NoWelcome { get; } = new("--no-welcome")
{
    Description = "Skip the TUI landing page on startup",
    Recursive = true,
};
```

Register in `AddTo`:

```csharp
public static void AddTo(RootCommand root)
{
    root.Options.Add(Headless);
    root.Options.Add(Prompt);
    root.Options.Add(Resume);
    root.Options.Add(NoResume);
    root.Options.Add(NoWelcome); // <-- add
}
```

#### Step 2: Pass Through DI to TuiApplication

The challenge is that `TuiApplication` is registered as a singleton at DI setup time (in `UseRealTui()`), but the `--no-welcome` flag value is only available at command parse time. Two approaches:

**Option A: Wrapper options class (recommended)**

Create a simple options record that DI can resolve, and populate it from the parse result before resolving `ITuiApplication`:

```csharp
// In Lopen.Tui or Lopen (CLI module)
namespace Lopen.Tui;

/// <summary>
/// Runtime options for TUI behavior, populated from CLI parse results.
/// </summary>
public sealed class TuiOptions
{
    /// <summary>Whether to show the landing page on startup.</summary>
    public bool ShowLandingPage { get; set; } = true;
}
```

Register in `Program.cs` (or `AddLopenTui`):

```csharp
builder.Services.AddSingleton<TuiOptions>();
```

Then update `TuiApplication` to consume it:

```csharp
public TuiApplication(
    TopPanelComponent topPanel,
    ActivityPanelComponent activityPanel,
    ContextPanelComponent contextPanel,
    PromptAreaComponent promptArea,
    KeyboardHandler keyboardHandler,
    ILogger<TuiApplication> logger,
    TuiOptions tuiOptions,               // <-- new required param
    // ... optional params unchanged
    ISessionDetector? sessionDetector = null)
{
    // ...
    _showLandingPage = tuiOptions.ShowLandingPage;
}
```

Set the flag in `RootCommandHandler` before launching TUI:

```csharp
// In RootCommandHandler.Configure, inside the TUI branch:
var noWelcome = parseResult.GetValue(GlobalOptions.NoWelcome);
var tuiOptions = services.GetRequiredService<TuiOptions>();
tuiOptions.ShowLandingPage = !noWelcome;

var app = services.GetRequiredService<ITuiApplication>();
var prompt = parseResult.GetValue(GlobalOptions.Prompt);
await app.RunAsync(prompt, cancellationToken);
```

**Option B: Direct parameter injection via factory (simpler but less extensible)**

Modify `UseRealTui` to accept the flag as a factory parameter. This is less clean because it couples DI registration to a CLI concern.

**Recommendation:** Option A is preferred because:
- `TuiOptions` is extensible for future TUI flags.
- It follows the established pattern of `WorkflowOptions` already used in Lopen.Core.
- The singleton is mutable only during startup, before `RunAsync` is called.
- It keeps the `Lopen.Tui` module decoupled from `System.CommandLine`.

#### Step 3: Handle in Subcommands

Any subcommand that launches the TUI (e.g., `spec`, `plan`, `build`) should also respect `--no-welcome`. Since `GlobalOptions` are recursive, the flag is available in all subcommand parse results. Each command handler that calls `app.RunAsync()` should set `TuiOptions.ShowLandingPage` from the parse result.

A helper method can reduce duplication:

```csharp
// In a shared location (e.g., GlobalOptions or a new CommandHelpers class)
internal static void ApplyTuiOptions(IServiceProvider services, ParseResult parseResult)
{
    var tuiOptions = services.GetRequiredService<TuiOptions>();
    tuiOptions.ShowLandingPage = !parseResult.GetValue(GlobalOptions.NoWelcome);
}
```

### Interaction with `--headless`

When `--headless` is active, the TUI is never launched, so `--no-welcome` is irrelevant. No special handling needed — the flag is simply unused in headless mode.

### Testing Strategy

- **Unit test `GlobalOptions`:** Verify `NoWelcome` is registered and parseable.
- **Unit test `TuiOptions`:** Verify default is `ShowLandingPage = true`.
- **Unit test `RootCommandHandler`:** Mock `ITuiApplication`, parse `--no-welcome`, verify `TuiOptions.ShowLandingPage` is `false` before `RunAsync` is called.
- **Unit test `TuiApplication`:** Verify that when `TuiOptions.ShowLandingPage` is `false`, `_modalState` is not set to `LandingPage` on startup. This is already partially covered by existing tests that construct `TuiApplication` with `showLandingPage: false`.
