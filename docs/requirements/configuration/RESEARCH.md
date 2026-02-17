# Research: Layered Configuration in .NET for CLI Tools

> **Last validated:** February 2026
>
> **Date:** 2025-07-24
> **Sources:** [Microsoft.Extensions.Configuration docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration), [NuGet (10.0.3)](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json), [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)

---

## 1. Layered Configuration in .NET

`Microsoft.Extensions.Configuration` provides a provider-based system where multiple sources are composed into a single `IConfiguration` view. **Sources added later override earlier ones** for the same key — this "last wins" behavior is the foundation of Lopen's priority hierarchy.

```csharp
// Lopen uses Host.CreateApplicationBuilder for full hosting, then layers
// configuration via LopenConfigurationBuilder which internally uses:
var config = new ConfigurationBuilder()
    .AddJsonFile(globalConfigPath, optional: true)      // lowest priority
    .AddJsonFile(projectConfigPath, optional: true)     // overrides global
    .AddEnvironmentVariables("LOPEN_")                  // overrides JSON
    .AddInMemoryCollection(cliOverrides)                // CLI flags win
    .Build();
```

The resulting `IConfigurationRoot` merges all providers. When reading `config["models:planning"]`, the value comes from the **last provider that contains that key**.

### Key Concepts

| Concept | Description |
|---|---|
| `IConfigurationBuilder` | Accumulates configuration sources in order |
| `IConfigurationRoot` | The built, merged configuration tree |
| `IConfigurationSection` | A subtree accessed via `GetSection("key")` |
| `IConfigurationProvider` | A single source (JSON file, env vars, etc.) |

### Hierarchy Separator

Configuration keys use `:` as the hierarchy delimiter. The JSON path `models.planning` becomes the key `models:planning`. Environment variables use `__` (double underscore) as an alternative separator on Linux.

---

## 2. JSON Configuration Files

The `Microsoft.Extensions.Configuration.Json` package reads JSON files into the configuration tree via `AddJsonFile()`.

### Reading Lopen's Config Files

```csharp
// Resolve paths
var globalConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config", "lopen", "config.json");

var projectConfigPath = FindProjectConfig(Directory.GetCurrentDirectory());

var config = new ConfigurationBuilder()
    .AddJsonFile(globalConfigPath, optional: true, reloadOnChange: false)
    .AddJsonFile(projectConfigPath, optional: true, reloadOnChange: false)
    .Build();
```

### Project Config Discovery

The specification requires walking up the directory tree to find the nearest `.lopen/` directory:

```csharp
static string? FindProjectConfig(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        var configPath = Path.Combine(dir.FullName, ".lopen", "config.json");
        if (File.Exists(configPath))
            return configPath;
        dir = dir.Parent;
    }
    return null;
}
```

### JSON Structure

The JSON provider maps nested objects to `:` delimited keys automatically:

```json
{
  "Models": {
    "Planning": "claude-opus-4.6",
    "Building": "claude-opus-4.6"
  },
  "Budget": {
    "TokenBudgetPerModule": 50000,
    "WarningThreshold": 0.8
  },
  "Git": {
    "AutoCommit": true,
    "Convention": "conventional"
  }
}
```

This produces keys like `Models:Planning`, `Budget:TokenBudgetPerModule`, `Git:AutoCommit`.

### Important Behaviors

- `optional: true` — no error if the file doesn't exist (essential for Lopen's "works with zero config" principle)
- `reloadOnChange: false` — appropriate for CLI tools (no file watcher overhead)
- Property names are **case-insensitive** when binding
- Arrays use index-based keys: `models:0`, `models:1`

---

## 3. Configuration Binding

The `Microsoft.Extensions.Configuration.Binder` package maps configuration sections to strongly-typed C# classes.

### Defining Options Classes

`LopenOptions` uses **nested option classes** to group related settings. Properties use PascalCase and bind via the default case-insensitive binder — no `[ConfigurationKeyName]` attributes are needed.

