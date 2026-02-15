# Research: Layered Configuration in .NET for CLI Tools

> **Date:** 2025-07-24
> **Sources:** [Microsoft.Extensions.Configuration docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration), [NuGet (10.0.3)](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json), [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)

---

## 1. Layered Configuration in .NET

`Microsoft.Extensions.Configuration` provides a provider-based system where multiple sources are composed into a single `IConfiguration` view. **Sources added later override earlier ones** for the same key — this "last wins" behavior is the foundation of Lopen's priority hierarchy.

```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile(globalConfigPath, optional: true)    // lowest priority
    .AddJsonFile(projectConfigPath, optional: true)   // overrides global
    .AddEnvironmentVariables(prefix: "LOPEN_")        // overrides JSON
    .Add(new ParseResultSource(parseResult, optionMap)) // CLI flags win
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
  "models": {
    "planning": "claude-opus-4.6",
    "building": "claude-opus-4.6"
  },
  "budget": {
    "token_budget_per_module": 50000,
    "warning_threshold": 0.8
  },
  "git": {
    "auto_commit": true,
    "convention": "conventional"
  }
}
```

This produces keys like `models:planning`, `budget:token_budget_per_module`, `git:auto_commit`.

### Important Behaviors

- `optional: true` — no error if the file doesn't exist (essential for Lopen's "works with zero config" principle)
- `reloadOnChange: false` — appropriate for CLI tools (no file watcher overhead)
- Property names are **case-insensitive** when binding
- Arrays use index-based keys: `models:0`, `models:1`

---

## 3. Configuration Binding

The `Microsoft.Extensions.Configuration.Binder` package maps configuration sections to strongly-typed C# classes.

### Defining Options Classes

```csharp
public class LopenOptions
{
    public ModelOptions Models { get; set; } = new();
    public BudgetOptions Budget { get; set; } = new();
    public OracleOptions Oracle { get; set; } = new();
    public GitOptions Git { get; set; } = new();

    [ConfigurationKeyName("tool_discipline")]
    public ToolDisciplineOptions ToolDiscipline { get; set; } = new();
    public bool Unattended { get; set; } = false;

    [ConfigurationKeyName("max_iterations")]
    public int MaxIterations { get; set; } = 100;

    [ConfigurationKeyName("failure_threshold")]
    public int FailureThreshold { get; set; } = 3;

    [ConfigurationKeyName("auto_resume")]
    public bool AutoResume { get; set; } = true;

    [ConfigurationKeyName("session_retention")]
    public int SessionRetention { get; set; } = 10;

    [ConfigurationKeyName("save_iteration_history")]
    public bool SaveIterationHistory { get; set; } = false;

    [ConfigurationKeyName("show_token_usage")]
    public bool ShowTokenUsage { get; set; } = true;

    [ConfigurationKeyName("show_premium_count")]
    public bool ShowPremiumCount { get; set; } = true;
}

public class ModelOptions
{
    [ConfigurationKeyName("requirement_gathering")]
    public string RequirementGathering { get; set; } = "claude-opus-4.6";
    public string Planning { get; set; } = "claude-opus-4.6";
    public string Building { get; set; } = "claude-opus-4.6";
    public string Research { get; set; } = "claude-opus-4.6";
}

public class BudgetOptions
{
    [ConfigurationKeyName("token_budget_per_module")]
    public int TokenBudgetPerModule { get; set; } = 0;

    [ConfigurationKeyName("premium_request_budget")]
    public int PremiumRequestBudget { get; set; } = 0;

    [ConfigurationKeyName("warning_threshold")]
    public double WarningThreshold { get; set; } = 0.8;

    [ConfigurationKeyName("confirmation_threshold")]
    public double ConfirmationThreshold { get; set; } = 0.9;
}

public class OracleOptions
{
    public string Model { get; set; } = "gpt-5-mini";
}

public class GitOptions
{
    public bool Enabled { get; set; } = true;

    [ConfigurationKeyName("auto_commit")]
    public bool AutoCommit { get; set; } = true;
    public string Convention { get; set; } = "conventional";
}

public class ToolDisciplineOptions
{
    [ConfigurationKeyName("max_file_reads")]
    public int MaxFileReads { get; set; } = 3;

    [ConfigurationKeyName("max_command_retries")]
    public int MaxCommandRetries { get; set; } = 3;
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

The configuration binder uses **case-insensitive matching** but does **not** strip underscores. This means snake_case JSON keys like `token_budget_per_module` will **not** automatically bind to PascalCase C# properties like `TokenBudgetPerModule` — the `OrdinalIgnoreCase` comparison fails because `_` ≠ `B`.

**Solution:** Use `[ConfigurationKeyName]` to explicitly map snake_case JSON keys to PascalCase properties:

```csharp
public class BudgetOptions
{
    [ConfigurationKeyName("token_budget_per_module")]
    public int TokenBudgetPerModule { get; set; } = 0;

