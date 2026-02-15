# Research: CLI Module — .NET CLI Implementation

> **Date:** 2026-02-15
> **Sources:** [dotnet/command-line-api](https://github.com/dotnet/command-line-api), [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console), Microsoft .NET documentation, CLI specification

---

## 1. System.CommandLine

### Package & Version

| Property       | Value                              |
| -------------- | ---------------------------------- |
| NuGet Package  | `System.CommandLine`               |
| Latest Version | `2.0.0-beta4.22272.1` (prerelease) |
| Status         | **Beta** — no GA release exists    |
| Repository     | `dotnet/command-line-api`          |

Despite the beta status, `System.CommandLine` is the official Microsoft CLI parsing library and is widely used in production .NET tools. The API surface is stable enough for adoption with the caveat of potential breaking changes on upgrades.

### Installation

```sh
dotnet add package System.CommandLine --prerelease
```

### Basic Usage

```csharp
using System.CommandLine;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI agent");

var headlessOption = new Option<bool>("--headless", "Run without TUI");
headlessOption.AddAlias("-q");

rootCommand.AddOption(headlessOption);

rootCommand.SetHandler((bool headless) =>
{
    // Entry point logic
}, headlessOption);

return await rootCommand.InvokeAsync(args);
```

### Handler Model

Handlers bind parsed options/arguments to method parameters via `SetHandler`. For complex scenarios, use `InvocationContext`:

```csharp
command.SetHandler((InvocationContext context) =>
{
    var model = context.ParseResult.GetValueForOption(modelOption);
    var ct = context.GetCancellationToken(); // supports Ctrl+C
    context.ExitCode = 0;
});
```

### Relevance to Lopen

System.CommandLine maps directly to the CLI specification's command structure. It supports subcommands, global options, aliases, help generation, and exit codes out of the box. The `InvocationContext` provides `CancellationToken` which is essential for TUI cleanup on Ctrl+C.

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
│   ├── Lopen.Cli/                 # CLI entry point (executable)
│   ├── Lopen.Core/                # Workflow orchestration
│   ├── Lopen.Llm/                 # Copilot SDK integration
│   ├── Lopen.Storage/             # Session persistence
│   ├── Lopen.Configuration/       # Settings resolution
│   ├── Lopen.Auth/                # GitHub authentication
│   ├── Lopen.Tui/                 # Terminal UI (Spectre.Console)
│   └── Lopen.Otel/                # OpenTelemetry integration
├── tests/
│   ├── Lopen.Cli.Tests/
│   ├── Lopen.Core.Tests/
│   └── ...
└── docs/
```

### Key Conventions

- **Thin CLI, fat libraries** — `Lopen.Cli` is command definitions + DI wiring only. All logic lives in library projects.
- **1:1 module mapping** — Each `docs/requirements/<module>/` maps to a `src/Lopen.<Module>/` project.
- **Central Package Management** — `Directory.Packages.props` at root for NuGet version consistency.
- **`Microsoft.NET.Sdk`** — Use the standard SDK (not `Microsoft.NET.Sdk.Web`) for CLI apps.

### Project File

```xml
<!-- src/Lopen.Cli/Lopen.Cli.csproj -->
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
src/Lopen.Cli/
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
└── Lopen.Cli.csproj
```

### Relevance to Lopen

The project-per-module approach mirrors the existing `docs/requirements/` structure. The CLI project stays thin — it only wires up commands and DI, delegating all behavior to module libraries. This enables independent testing of each module and potential future reuse.

---

## 3. Subcommand Pattern

### Adding Subcommands

```csharp
var rootCommand = new RootCommand("Lopen");

// Phase commands (top-level)
rootCommand.AddCommand(new Command("spec", "Run requirement gathering"));
rootCommand.AddCommand(new Command("plan", "Run planning phase"));
rootCommand.AddCommand(new Command("build", "Run building phase"));
rootCommand.AddCommand(new Command("revert", "Rollback to last good commit"));
```

### Nested Subcommands

For `auth` and `session` which have their own sub-hierarchy:

```csharp
// auth login | auth status | auth logout
var authCommand = new Command("auth", "Authentication management");
authCommand.AddCommand(new Command("login", "Authenticate with GitHub"));
authCommand.AddCommand(new Command("status", "Check authentication state"));
authCommand.AddCommand(new Command("logout", "Clear credentials"));
rootCommand.AddCommand(authCommand);

// session list | session show | session resume | session delete | session prune
var sessionCommand = new Command("session", "Session management");
sessionCommand.AddCommand(new Command("list", "List all sessions"));
sessionCommand.AddCommand(new Command("show", "Show session details"));
sessionCommand.AddCommand(new Command("resume", "Resume a session"));
sessionCommand.AddCommand(new Command("delete", "Delete a session"));
sessionCommand.AddCommand(new Command("prune", "Remove old sessions"));
rootCommand.AddCommand(sessionCommand);

// config show
var configCommand = new Command("config", "Configuration inspection");
configCommand.AddCommand(new Command("show", "Display resolved config"));
rootCommand.AddCommand(configCommand);
```

### Command-Specific Options

Options can be scoped to specific commands. For example, `session show --format`:

```csharp
var showCommand = new Command("show", "Show session details");
var formatOption = new Option<string>("--format", "Output format (md, json, yaml)");
formatOption.SetDefaultValue("md");
showCommand.AddOption(formatOption);

var sessionIdArg = new Argument<string?>("session-id", "Session ID (latest if omitted)");
sessionIdArg.SetDefaultValue(null);
showCommand.AddArgument(sessionIdArg);
```

### Relevance to Lopen

Every command in the CLI specification maps to a `Command` instance. Nested commands (`auth login`, `session list`, `config show`) use `AddCommand()` on parent commands. This gives automatic `--help` generation at every level.

---

## 4. Global Flags

### AddGlobalOption

`RootCommand.AddGlobalOption()` makes an option available to **all** subcommands automatically:

```csharp
var rootCommand = new RootCommand("Lopen");

// Global options from CLI specification
var headlessOption = new Option<bool>("--headless", "Run without TUI; output to stdout");
headlessOption.AddAlias("-q");
rootCommand.AddGlobalOption(headlessOption);

var quietOption = new Option<bool>("--quiet", "Alias for --headless");
rootCommand.AddGlobalOption(quietOption);

var promptOption = new Option<string?>("--prompt", "Inject instructions for the LLM");
promptOption.AddAlias("-p");
rootCommand.AddGlobalOption(promptOption);

var modelOption = new Option<string?>("--model", "Override model for all phases");
rootCommand.AddGlobalOption(modelOption);

var unattendedOption = new Option<bool>("--unattended", "Suppress intervention prompts");
rootCommand.AddGlobalOption(unattendedOption);

var resumeOption = new Option<string?>("--resume", "Resume a specific session");
rootCommand.AddGlobalOption(resumeOption);

var noResumeOption = new Option<bool>("--no-resume", "Force a new session");
rootCommand.AddGlobalOption(noResumeOption);

var maxIterationsOption = new Option<int?>("--max-iterations", "Max loop iterations before pausing");
rootCommand.AddGlobalOption(maxIterationsOption);
```

### Accessing Global Options in Handlers

```csharp
specCommand.SetHandler((InvocationContext context) =>
{
    bool headless = context.ParseResult.GetValueForOption(headlessOption);
    string? prompt = context.ParseResult.GetValueForOption(promptOption);
    string? model = context.ParseResult.GetValueForOption(modelOption);

    // All global options are accessible from any subcommand handler
});
```

### Applicability Constraints

The specification notes that some global flags don't apply to all commands (e.g., `--prompt` is not applicable to `auth`). System.CommandLine does not enforce this — validation must be done in the handler or via middleware:

```csharp
// Option: validate in handler
authLoginCommand.SetHandler((InvocationContext ctx) =>
{
    if (ctx.ParseResult.GetValueForOption(promptOption) is not null)
    {
        ctx.Console.Error.Write("--prompt is not applicable to auth commands");
        ctx.ExitCode = 1;
        return;
    }
});
```

### Relevance to Lopen

All global flags from the specification (`--headless`, `--quiet`, `--prompt`, `--model`, `--unattended`, `--resume`, `--no-resume`, `--max-iterations`) map to `AddGlobalOption()`. Applicability constraints (e.g., `--prompt` not valid for `auth`) need explicit validation.

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
// Via InvocationContext (preferred)
command.SetHandler((InvocationContext context) =>
{
    context.ExitCode = 0; // or 1, or 2
});

// Via return value from Main
static async Task<int> Main(string[] args)
{
    var root = BuildRootCommand();
    return await root.InvokeAsync(args);
    // InvokeAsync returns the handler's ExitCode
}
```

### Exception-to-Exit-Code Mapping

```csharp
command.SetHandler(async (InvocationContext context) =>
{
    try
    {
        await RunWorkflowAsync(context);
        context.ExitCode = 0;
    }
    catch (UserInterventionRequiredException)
    {
        context.ExitCode = 2;
    }
    catch (OperationCanceledException)
    {
        context.ExitCode = 130;
    }
    catch (Exception)
    {
        context.ExitCode = 1;
    }
});
```

### Relevance to Lopen

Use `return await root.InvokeAsync(args)` from `Main` to propagate exit codes. Map domain exceptions to exit codes in each handler. The `ExitCode = 2` case only applies in headless + unattended mode when the failure threshold is reached.

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
| `System.CommandLine`            | CLI parsing, subcommands, options, help           | `2.0.0-beta4` (prerelease) |
| `System.CommandLine.Hosting`    | Bridges System.CommandLine ↔ Generic Host DI      | `0.4.0-alpha` (prerelease) |
| `Microsoft.Extensions.Hosting`  | DI container, configuration, logging              | Stable          |
| `Microsoft.Extensions.Options`  | Strongly-typed configuration binding              | Stable          |

### TUI Packages

| Package                    | Purpose                                    | Version Note |
| -------------------------- | ------------------------------------------ | ------------ |
| `Spectre.Console`          | Rich terminal rendering (tables, progress) | Stable       |
| `Spectre.Console.Json`     | JSON syntax highlighting in terminal       | Stable       |

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

The `System.CommandLine` + `System.CommandLine.Hosting` combination provides CLI parsing with full DI support. Spectre.Console handles TUI rendering only (not CLI parsing). The `Microsoft.Extensions.Hosting` stack provides the DI container, configuration, and logging infrastructure that all modules plug into.

---

## 8. Implementation Approach

### Step 1: Scaffold the Solution

```sh
# Create solution and projects
dotnet new sln -n lopen
dotnet new console -n Lopen.Cli -o src/Lopen.Cli
dotnet new classlib -n Lopen.Core -o src/Lopen.Core

dotnet sln add src/Lopen.Cli
dotnet sln add src/Lopen.Core
dotnet add src/Lopen.Cli reference src/Lopen.Core
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
    <PackageVersion Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageVersion Include="System.CommandLine.Hosting" Version="0.4.0-alpha.22272.1" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0-preview.4.25258.110" />
    <PackageVersion Include="Spectre.Console" Version="0.50.0" />
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
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Hosting;

var runner = new CommandLineBuilder(new LopenRootCommand())
    .UseHost(_ => Host.CreateDefaultBuilder(args), host =>
    {
        host.ConfigureServices((context, services) =>
        {
            // Register module services
        });
    })
    .UseDefaults()
    .Build();

return await runner.InvokeAsync(args);
```

### Step 6: Command Registration Pattern

Each command is a self-contained class:

```csharp
public class LopenRootCommand : RootCommand
{
    public LopenRootCommand() : base("Lopen - GitHub Copilot workflow agent")
    {
        // Global options
        AddGlobalOption(GlobalOptions.Headless);
        AddGlobalOption(GlobalOptions.Prompt);
        AddGlobalOption(GlobalOptions.Model);
        AddGlobalOption(GlobalOptions.Unattended);
        AddGlobalOption(GlobalOptions.Resume);
        AddGlobalOption(GlobalOptions.NoResume);
        AddGlobalOption(GlobalOptions.MaxIterations);

        // Phase commands
        AddCommand(new SpecCommand());
        AddCommand(new PlanCommand());
        AddCommand(new BuildCommand());

        // Utility commands
        AddCommand(new AuthCommand());
        AddCommand(new SessionCommand());
        AddCommand(new RevertCommand());
        AddCommand(new ConfigCommand());
    }
}
```

### Build Order

1. **Lopen.Cli** — Command definitions, `Program.cs`, DI wiring (this module)
2. **Lopen.Core** — Workflow orchestration (depends on specification)
3. **Lopen.Auth** — Authentication (early dependency — needed for LLM calls)
4. **Lopen.Configuration** — Settings resolution
5. **Lopen.Storage** — Session persistence
6. **Lopen.Llm** — Copilot SDK integration
7. **Lopen.Tui** — Terminal UI rendering
8. **Lopen.Otel** — Telemetry (can be added at any point)

### Relevance to Lopen

Start with the CLI skeleton and Core library. The CLI module is the entry point but should be implemented early because it defines the DI container and command structure that all other modules plug into. Each module registers its services via `IServiceCollection` extensions, keeping the CLI project thin.