```csharp
public sealed class LopenOptions
{
    public ModelOptions Models { get; set; } = new();
    public BudgetOptions Budget { get; set; } = new();
    public OracleOptions Oracle { get; set; } = new();
    public WorkflowOptions Workflow { get; set; } = new();
    public SessionOptions Session { get; set; } = new();
    public GitOptions Git { get; set; } = new();
    public ToolDisciplineOptions ToolDiscipline { get; set; } = new();
    public DisplayOptions Display { get; set; } = new();
}

public sealed class ModelOptions
{
    public string RequirementGathering { get; set; } = "claude-opus-4.6";
    public string Planning { get; set; } = "claude-opus-4.6";
    public string Building { get; set; } = "claude-opus-4.6";
    public string Research { get; set; } = "claude-opus-4.6";

    // Per-phase fallback chains for runtime model unavailability (LLM-11).
    public List<string> RequirementGatheringFallbacks { get; set; } = [];
    public List<string> PlanningFallbacks { get; set; } = [];
    public List<string> BuildingFallbacks { get; set; } = [];
    public List<string> ResearchFallbacks { get; set; } = [];

    public string GlobalFallback { get; set; } = "claude-sonnet-4";
}

public sealed class BudgetOptions
{
    public int TokenBudgetPerModule { get; set; }
    public int PremiumRequestBudget { get; set; }
    public double WarningThreshold { get; set; } = 0.8;
    public double ConfirmationThreshold { get; set; } = 0.9;
}

public sealed class OracleOptions
{
    public string Model { get; set; } = "gpt-5-mini";
}

public sealed class WorkflowOptions
{
    public bool Unattended { get; set; }
    public int MaxIterations { get; set; } = 100;
    public int FailureThreshold { get; set; } = 3;
}

public sealed class SessionOptions
{
    public bool AutoResume { get; set; } = true;
    public int SessionRetention { get; set; } = 10;
    public bool SaveIterationHistory { get; set; }
}

public sealed class GitOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutoCommit { get; set; } = true;
    public string Convention { get; set; } = "conventional";
}

public sealed class ToolDisciplineOptions
{
    public int MaxFileReads { get; set; } = 3;
    public int MaxCommandRetries { get; set; } = 3;
}

public sealed class DisplayOptions
{
    public bool ShowTokenUsage { get; set; } = true;
    public bool ShowPremiumCount { get; set; } = true;
}
```

### Two Binding Approaches

```csharp
// Approach 1: .Get<T>() — returns a new populated instance
LopenOptions options = config.Get<LopenOptions>() ?? new LopenOptions();

// Approach 2: .Bind() — populates an existing instance (preserves defaults)
var options = new LopenOptions();
config.Bind(options);
```

**`Bind()` is preferred for Lopen** because it preserves default values in the class when a key is absent from configuration. `Get<T>()` also respects defaults set in property initializers, but `Bind()` makes the intent explicit.

### JSON Property Name Mapping

The configuration binder uses **case-insensitive matching**. Lopen avoids the snake_case binding problem entirely by using **PascalCase keys in JSON configuration files** that match the C# property names directly. No `[ConfigurationKeyName]` attributes are needed.

```json
{
  "Models": {
    "Planning": "claude-opus-4.6",
    "Building": "claude-opus-4.6"
  },
  "Budget": {
    "TokenBudgetPerModule": 50000,
    "WarningThreshold": 0.8
  }
}
```

This approach keeps the options classes clean and relies solely on the default binder behavior.

### Source Generator Alternative

.NET 8+ provides `Microsoft.Extensions.Configuration.Binder` source generators for AOT/trimming compatibility. Enable with:

```xml
<PropertyGroup>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

This replaces reflection-based binding with compile-time generated code — important if Lopen targets Native AOT.

---

## 4. CLI Flag Integration

### The Problem

`System.CommandLine` and `Microsoft.Extensions.Configuration.CommandLine` are **separate, incompatible systems**. There is no built-in bridge. The `CommandLine` configuration provider is a simple key-value parser, not the full argument parser that System.CommandLine provides.

### Solution: In-Memory Collection Overrides

Rather than a custom `IConfigurationProvider`, Lopen uses `AddInMemoryCollection` with a dictionary of overrides. The `LopenConfigurationBuilder` exposes typed helper methods that translate CLI flags into configuration keys:

```csharp
public sealed class LopenConfigurationBuilder
{
    private readonly Dictionary<string, string?> _overrides = new();

    public LopenConfigurationBuilder AddOverride(string key, string value)
    {
        _overrides[key] = value;
        return this;
    }

    public LopenConfigurationBuilder AddModelOverride(string model)
    {
        _overrides["Models:RequirementGathering"] = model;
        _overrides["Models:Planning"] = model;
        _overrides["Models:Building"] = model;
        _overrides["Models:Research"] = model;
        return this;
    }