    [ConfigurationKeyName("premium_request_budget")]
    public int PremiumRequestBudget { get; set; } = 0;

    [ConfigurationKeyName("warning_threshold")]
    public double WarningThreshold { get; set; } = 0.8;

    [ConfigurationKeyName("confirmation_threshold")]
    public double ConfirmationThreshold { get; set; } = 0.9;
}
```

Apply `[ConfigurationKeyName("snake_case_name")]` to every property whose JSON key uses snake_case. Properties that are single words (e.g., `Model`, `Enabled`) don't need the attribute since case-insensitive matching handles those. The attribute is in the `Microsoft.Extensions.Configuration` namespace and requires no extra packages.

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

### Solution: Custom IConfigurationProvider

A custom provider reads parsed CLI values from `System.CommandLine`'s `ParseResult` and injects them into the configuration pipeline as the highest-priority source:

```csharp
public class ParseResultConfigurationSource : IConfigurationSource
{
    private readonly ParseResult _parseResult;
    private readonly Dictionary<string, Option> _optionMap;

    public ParseResultConfigurationSource(
        ParseResult parseResult,
        Dictionary<string, Option> optionMap)
    {
        _parseResult = parseResult;
        _optionMap = optionMap;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new ParseResultConfigurationProvider(_parseResult, _optionMap);
}

public class ParseResultConfigurationProvider : ConfigurationProvider
{
    private readonly ParseResult _parseResult;
    private readonly Dictionary<string, Option> _optionMap;

    public ParseResultConfigurationProvider(
        ParseResult parseResult,
        Dictionary<string, Option> optionMap)
    {
        _parseResult = parseResult;
        _optionMap = optionMap;
    }

    public override void Load()
    {
        foreach (var (configKey, option) in _optionMap)
        {
            // Only set if the user explicitly provided the flag
            var result = _parseResult.FindResultFor(option);
            if (result is not null && !result.IsImplicit)
            {
                var value = _parseResult.GetValueForOption(option);
                if (value is not null)
                    Data[configKey] = value.ToString()!;
            }
        }
    }
}
```

### Extension Method for Clean Registration

```csharp
public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddParsedCommandLine(
        this IConfigurationBuilder builder,
        ParseResult parseResult,
        Dictionary<string, Option> optionMap)
    {
        builder.Add(new ParseResultConfigurationSource(parseResult, optionMap));
        return builder;
    }
}
```

### Wiring It Together

```csharp
var modelOption = new Option<string>("--model", "Override all model assignments");
var unattendedOption = new Option<bool>("--unattended", "Suppress intervention prompts");
var maxIterationsOption = new Option<int>("--max-iterations", "Maximum loop iterations");
var resumeOption = new Option<string?>("--resume", "Resume a specific session by ID");
var noResumeOption = new Option<bool>("--no-resume", "Disable auto-resume for this invocation");

// Map CLI flags to configuration keys
var optionMap = new Dictionary<string, Option>
{
    ["models:requirement_gathering"] = modelOption,
    ["models:planning"] = modelOption,
    ["models:building"] = modelOption,
    ["models:research"] = modelOption,
    ["unattended"] = unattendedOption,
    ["max_iterations"] = maxIterationsOption,
};

var rootCommand = new RootCommand("lopen")
{
    modelOption, unattendedOption, maxIterationsOption, resumeOption, noResumeOption
};
rootCommand.SetHandler(async (InvocationContext ctx) =>
{
    var config = new ConfigurationBuilder()
        .AddJsonFile(globalConfigPath, optional: true)
        .AddJsonFile(projectConfigPath, optional: true)
        .AddParsedCommandLine(ctx.ParseResult, optionMap) // highest priority
        .Build();

    var options = new LopenOptions();
    config.Bind(options);

    // options now reflects the full merged configuration
});
```

### Critical: `IsImplicit` Check

The `result.IsImplicit` check is essential. Without it, System.CommandLine's default values for options would override values from config files. Only **explicitly provided** CLI flags should participate as overrides.

### Handling `--resume` and `--no-resume`

The `--resume <id>` and `--no-resume` flags from the specification require special handling outside the configuration provider because they carry **behavioral semantics** beyond a simple key override:

- `--resume <id>` — sets `auto_resume` to `true` **and** specifies a target session ID. The session ID is not a configuration setting; it's a command argument consumed by the session-resume workflow.
- `--no-resume` — sets `auto_resume` to `false` for this invocation.

These two flags are **mutually exclusive**. Handle them in the command handler after configuration is built:

```csharp
rootCommand.SetHandler(async (InvocationContext ctx) =>
{
    var config = BuildConfiguration(ctx.ParseResult);
    var options = new LopenOptions();
    config.Bind(options);

    // Override auto_resume based on --resume / --no-resume
    var resumeResult = ctx.ParseResult.FindResultFor(resumeOption);
    var noResumeResult = ctx.ParseResult.FindResultFor(noResumeOption);

    string? resumeSessionId = null;
    if (resumeResult is not null && !resumeResult.IsImplicit)
    {
        options.AutoResume = true;
        resumeSessionId = ctx.ParseResult.GetValueForOption(resumeOption);
    }
    else if (noResumeResult is not null && !noResumeResult.IsImplicit)
    {
        options.AutoResume = false;
    }
});
```

Use `AddValidator` on the root command to enforce mutual exclusivity:

```csharp
rootCommand.AddValidator(result =>
{
    if (result.FindResultFor(resumeOption) is not null
        && result.FindResultFor(noResumeOption) is not null)
    {
        result.ErrorMessage = "Cannot use --resume and --no-resume together.";
    }
});
```

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
                "budget.warning_threshold must be less than budget.confirmation_threshold.");

        if (options.ConfirmationThreshold > 1.0 || options.ConfirmationThreshold < 0.0)
            failures.Add(
                "budget.confirmation_threshold must be between 0.0 and 1.0.");

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

        if (options.MaxIterations < 1)
            errors.Add("max_iterations must be at least 1.");

        if (options.FailureThreshold < 1)
            errors.Add("failure_threshold must be at least 1.");

        if (options.Budget.WarningThreshold >= options.Budget.ConfirmationThreshold)
            errors.Add("budget.warning_threshold must be less than budget.confirmation_threshold.");

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

> **Note:** Do **not** use `Microsoft.Extensions.Configuration.CommandLine` — its simple parser conflicts with System.CommandLine. Use the custom `ParseResultConfigurationProvider` from Section 4 instead.

---

## 7. Implementation Approach

### Recommended Architecture

```
Lopen.Configuration/
├── LopenOptions.cs              # Root options class with nested types
├── LopenConfigurationBuilder.cs # Builds IConfiguration with layered sources
├── ParseResultConfigurationProvider.cs  # Bridges System.CommandLine → IConfiguration
├── LopenOptionsValidator.cs     # Validates merged configuration
└── ConfigurationDiagnostics.cs  # Implements "lopen config show"
```

### Build Order

1. **Discover config file paths** — walk up directories for `.lopen/config.json`, resolve `~/.config/lopen/config.json`
2. **Build `IConfiguration`** — layer: defaults → global JSON → project JSON → CLI flags
3. **Bind to `LopenOptions`** — `config.Bind(options)` populates the strongly-typed tree
4. **Validate** — run validators, fail fast with aggregated error messages
5. **Inject** — pass `LopenOptions` (or `IOptions<LopenOptions>`) to consuming modules

### Complete Wiring Example

```csharp
public static class LopenConfigurationBuilder
{
    public static LopenOptions Build(ParseResult? parseResult = null)
    {
        var globalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "lopen", "config.json");