    public LopenConfigurationBuilder AddUnattendedOverride(bool unattended = true)
    {
        _overrides["Workflow:Unattended"] = unattended.ToString();
        return this;
    }

    public LopenConfigurationBuilder AddResumeOverride(bool autoResume)
    {
        _overrides["Session:AutoResume"] = autoResume.ToString();
        return this;
    }

    public LopenConfigurationBuilder AddMaxIterationsOverride(int maxIterations)
    {
        _overrides["Workflow:MaxIterations"] = maxIterations.ToString();
        return this;
    }

    public (LopenOptions Options, IConfigurationRoot Configuration) Build()
    {
        var configBuilder = new ConfigurationBuilder();

        // Layer 1: Global configuration
        if (_globalConfigPath is not null && File.Exists(_globalConfigPath))
            configBuilder.AddJsonFile(_globalConfigPath, optional: true, reloadOnChange: false);

        // Layer 2: Project configuration
        if (_projectConfigPath is not null && File.Exists(_projectConfigPath))
            configBuilder.AddJsonFile(_projectConfigPath, optional: true, reloadOnChange: false);

        // Layer 3: Environment variables (prefixed with LOPEN_)
        configBuilder.AddEnvironmentVariables("LOPEN_");

        // Layer 4: CLI flag overrides (highest priority)
        if (_overrides.Count > 0)
            configBuilder.AddInMemoryCollection(_overrides);

        var configRoot = configBuilder.Build();
        var options = new LopenOptions();
        configRoot.Bind(options);

        // Validate and throw on errors
        var errors = LopenOptionsValidator.Validate(options);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}")));

        return (options, configRoot);
    }
}
```

### How CLI Flags Reach the Builder

The System.CommandLine handler inspects `ParseResult` and calls the appropriate `Add*Override()` methods **only when the user explicitly provided the flag**. This avoids System.CommandLine's default values overriding config file values — the equivalent of the `IsImplicit` check, but done in the handler rather than a custom provider.

### Handling `--resume` and `--no-resume`

The `--resume <id>` and `--no-resume` flags require special handling because they carry **behavioral semantics** beyond a simple key override:

- `--resume <id>` — sets `Session:AutoResume` to `true` **and** specifies a target session ID. The session ID is not a configuration setting; it's a command argument consumed by the session-resume workflow.
- `--no-resume` — sets `Session:AutoResume` to `false` for this invocation.

These two flags are **mutually exclusive**. The handler calls `AddResumeOverride(true)` or `AddResumeOverride(false)` accordingly.

---

## 5. Configuration Validation

### Data Annotations

Decorate options classes with validation attributes:

```csharp
using System.ComponentModel.DataAnnotations;

public class BudgetOptions
{
    [Range(0, int.MaxValue, ErrorMessage = "Token budget must be non-negative.")]
    public int TokenBudgetPerModule { get; set; } = 0;

    [Range(0.0, 1.0, ErrorMessage = "Warning threshold must be between 0.0 and 1.0.")]
    public double WarningThreshold { get; set; } = 0.8;