        var projectPath = FindProjectConfig(Directory.GetCurrentDirectory());

        var builder = new ConfigurationBuilder();

        // Layer 4: Built-in defaults (via class property initializers — no source needed)
        // Layer 3: Global config
        builder.AddJsonFile(globalPath, optional: true, reloadOnChange: false);

        // Layer 2: Project config
        if (projectPath is not null)
            builder.AddJsonFile(projectPath, optional: true, reloadOnChange: false);

        // Layer 1: CLI flags (highest priority)
        if (parseResult is not null)
            builder.AddParsedCommandLine(parseResult, CliOptionMappings.Map);

        var config = builder.Build();

        var options = new LopenOptions();
        config.Bind(options);

        // Validate
        var (isValid, errors) = LopenOptionsValidator.Validate(options);
        if (!isValid)
        {
            foreach (var error in errors)
                Console.Error.WriteLine($"  ✗ {error}");
            throw new InvalidOperationException("Invalid configuration.");
        }

        return options;
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

1. **`ConfigurationBuilder` directly implements Lopen's hierarchy** — add sources in order (global → project → CLI) and last wins. No custom merging logic needed.

2. **No host required** — `ConfigurationBuilder` works standalone without `Host.CreateApplicationBuilder()`. Lopen is a CLI tool, not a hosted service, so the raw builder is the right choice.

3. **Custom `ParseResultConfigurationProvider` is necessary** — System.CommandLine and M.E.Configuration.CommandLine are incompatible. The custom provider (Section 4) bridges the gap cleanly, using `IsImplicit` to avoid default-value conflicts.

4. **`--model` maps to multiple keys** — a single `--model` flag sets all four phase model keys. The option map handles this by mapping one `Option` to multiple configuration keys.

5. **Snake_case JSON requires `[ConfigurationKeyName]`** — the binder's case-insensitive comparison does **not** strip underscores. Properties with snake_case JSON keys (e.g., `token_budget_per_module`) must use `[ConfigurationKeyName("token_budget_per_module")]` to bind correctly. Single-word properties (e.g., `Model`, `Enabled`) work without it.

6. **Fail-fast validation** — CLI tools must validate at startup. Use `LopenOptionsValidator` with aggregated error messages so users see all problems at once.

7. **`lopen config show` is achievable** — `IConfigurationRoot.Providers` exposes the source of each value for diagnostic display.

8. **Environment variables are optional** — the spec doesn't require `LOPEN_*` env vars, but the infrastructure supports adding them later with zero code changes (just add `.AddEnvironmentVariables("LOPEN_")` to the builder chain).

9. **AOT compatibility** — if Lopen targets Native AOT, enable the configuration binding source generator (`EnableConfigurationBindingGenerator`) to avoid reflection.

### Decisions to Make During Implementation

| Decision | Options | Recommendation |
|---|---|---|
| DI vs manual | Full `IOptions<T>` with DI vs manual `Bind()` | Start with manual `Bind()`, add DI later if needed |
| Validation | Data Annotations vs manual validator | Manual validator — simpler for a CLI tool, no DI required |
| Env var support | Include `LOPEN_*` env vars now or later | Later — spec doesn't require it; easy to add |
| Config file format | JSON only vs also YAML/TOML | JSON only — spec mandates `config.json` |
| AOT | Reflection binder vs source generator | Source generator if targeting AOT; reflection otherwise |