    [Range(0.0, 1.0, ErrorMessage = "Confirmation threshold must be between 0.0 and 1.0.")]
    public double ConfirmationThreshold { get; set; } = 0.9;
}
```

### IValidateOptions\<T\> for Cross-Property Rules

```csharp
public class BudgetOptionsValidator : IValidateOptions<BudgetOptions>
{
    public ValidateOptionsResult Validate(string? name, BudgetOptions options)
    {
        var failures = new List<string>();

        if (options.WarningThreshold >= options.ConfirmationThreshold)
            failures.Add(
                "Budget:WarningThreshold must be less than Budget:ConfirmationThreshold.");

        if (options.ConfirmationThreshold > 1.0 || options.ConfirmationThreshold < 0.0)
            failures.Add(
                "Budget:ConfirmationThreshold must be between 0.0 and 1.0.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```

### Startup Validation for CLI Tools

CLI tools should **fail fast** — validate all configuration at startup, not on first access:

```csharp
services.AddOptions<BudgetOptions>()
    .BindConfiguration("budget")
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddSingleton<IValidateOptions<BudgetOptions>, BudgetOptionsValidator>();
```

### Error Presentation

Catch `OptionsValidationException` at the top level and format errors for the terminal:

```csharp
try
{
    // build and validate options
}
catch (OptionsValidationException ex)
{
    Console.Error.WriteLine("Configuration errors:");
    foreach (var failure in ex.Failures)
        Console.Error.WriteLine($"  ✗ {failure}");
    Environment.Exit(1);
}
```

All validators run and **all failures are aggregated** — the user sees every problem at once, not one at a time. This satisfies the acceptance criterion: "Invalid configuration values produce clear error messages with guidance."

### Manual Validation (Without DI)

For a simpler approach without the full Options/DI pipeline:

```csharp
public static class LopenOptionsValidator
{
    public static (bool IsValid, IReadOnlyList<string> Errors) Validate(LopenOptions options)
    {
        var errors = new List<string>();

        if (options.Workflow.MaxIterations < 1)
            errors.Add("Workflow:MaxIterations must be at least 1.");

        if (options.Workflow.FailureThreshold < 1)
            errors.Add("Workflow:FailureThreshold must be at least 1.");

        if (options.Budget.WarningThreshold >= options.Budget.ConfirmationThreshold)
            errors.Add("Budget:WarningThreshold must be less than Budget:ConfirmationThreshold.");

        return (errors.Count == 0, errors);
    }
}
```

---

## 6. Recommended NuGet Packages

All packages are at version **10.0.3** (current stable, compatible with .NET 10).

| Package | Purpose | Required |
|---|---|---|
| `Microsoft.Extensions.Configuration` | Core `ConfigurationBuilder` and abstractions | ✅ Yes |
| `Microsoft.Extensions.Configuration.Json` | `AddJsonFile()` for `.lopen/config.json` | ✅ Yes |
| `Microsoft.Extensions.Configuration.Binder` | `.Bind()` / `.Get<T>()` to map config to POCOs | ✅ Yes |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | `AddEnvironmentVariables()` for `LOPEN_*` vars | ⬜ Optional |
| `Microsoft.Extensions.Options` | `IOptions<T>`, options pattern with DI | ⬜ Optional |
| `Microsoft.Extensions.Options.DataAnnotations` | `ValidateDataAnnotations()` on options | ⬜ Optional |

### Minimal .csproj References

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.3" />
</ItemGroup>
```

### Extended (with validation and env vars)

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.3" />
</ItemGroup>
```

> **Note:** Do **not** use `Microsoft.Extensions.Configuration.CommandLine` — its simple parser conflicts with System.CommandLine. Use `AddInMemoryCollection` with typed helper methods on `LopenConfigurationBuilder` (see Section 4) instead.

---

## 7. Implementation Approach

### Recommended Architecture

```
Lopen.Configuration/
├── LopenOptions.cs              # Root options class with nested types
├── LopenConfigurationBuilder.cs # Builds IConfiguration with layered sources
├── LopenOptionsValidator.cs     # Validates merged configuration
├── ServiceCollectionExtensions.cs # DI registration
└── ConfigurationDiagnostics.cs  # Implements "lopen config show"
```

### Build Order

1. **Create application host** — `Host.CreateApplicationBuilder(args)` provides the hosting infrastructure
2. **Register configuration services** — `builder.Services.AddLopenConfiguration()` wires up `LopenConfigurationBuilder`
3. **Discover config file paths** — walk up directories for `.lopen/config.json`, resolve `~/.config/lopen/config.json`
4. **Build `IConfiguration`** — layer: defaults → global JSON → project JSON → env vars → CLI overrides (via `AddInMemoryCollection`)
5. **Bind to `LopenOptions`** — `config.Bind(options)` populates the strongly-typed tree of nested option classes
6. **Validate** — run validators, fail fast with aggregated error messages
7. **Inject** — pass `LopenOptions` (or individual nested options) to consuming modules as singletons

### Complete Wiring Example

The application entry point uses `Host.CreateApplicationBuilder`:

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLopenConfiguration();
// ... register other services
var app = builder.Build();
```

`LopenConfigurationBuilder` handles the layered resolution internally:

```csharp
public sealed class LopenConfigurationBuilder
{
    private readonly string? _globalConfigPath;
    private readonly string? _projectConfigPath;
    private readonly Dictionary<string, string?> _overrides = new();

    public (LopenOptions Options, IConfigurationRoot Configuration) Build()
    {
        var configBuilder = new ConfigurationBuilder();

        // Layer 1: Global configuration
        if (_globalConfigPath is not null && File.Exists(_globalConfigPath))
            configBuilder.AddJsonFile(_globalConfigPath, optional: true, reloadOnChange: false);

        // Layer 2: Project configuration
        if (_projectConfigPath is not null && File.Exists(_projectConfigPath))
            configBuilder.AddJsonFile(_projectConfigPath, optional: true, reloadOnChange: false);

        // Layer 3: Environment variables
        configBuilder.AddEnvironmentVariables("LOPEN_");

        // Layer 4: CLI flag overrides (highest priority)
        if (_overrides.Count > 0)
            configBuilder.AddInMemoryCollection(_overrides);

        var configRoot = configBuilder.Build();

        var options = new LopenOptions();
        configRoot.Bind(options);

        // Validate
        var errors = LopenOptionsValidator.Validate(options);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}")));

        return (options, configRoot);
    }
}
```

### Implementing `lopen config show`

The `IConfigurationRoot` exposes provider information for diagnostics:

```csharp
public static void ShowConfig(IConfigurationRoot configRoot)
{
    foreach (var provider in configRoot.Providers)
    {
        // Each provider can be inspected for its source type
    }

    // Or iterate all keys and find which provider supplied each value
    foreach (var section in configRoot.GetChildren())
    {
        PrintSection(configRoot, section, indent: 0);
    }
}

static void PrintSection(IConfigurationRoot root, IConfigurationSection section, int indent)
{
    var prefix = new string(' ', indent * 2);
    if (section.Value is not null)
    {
        // Find which provider supplied this value
        var source = GetSourceName(root, section.Path);
        Console.WriteLine($"{prefix}{section.Key}: {section.Value}  ({source})");
    }
    else
    {
        Console.WriteLine($"{prefix}{section.Key}:");
        foreach (var child in section.GetChildren())
            PrintSection(root, child, indent + 1);
    }
}
```

This satisfies the acceptance criteria for `lopen config show` displaying resolved configuration with sources indicated.

### `lopen config show --json`

```csharp
if (jsonOutput)
{
    var options = new LopenOptions();
    configRoot.Bind(options);
    Console.WriteLine(JsonSerializer.Serialize(options, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    }));
}
```

---

## 8. Relevance to Lopen

### Key Takeaways

1. **`ConfigurationBuilder` directly implements Lopen's hierarchy** — add sources in order (global → project → env vars → CLI) and last wins. No custom merging logic needed.

2. **Full hosting via `Host.CreateApplicationBuilder`** — Lopen uses the standard .NET hosting model, with `AddLopenConfiguration()` registering configuration services into DI. The `LopenConfigurationBuilder` uses a raw `ConfigurationBuilder` internally for the layered resolution.

3. **`AddInMemoryCollection` for CLI overrides** — rather than a custom `IConfigurationProvider`, CLI flag overrides are collected into a `Dictionary<string, string?>` and added as the highest-priority source via `AddInMemoryCollection`. The `LopenConfigurationBuilder` exposes typed helpers (`AddModelOverride`, `AddUnattendedOverride`, etc.) that populate this dictionary.

4. **`--model` maps to multiple keys** — a single `--model` flag sets all four phase model keys. `AddModelOverride()` handles this by writing four entries to the overrides dictionary.

5. **PascalCase binding — no `[ConfigurationKeyName]` needed** — JSON config files use PascalCase keys matching C# property names, so the default case-insensitive binder works without explicit key-name attributes.

6. **Nested option classes** — `LopenOptions` uses nested classes (`WorkflowOptions`, `SessionOptions`, `DisplayOptions`, `ModelOptions`, `BudgetOptions`, etc.) rather than flat properties. This groups related settings and maps naturally to the `:` hierarchy (e.g., `Workflow:MaxIterations`).

7. **Fail-fast validation** — CLI tools must validate at startup. Use `LopenOptionsValidator` with aggregated error messages so users see all problems at once.

8. **`lopen config show` is achievable** — `IConfigurationRoot.Providers` exposes the source of each value for diagnostic display.

9. **Environment variables are supported** — `AddEnvironmentVariables("LOPEN_")` is included in the configuration chain, sitting between JSON files and CLI overrides in priority.

10. **AOT compatibility** — if Lopen targets Native AOT, enable the configuration binding source generator (`EnableConfigurationBindingGenerator`) to avoid reflection.

### Decisions Made During Implementation

| Decision | Options Considered | Outcome |
|---|---|---|
| DI vs manual | Full `IOptions<T>` with DI vs manual `Bind()` | `Host.CreateApplicationBuilder` with DI; nested options registered as singletons |
| CLI override mechanism | Custom `IConfigurationProvider` vs `AddInMemoryCollection` | `AddInMemoryCollection` — simpler, no custom provider needed |
| Property naming | Snake_case JSON + `[ConfigurationKeyName]` vs PascalCase JSON | PascalCase JSON — matches C# properties, no attributes needed |
| Options structure | Flat properties on root vs nested classes | Nested classes — `WorkflowOptions`, `SessionOptions`, `DisplayOptions`, etc. |
| Validation | Data Annotations vs manual validator | Manual validator — simpler for a CLI tool |
| Env var support | Include `LOPEN_*` env vars now or later | Included — `AddEnvironmentVariables("LOPEN_")` in the chain |
| Config file format | JSON only vs also YAML/TOML | JSON only — spec mandates `config.json` |
| AOT | Reflection binder vs source generator | Source generator if targeting AOT; reflection otherwise |

---

## 9. Budget Enforcement Wiring

`BudgetEnforcer` and `IBudgetEnforcer` exist in `Lopen.Configuration` and are registered in DI via `ServiceCollectionExtensions.AddLopenConfiguration()`. The LLM module provides `ITokenTracker` / `InMemoryTokenTracker` (in `Lopen.Llm`) which accumulates `TokenUsage` per invocation and exposes `SessionTokenMetrics` with `CumulativeInputTokens + CumulativeOutputTokens` and `PremiumRequestCount`. These two pieces need to be wired together in the orchestration loop.

### Pre-Invocation Budget Check

Before each LLM call, the orchestrator should query `ITokenTracker.GetSessionMetrics()` and pass the cumulative values to `IBudgetEnforcer.Check()`:

```csharp
var metrics = tokenTracker.GetSessionMetrics();
long totalTokens = metrics.CumulativeInputTokens + metrics.CumulativeOutputTokens;
var budgetResult = budgetEnforcer.Check(totalTokens, metrics.PremiumRequestCount);

switch (budgetResult.Status)
{
    case BudgetStatus.Exceeded:
        // Halt the workflow — do not invoke the LLM
        break;
    case BudgetStatus.ConfirmationRequired:
        // In attended mode: prompt user to continue or abort
        // In unattended mode: halt (cannot prompt)
        break;
    case BudgetStatus.Warning:
        // Log warning, continue execution
        break;
    case BudgetStatus.Ok:
        break;
}
```

### Handling Budget Exceeded

When `BudgetStatus.Exceeded` is returned, the orchestration loop must **halt gracefully**:

1. **Do not invoke the LLM** — the budget is spent.
2. **Persist session state** — save current progress so `--resume` can continue if the user raises the budget.
3. **Report clearly** — display `budgetResult.Message` (e.g., "Token budget exceeded (105% used).") and exit with a non-zero code.

For `BudgetStatus.ConfirmationRequired`, behavior depends on `WorkflowOptions.Unattended`:
- **Attended:** prompt the user with the message and a continue/abort choice.
- **Unattended (`--unattended`):** treat as `Exceeded` — halt, since no user is present to confirm.

### Integration Points

| Component | Role |
|---|---|
| `ITokenTracker` (Lopen.Llm) | Accumulates per-invocation `TokenUsage`, exposes `SessionTokenMetrics` |
| `IBudgetEnforcer` (Lopen.Configuration) | Stateless checker — compares usage against `BudgetOptions` thresholds |
| Orchestration loop (consumer) | Calls `Check()` before each LLM invocation; acts on `BudgetStatus` |

The `BudgetEnforcer` is intentionally **stateless** — it does not track usage itself. The orchestrator owns the "read metrics → check budget → invoke or halt" flow. This keeps the configuration module decoupled from the LLM module: `IBudgetEnforcer.Check(long, int)` accepts raw numbers and has no dependency on `ITokenTracker` or `TokenUsage`.

### Token Counting

`IBudgetEnforcer.Check()` takes `currentTokens` as a `long` (cumulative input + output tokens) and `currentRequests` as an `int` (premium request count). Both values come from `SessionTokenMetrics`. A budget of `0` means unlimited — `BudgetEnforcer.CheckSingle` returns `BudgetStatus.Ok` when the limit is ≤ 0.

### Key Design Decision

The enforcer lives in `Lopen.Configuration` (not `Lopen.Llm`) because it operates purely on configuration values (`BudgetOptions`). The LLM module tracks usage; the configuration module defines and checks limits. The orchestrator bridges the two. This avoids a circular dependency between modules.
