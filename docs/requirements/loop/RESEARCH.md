# Loop Module - Implementation Research

> Research for REQ-030 through REQ-036: Autonomous Development Workflow
> Last validated: 2026-01-25
> Target: .NET 10, GitHub.Copilot.SDK 0.1.17

## Executive Summary

The Loop module implements an autonomous, iterative development workflow that:
1. **Plans** work by analyzing specifications and creating a prioritized job list
2. **Builds** features by iteratively executing the highest priority job until complete
3. **Verifies** completion using a dedicated verification agent
4. **Streams** real-time output for human-on-the-loop oversight
5. **Manages state** via file-based persistence (jobs-to-be-done.json, IMPLEMENTATION_PLAN.md)

The implementation follows the **"Ralph Wiggum Technique"** (persistent iteration with minimal guardrails) and leverages the existing Copilot SDK integration for AI-powered automation.

---

## Architecture Overview

### High-Level Design

```
┌─────────────────────────────────────────────────────────────┐
│                       lopen loop                             │
│                     (CLI Command)                            │
└───────────────┬─────────────────────────────────────────────┘
                │
                ├─> LoopConfigService (REQ-031)
                │   ├─> Load user config (~/.lopen/loop-config.json)
                │   ├─> Load project config (.lopen/loop-config.json)
                │   └─> Merge with defaults
                │
                ├─> LoopStateManager (REQ-034)
                │   ├─> Read jobs-to-be-done.json
                │   ├─> Read IMPLEMENTATION_PLAN.md
                │   ├─> Check lopen.loop.done
                │   └─> Write state updates
                │
                └─> LoopService (REQ-030, REQ-032, REQ-033)
                    ├─> PLAN Phase (once)
                    │   ├─> Load PLAN.PROMPT.md
                    │   ├─> Create Copilot session
                    │   ├─> Stream prompt with context
                    │   ├─> LoopOutputService → Console
                    │   └─> Update jobs-to-be-done.json
                    │
                    └─> BUILD Phase (loop until done)
                        ├─> Load BUILD.PROMPT.md
                        ├─> Create Copilot session
                        ├─> Stream prompt with context
                        ├─> LoopOutputService → Console
                        ├─> VerificationService checks completion
                        ├─> Update IMPLEMENTATION_PLAN.md
                        ├─> Check lopen.loop.done
                        └─> Increment iteration counter
```

### Component Responsibilities

| Component | Purpose | Dependencies |
|-----------|---------|--------------|
| `LoopCommand` | CLI entry point, coordinates phases | `LoopService`, `LoopConfigService` |
| `LoopService` | Core orchestration of plan/build workflow | `ICopilotService`, `LoopStateManager`, `LoopOutputService` |
| `LoopConfigService` | Load/save/merge configuration | File system |
| `LoopStateManager` | Read/write state files (jobs, plan, done) | File system, JSON serialization |
| `LoopOutputService` | Stream output with formatting | `ConsoleOutput`, Spectre.Console |
| `VerificationService` | Quality gates via sub-agent | `ICopilotService` |

---

## Implementation Approach by Requirement

### REQ-030: Loop Command

**Goal**: Entry point for the autonomous loop workflow.

#### Command Structure

```csharp
// In Program.cs
var loopCommand = new Command("loop", "Autonomous development workflow");

var autoOption = new Option<bool>("--auto")
{
    Description = "Skip interactive setup, use defaults",
    DefaultValueFactory = _ => false
};

var configOption = new Option<string?>("--config")
{
    Description = "Path to custom config file"
};
configOption.Aliases.Add("-c");

loopCommand.Options.Add(autoOption);
loopCommand.Options.Add(configOption);

loopCommand.SetAction(async parseResult =>
{
    var auto = parseResult.GetValue(autoOption);
    var configPath = parseResult.GetValue(configOption);
    
    var configService = new LoopConfigService();
    var config = await configService.LoadConfigAsync(configPath);
    
    if (!auto)
    {
        // Interactive prompt: "Do you need to add specifications, or shall we commence planning and building?"
        output.Info("Options:");
        output.WriteLine("  1. Add specifications and then plan/build");
        output.WriteLine("  2. Proceed directly to planning");
        output.WriteLine("  3. Skip planning, start building");
        output.Write("Select (1-3, default=2): ");
        
        var choice = Console.ReadLine()?.Trim();
        // Handle choice logic...
    }
    
    var loopService = new LoopService(
        copilotService, 
        new LoopStateManager(), 
        new LoopOutputService(output),
        config);
    
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    return await loopService.RunAsync(cts.Token);
});

rootCommand.Subcommands.Add(loopCommand);
```

#### LoopService Class Design

```csharp
namespace Lopen.Core;

public class LoopService
{
    private readonly ICopilotService _copilotService;
    private readonly LoopStateManager _stateManager;
    private readonly LoopOutputService _outputService;
    private readonly LoopConfig _config;
    
    public LoopService(
        ICopilotService copilotService,
        LoopStateManager stateManager,
        LoopOutputService outputService,
        LoopConfig config)
    {
        _copilotService = copilotService;
        _stateManager = stateManager;
        _outputService = outputService;
        _config = config;
    }
    
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        // 1. Run PLAN phase
        await RunPlanPhaseAsync(ct);
        
        // 2. Run BUILD phase loop
        return await RunBuildPhaseAsync(ct);
    }
    
    private async Task RunPlanPhaseAsync(CancellationToken ct)
    {
        _outputService.WritePhaseHeader("PLAN");
        
        // Remove lopen.loop.done if it exists
        _stateManager.RemoveDoneFile();
        
        // Load plan prompt
        var prompt = await File.ReadAllTextAsync(_config.PlanPromptPath, ct);
        
        // Create session and stream
        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions
            {
                Model = _config.Model,
                Streaming = _config.Stream
            }, ct);
        
        // Stream prompt execution
        await foreach (var chunk in session.StreamAsync(prompt, ct))
        {
            _outputService.WriteChunk(chunk);
        }
        
        _outputService.WriteLine();
    }
    
    private async Task<int> RunBuildPhaseAsync(CancellationToken ct)
    {
        _outputService.WritePhaseHeader("BUILD");
        
        int iteration = 0;
        
        while (!ct.IsCancellationRequested)
        {
            // Check for completion signal
            if (_stateManager.IsDone())
            {
                _outputService.WriteSuccess("Loop complete!");
                return ExitCodes.Success;
            }
            
            // Load build prompt
            var prompt = await File.ReadAllTextAsync(_config.BuildPromptPath, ct);
            
            // Create session and stream
            await using var session = await _copilotService.CreateSessionAsync(
                new CopilotSessionOptions
                {
                    Model = _config.Model,
                    Streaming = _config.Stream
                }, ct);
            
            // Stream prompt execution
            await foreach (var chunk in session.StreamAsync(prompt, ct))
            {
                _outputService.WriteChunk(chunk);
            }
            
            _outputService.WriteLine();
            
            // Increment and display iteration
            iteration++;
            _outputService.WriteIterationComplete(iteration);
        }
        
        _outputService.WriteInfo("Loop interrupted by user.");
        return ExitCodes.Success;
    }
}
```

**Key Design Decisions**:
- Uses existing `ICopilotService` abstraction for testability
- Separates PLAN (once) from BUILD (loop) phases clearly
- Delegates output formatting to `LoopOutputService`
- Delegates state management to `LoopStateManager`
- Cancellation token propagation for Ctrl+C handling

---

### REQ-031: Loop Configuration

**Goal**: Configurable loop behavior with user/project precedence.

#### Configuration Model

```csharp
namespace Lopen.Core;

/// <summary>
/// Configuration for loop behavior.
/// </summary>
public class LoopConfig
{
    public string Model { get; init; } = "claude-opus-4.5";
    public string PlanPromptPath { get; init; } = "PLAN.PROMPT.md";
    public string BuildPromptPath { get; init; } = "BUILD.PROMPT.md";
    public bool AllowAll { get; init; } = true;
    public bool Stream { get; init; } = true;
    public bool AutoCommit { get; init; } = false;
    public string LogLevel { get; init; } = "all";
}

/// <summary>
/// Service for loading and saving loop configuration.
/// </summary>
public class LoopConfigService
{
    private const string UserConfigDir = "~/.lopen";
    private const string UserConfigFile = "loop-config.json";
    private const string ProjectConfigDir = ".lopen";
    private const string ProjectConfigFile = "loop-config.json";
    
    public async Task<LoopConfig> LoadConfigAsync(string? customPath = null)
    {
        // Start with defaults
        var config = new LoopConfig();
        
        // Load user config if exists
        var userConfigPath = ExpandPath(Path.Combine(UserConfigDir, UserConfigFile));
        if (File.Exists(userConfigPath))
        {
            var userConfig = await LoadFromFileAsync(userConfigPath);
            config = MergeConfigs(config, userConfig);
        }
        
        // Load project config if exists (overrides user)
        var projectConfigPath = Path.Combine(ProjectConfigDir, ProjectConfigFile);
        if (File.Exists(projectConfigPath))
        {
            var projectConfig = await LoadFromFileAsync(projectConfigPath);
            config = MergeConfigs(config, projectConfig);
        }
        
        // Load custom config if specified (overrides all)
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            var customConfig = await LoadFromFileAsync(customPath);
            config = MergeConfigs(config, customConfig);
        }
        
        return config;
    }
    
    public async Task SaveConfigAsync(LoopConfig config, bool projectLevel = false)
    {
        var path = projectLevel
            ? Path.Combine(ProjectConfigDir, ProjectConfigFile)
            : ExpandPath(Path.Combine(UserConfigDir, UserConfigFile));
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        // Serialize and write
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(path, json);
    }
    
    private async Task<LoopConfig?> LoadFromFileAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<LoopConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    
    private LoopConfig MergeConfigs(LoopConfig baseConfig, LoopConfig? overrides)
    {
        if (overrides is null) return baseConfig;
        
        // Use reflection or manual property merging
        // Only override non-default values
        return new LoopConfig
        {
            Model = overrides.Model ?? baseConfig.Model,
            PlanPromptPath = overrides.PlanPromptPath ?? baseConfig.PlanPromptPath,
            BuildPromptPath = overrides.BuildPromptPath ?? baseConfig.BuildPromptPath,
            AllowAll = overrides.AllowAll,
            Stream = overrides.Stream,
            AutoCommit = overrides.AutoCommit,
            LogLevel = overrides.LogLevel ?? baseConfig.LogLevel
        };
    }
    
    private string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                path[2..]);
        return path;
    }
}
```

#### Configure Subcommand

```csharp
// lopen loop configure
var loopConfigureCommand = new Command("configure", "Configure loop behavior");

var modelOption = new Option<string?>("--model") { Description = "AI model" };
var planPromptOption = new Option<string?>("--plan-prompt") { Description = "Plan prompt path" };
var buildPromptOption = new Option<string?>("--build-prompt") { Description = "Build prompt path" };
var allowAllOption = new Option<bool?>("--allow-all") { Description = "Allow all operations" };
var resetOption = new Option<bool>("--reset") { Description = "Reset to defaults" };

loopConfigureCommand.Options.Add(modelOption);
loopConfigureCommand.Options.Add(planPromptOption);
loopConfigureCommand.Options.Add(buildPromptOption);
loopConfigureCommand.Options.Add(allowAllOption);
loopConfigureCommand.Options.Add(resetOption);

loopConfigureCommand.SetAction(async parseResult =>
{
    var configService = new LoopConfigService();
    
    if (parseResult.GetValue(resetOption))
    {
        // Reset by deleting config files
        output.Success("Configuration reset to defaults.");
        return ExitCodes.Success;
    }
    
    // Load current config
    var config = await configService.LoadConfigAsync();
    
    // Apply command-line overrides
    var model = parseResult.GetValue(modelOption);
    if (!string.IsNullOrEmpty(model))
        config = config with { Model = model };
    
    // ... apply other options
    
    // Interactive mode if no options provided
    if (model is null && /* all other options null */)
    {
        output.Info("Interactive configuration:");
        // Prompt for each setting...
    }
    
    // Save config
    await configService.SaveConfigAsync(config);
    output.Success("Configuration saved.");
    
    return ExitCodes.Success;
});

loopCommand.Subcommands.Add(loopConfigureCommand);
```

**Key Design Decisions**:
- JSON-based configuration (human-readable, editable)
- Layered precedence: defaults → user → project → custom file
- Record types for immutability (`config with { Property = value }`)
- Uses System.Text.Json (built-in, fast)
- Supports both interactive and flag-based configuration

---

### REQ-032: Plan Phase & REQ-033: Build Phase

**Covered in LoopService implementation above.** Key patterns:

1. **Prompt Loading**: Read from configured file paths
2. **Session Creation**: Use `ICopilotService.CreateSessionAsync` with streaming
3. **Event Streaming**: `session.StreamAsync(prompt, ct)` yields chunks
4. **Output Delegation**: `LoopOutputService.WriteChunk(chunk)` for console
5. **Iteration Counting**: Simple integer counter, displayed after each cycle
6. **Completion Detection**: Check for `lopen.loop.done` file

**Integration with Sub-agents**:
- The prompts (`PLAN.PROMPT.md`, `BUILD.PROMPT.md`) instruct the AI to use sub-agents
- SDK automatically handles sub-agent spawning via `SubagentStartedEvent` and `SubagentCompletedEvent`
- No special code needed - just stream output as-is

---

### REQ-034: State Management

**Goal**: Persist loop state across iterations and application restarts.

#### LoopStateManager Class

```csharp
namespace Lopen.Core;

/// <summary>
/// Manages loop state files (jobs-to-be-done.json, IMPLEMENTATION_PLAN.md, lopen.loop.done).
/// </summary>
public class LoopStateManager
{
    private const string JobsFilePath = "docs/requirements/jobs-to-be-done.json";
    private const string PlanFilePath = "docs/requirements/IMPLEMENTATION_PLAN.md";
    private const string DoneFilePath = "lopen.loop.done";
    
    public async Task<JobsToBeDone> ReadJobsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(JobsFilePath))
            return new JobsToBeDone { Jobs = [] };
        
        var json = await File.ReadAllTextAsync(JobsFilePath, ct);
        return JsonSerializer.Deserialize<JobsToBeDone>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new JobsToBeDone { Jobs = [] };
    }
    
    public async Task WriteJobsAsync(JobsToBeDone jobs, CancellationToken ct = default)
    {
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(JobsFilePath)!);
        
        var json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(JobsFilePath, json, ct);
    }
    
    public async Task<string> ReadPlanAsync(CancellationToken ct = default)
    {
        if (!File.Exists(PlanFilePath))
            return string.Empty;
        
        return await File.ReadAllTextAsync(PlanFilePath, ct);
    }
    
    public async Task WritePlanAsync(string content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PlanFilePath)!);
        await File.WriteAllTextAsync(PlanFilePath, content, ct);
    }
    
    public bool IsDone() => File.Exists(DoneFilePath);
    
    public void RemoveDoneFile()
    {
        if (File.Exists(DoneFilePath))
            File.Delete(DoneFilePath);
    }
    
    public async Task CreateDoneFileAsync(CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(DoneFilePath, 
            $"Loop completed at {DateTimeOffset.UtcNow:O}\n", ct);
    }
}

/// <summary>
/// Model for jobs-to-be-done.json structure.
/// </summary>
public class JobsToBeDone
{
    public List<Job> Jobs { get; init; } = [];
}

public class Job
{
    public string Id { get; init; } = string.Empty;
    public string Requirement { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string Status { get; init; } = "pending"; // pending, in-progress, done
    public string? Notes { get; init; }
}
```

**Key Design Decisions**:
- **File-based**: Simple, transparent, git-friendly
- **JSON for structured data**: jobs-to-be-done.json
- **Markdown for human content**: IMPLEMENTATION_PLAN.md
- **Simple signal file**: lopen.loop.done (presence = done)
- **No database**: Keeps infrastructure minimal
- **Synchronous file checks**: `File.Exists()` is cheap
- **Async I/O**: Use async file operations for read/write

**State Resilience**:
- Files survive application restart → loop can resume
- Git tracks changes → audit trail
- Human-readable → easy debugging

---

### REQ-035: Output Streaming

**Goal**: Real-time visibility with clear phase indicators.

#### LoopOutputService Class

```csharp
namespace Lopen.Core;

using Spectre.Console;

/// <summary>
/// Handles formatted output for loop operations.
/// </summary>
public class LoopOutputService
{
    private readonly ConsoleOutput _output;
    
    public LoopOutputService(ConsoleOutput output)
    {
        _output = output;
    }
    
    public void WritePhaseHeader(string phase)
    {
        _output.WriteLine();
        _output.WriteLine("------------");
        _output.WriteLine($"Mode: {phase}");
        _output.WriteLine("------------");
    }
    
    public void WriteIterationComplete(int iteration)
    {
        _output.WriteLine();
        _output.WriteLine("------------------------------");
        _output.WriteLine($"Completed iteration {iteration}");
        _output.WriteLine("------------------------------");
    }
    
    public void WriteChunk(string chunk)
    {
        // Direct output, no buffering (for real-time streaming)
        Console.Write(chunk);
    }
    
    public void WriteSuccess(string message)
    {
        _output.Success(message);
    }
    
    public void WriteInfo(string message)
    {
        _output.Info(message);
    }
    
    public void WriteLine(string message = "")
    {
        _output.WriteLine(message);
    }
}
```

**Alternative: Spectre.Console Live Display**

For more sophisticated output (e.g., showing sub-agent activity):

```csharp
public async Task StreamWithLiveDisplayAsync(
    ICopilotSession session, 
    string prompt, 
    CancellationToken ct)
{
    await AnsiConsole.Live(new Markup(""))
        .StartAsync(async ctx =>
        {
            var display = new Markup("");
            ctx.UpdateTarget(display);
            
            var contentBuilder = new StringBuilder();
            
            await foreach (var chunk in session.StreamAsync(prompt, ct))
            {
                contentBuilder.Append(chunk);
                display = new Markup(Markup.Escape(contentBuilder.ToString()));
                ctx.UpdateTarget(display);
            }
        });
}
```

**Key Design Decisions**:
- **Direct console write**: `Console.Write(chunk)` for streaming (no buffering)
- **Spectre.Console for structure**: Phase headers, iteration counters
- **NO_COLOR support**: Automatic via ConsoleOutput
- **Simple format**: Matches bash script style for consistency
- **Optional Live Display**: Can enhance in future without breaking API

---

### REQ-036: Verification Agent

**Goal**: Dedicated sub-agent for quality gates.

#### VerificationService Class

```csharp
namespace Lopen.Core;

/// <summary>
/// Service for verifying task completion and quality.
/// Uses a dedicated sub-agent via Copilot SDK.
/// </summary>
public class VerificationService
{
    private readonly ICopilotService _copilotService;
    
    public VerificationService(ICopilotService copilotService)
    {
        _copilotService = copilotService;
    }
    
    public async Task<VerificationResult> VerifyTaskCompletionAsync(
        string jobId, 
        string requirementCode,
        CancellationToken ct = default)
    {
        var prompt = BuildVerificationPrompt(jobId, requirementCode);
        
        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions { Model = "gpt-5" }, ct);
        
        var response = await session.SendAsync(prompt, ct);
        
        // Parse response (expect structured JSON or clear indicators)
        return ParseVerificationResponse(response);
    }
    
    private string BuildVerificationPrompt(string jobId, string requirementCode)
    {
        return $"""
            You are a verification agent. Verify if the following task is complete:
            
            - Job ID: {jobId}
            - Requirement: {requirementCode}
            
            Check the following:
            1. Tests exist and pass for this requirement
            2. Documentation exists (Divio model)
            3. Build succeeds
            4. Requirement code is valid (exists in SPECIFICATION.md)
            5. Commits follow conventional commit format
            
            Respond in JSON format:
            {{
                "complete": true/false,
                "testsPass": true/false,
                "documentationExists": true/false,
                "buildSucceeds": true/false,
                "requirementValid": true/false,
                "issues": ["list of any issues found"]
            }}
            """;
    }
    
    private VerificationResult ParseVerificationResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return new VerificationResult { Complete = false, Issues = ["No response from verification agent"] };
        
        try
        {
            var result = JsonSerializer.Deserialize<VerificationResult>(response, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new VerificationResult { Complete = false };
        }
        catch (JsonException)
        {
            // Fallback: parse text response
            return new VerificationResult
            {
                Complete = response.Contains("complete: true", StringComparison.OrdinalIgnoreCase),
                Issues = response.Contains("issue") ? [response] : []
            };
        }
    }
}

public class VerificationResult
{
    public bool Complete { get; init; }
    public bool TestsPass { get; init; }
    public bool DocumentationExists { get; init; }
    public bool BuildSucceeds { init; }
    public bool RequirementValid { get; init; }
    public List<string> Issues { get; init; } = [];
}
```

**Alternative: Custom Agent via SDK**

The Copilot SDK supports custom agents via `SessionConfig.CustomAgents`:

```csharp
await using var session = await _copilotService.CreateSessionAsync(
    new CopilotSessionOptions
    {
        Model = "gpt-5",
        CustomAgents = new List<CustomAgentConfig>
        {
            new()
            {
                Name = "verify-job-complete",
                Description = "Verifies if a job is complete with tests and documentation",
                Instructions = File.ReadAllText(".github/agents/verify-job-complete.agent.md")
            }
        }
    }, ct);

var response = await session.SendAsync(
    "Use the verify-job-complete agent to check if JTBD-042 is complete.", ct);
```

**Key Design Decisions**:
- **Dedicated service**: Separates verification logic
- **Sub-agent pattern**: Leverages Copilot's multi-agent capabilities
- **Structured output**: JSON response for machine parsing
- **Fallback parsing**: Handles text responses gracefully
- **Comprehensive checks**: Tests, docs, build, requirement validity
- **Optional integration**: Can be called from BUILD phase as needed

---

## Code Patterns and Interfaces

### Service Interfaces

```csharp
// ILoopService.cs
namespace Lopen.Core;

public interface ILoopService
{
    Task<int> RunAsync(CancellationToken ct = default);
    Task<int> RunPlanPhaseAsync(CancellationToken ct = default);
    Task<int> RunBuildPhaseAsync(CancellationToken ct = default);
}

// ILoopStateManager.cs
public interface ILoopStateManager
{
    Task<JobsToBeDone> ReadJobsAsync(CancellationToken ct = default);
    Task WriteJobsAsync(JobsToBeDone jobs, CancellationToken ct = default);
    Task<string> ReadPlanAsync(CancellationToken ct = default);
    Task WritePlanAsync(string content, CancellationToken ct = default);
    bool IsDone();
    void RemoveDoneFile();
    Task CreateDoneFileAsync(CancellationToken ct = default);
}

// ILoopOutputService.cs
public interface ILoopOutputService
{
    void WritePhaseHeader(string phase);
    void WriteIterationComplete(int iteration);
    void WriteChunk(string chunk);
    void WriteSuccess(string message);
    void WriteInfo(string message);
    void WriteLine(string message = "");
}

// IVerificationService.cs
public interface IVerificationService
{
    Task<VerificationResult> VerifyTaskCompletionAsync(
        string jobId, 
        string requirementCode,
        CancellationToken ct = default);
}
```

**Rationale**: Interfaces enable:
- Unit testing with mocks
- Dependency injection
- Future implementation swapping (e.g., database-backed state)

### Cancellation Token Patterns

```csharp
// Combine external token with Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

try
{
    await loopService.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    output.Info("Loop interrupted.");
}
```

### Streaming Pattern

```csharp
// Stream chunks to console in real-time
await foreach (var chunk in session.StreamAsync(prompt, ct))
{
    Console.Write(chunk); // Immediate output
}
Console.WriteLine(); // Final newline
```

### Configuration Merging Pattern

```csharp
// Use record types for immutability
var merged = baseConfig with
{
    Model = overrides?.Model ?? baseConfig.Model,
    Stream = overrides?.Stream ?? baseConfig.Stream
    // ...
};
```

---

## Testing Strategy

### Unit Tests

**Mock-Based Testing**: Use mocks for dependencies.

```csharp
// LoopServiceTests.cs
public class LoopServiceTests
{
    [Fact]
    public async Task RunAsync_ExecutesPlanThenBuild()
    {
        // Arrange
        var mockCopilot = new MockCopilotService();
        var mockState = new MockLoopStateManager();
        var mockOutput = new MockLoopOutputService();
        var config = new LoopConfig();
        
        var service = new LoopService(mockCopilot, mockState, mockOutput, config);
        
        // Act
        var exitCode = await service.RunAsync();
        
        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
        mockOutput.PhaseHeaders.ShouldContain("PLAN");
        mockOutput.PhaseHeaders.ShouldContain("BUILD");
    }
    
    [Fact]
    public async Task RunBuildPhaseAsync_StopsWhenDoneFileExists()
    {
        // Arrange
        var mockState = new MockLoopStateManager();
        mockState.SetDone(true);
        
        var service = new LoopService(/*...*/);
        
        // Act
        var exitCode = await service.RunBuildPhaseAsync();
        
        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
        mockOutput.Messages.ShouldContain("Loop complete!");
    }
    
    [Fact]
    public async Task RunAsync_HandlesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        
        // Act
        var exitCode = await service.RunAsync(cts.Token);
        
        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }
}

// LoopConfigServiceTests.cs
public class LoopConfigServiceTests
{
    [Fact]
    public async Task LoadConfigAsync_MergesUserAndProjectConfigs()
    {
        // Setup temp files with different configs
        // Load and verify precedence
    }
    
    [Fact]
    public async Task SaveConfigAsync_WritesValidJson()
    {
        // Save config, read back, verify
    }
}

// LoopStateManagerTests.cs
public class LoopStateManagerTests
{
    [Fact]
    public async Task ReadJobsAsync_ReturnsEmptyWhenFileNotExists()
    {
        var manager = new LoopStateManager();
        var jobs = await manager.ReadJobsAsync();
        jobs.Jobs.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task IsDone_ReturnsTrueWhenFileExists()
    {
        // Create temp done file
        var manager = new LoopStateManager();
        manager.IsDone().ShouldBeTrue();
    }
}

// VerificationServiceTests.cs
public class VerificationServiceTests
{
    [Fact]
    public async Task VerifyTaskCompletionAsync_ParsesJsonResponse()
    {
        var mockCopilot = new MockCopilotService();
        mockCopilot.SetResponse("""{"complete": true, "testsPass": true}""");
        
        var service = new VerificationService(mockCopilot);
        var result = await service.VerifyTaskCompletionAsync("JTBD-001", "REQ-030");
        
        result.Complete.ShouldBeTrue();
        result.TestsPass.ShouldBeTrue();
    }
}
```

### Integration Tests

**Real Copilot SDK**: Use actual SDK with skippable tests.

```csharp
[Trait("Category", "Integration")]
[Trait("Requires", "CopilotCli")]
public class LoopIntegrationTests
{
    [SkippableFact]
    public async Task LoopService_WithRealCopilot_RunsPlanPhase()
    {
        // Skip if Copilot not available
        var cli = Process.Start("copilot", "--version");
        Skip.If(cli == null, "Copilot CLI not installed");
        
        await using var copilotService = new CopilotService();
        var authStatus = await copilotService.GetAuthStatusAsync();
        Skip.IfNot(authStatus.IsAuthenticated, "Not authenticated");
        
        // Create temp directory with test prompts
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "PLAN.PROMPT.md"),
            "List the next 3 jobs to be done for this test project.");
        
        // Run plan phase
        var config = new LoopConfig
        {
            PlanPromptPath = Path.Combine(tempDir, "PLAN.PROMPT.md")
        };
        
        var service = new LoopService(copilotService, /*...*/);
        var exitCode = await service.RunPlanPhaseAsync();
        
        exitCode.ShouldBe(ExitCodes.Success);
        
        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
```

### Self-Test (Testing Module Integration)

As specified in the requirements:

```csharp
// Loop generates "5 bread recipes" in tmp folder
[Trait("Category", "SelfTest")]
public class LoopSelfTests
{
    [Fact]
    public async Task Loop_GeneratesBreadRecipes_InTempFolder()
    {
        // Setup: Create PLAN.PROMPT.md that instructs AI to generate 5 bread recipes
        var tempDir = Path.Combine(Path.GetTempPath(), "lopen-self-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        
        var planPrompt = """
            Study this empty directory. Your job is to create 5 bread recipe markdown files 
            (recipe1.md through recipe5.md) with unique recipes. 
            
            When complete, create a lopen.loop.done file.
            """;
        
        await File.WriteAllTextAsync(Path.Combine(tempDir, "PLAN.PROMPT.md"), planPrompt);
        
        // Execute loop
        var config = new LoopConfig
        {
            PlanPromptPath = Path.Combine(tempDir, "PLAN.PROMPT.md"),
            BuildPromptPath = Path.Combine(tempDir, "PLAN.PROMPT.md") // Same for simplicity
        };
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // Timeout
        
        await using var copilotService = new CopilotService();
        var service = new LoopService(copilotService, /*...*/);
        
        var exitCode = await service.RunAsync(cts.Token);
        
        // Verify
        exitCode.ShouldBe(ExitCodes.Success);
        Directory.GetFiles(tempDir, "recipe*.md").Length.ShouldBe(5);
        File.Exists(Path.Combine(tempDir, "lopen.loop.done")).ShouldBeTrue();
        
        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
```

**Test Coverage Targets**:
- Core services: 100%
- CLI commands: 80%+ (mocked Copilot interactions)
- Integration tests: Manual verification + CI when Copilot available

---

## Best Practices for Human-on-the-Loop Workflows in .NET

### 1. BackgroundService Pattern (Optional Enhancement)

For future: Run loop as a background service with separate control channel.

```csharp
public class LoopBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Run build iteration
            await RunBuildIterationAsync(stoppingToken);
        }
    }
}
```

**When to use**: Multi-process scenarios, web dashboard, monitoring.
**For MVP**: Not needed - simple command-line loop is sufficient.

### 2. Cancellation Token Discipline

```csharp
// Always accept and propagate CancellationToken
public async Task DoWorkAsync(CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    
    await someOperation(ct);
    
    ct.ThrowIfCancellationRequested();
}
```

### 3. Graceful Shutdown

```csharp
try
{
    await loopService.RunAsync(ct);
}
catch (OperationCanceledException)
{
    // Expected during Ctrl+C
    output.Info("Shutting down gracefully...");
    
    // Cleanup: close sessions, flush logs
    await copilotService.DisposeAsync();
}
finally
{
    output.Info("Goodbye!");
}
```

### 4. Progress Reporting

Use `IProgress<T>` for structured progress updates:

```csharp
public async Task RunAsync(IProgress<LoopProgress>? progress = null, CancellationToken ct = default)
{
    progress?.Report(new LoopProgress { Phase = "PLAN", Message = "Starting..." });
    
    // ... work ...
    
    progress?.Report(new LoopProgress { Phase = "BUILD", Iteration = 1 });
}

public record LoopProgress(string Phase, int Iteration = 0, string? Message = null);
```

### 5. Idempotency

Ensure operations can be safely retried:

```csharp
// Reading is naturally idempotent
var jobs = await stateManager.ReadJobsAsync();

// Writing with backup
var backup = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
try
{
    await File.WriteAllTextAsync(path, newContent);
}
catch
{
    if (backup is not null)
        await File.WriteAllTextAsync(path, backup);
    throw;
}
```

### 6. Logging (Future Enhancement)

Use Microsoft.Extensions.Logging for structured logs:

```csharp
public class LoopService
{
    private readonly ILogger<LoopService> _logger;
    
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Loop started");
        
        try
        {
            // ... work ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loop failed");
            throw;
        }
        finally
        {
            _logger.LogInformation("Loop completed");
        }
    }
}
```

**For MVP**: Console output is sufficient. Add logging later if needed.

---

## References

### Documentation
- [GitHub Copilot SDK (.NET)](https://github.com/github/copilot-sdk/tree/main/dotnet)
- [System.CommandLine 2.0](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [Spectre.Console](https://spectreconsole.net/)
- [.NET Background Services](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/background-tasks-with-ihostedservice)
- [Cancellation in .NET](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- [Divio Documentation System](https://documentation.divio.com/)
- [Conventional Commits](https://www.conventionalcommits.org/)

### Project Files
- `scripts/lopen.sh` - Original bash implementation (reference)
- `PLAN.PROMPT.md` - Plan phase instructions
- `BUILD.PROMPT.md` - Build phase instructions
- `docs/requirements/jobs-to-be-done.json` - Task tracking
- `docs/requirements/loop/SPECIFICATION.md` - Requirements
- `src/Lopen.Core/CopilotService.cs` - Existing Copilot integration
- `src/Lopen.Core/SessionState.cs` - Session state patterns
- `.github/agents/research.agent.md` - Research sub-agent

### Related Requirements
- REQ-020: Copilot SDK Integration (completed)
- REQ-021: Chat Command (completed)
- REQ-022: Streaming Responses (completed)
- REQ-023: Custom Tools (completed)
- REQ-011: Session State Management (completed)
- REQ-014: Modern TUI Patterns (in progress)

---

## Implementation Sequence

### Phase 1: Core Infrastructure (REQ-031, REQ-034)
1. ✅ `LoopConfig` model and JSON schema
2. ✅ `LoopConfigService` with file loading/saving
3. ✅ `LoopStateManager` with jobs/plan/done file handling
4. ✅ Unit tests for config and state management

### Phase 2: Output & Services (REQ-035)
5. ✅ `LoopOutputService` with phase headers and iteration counter
6. ✅ `ILoopOutputService` interface
7. ✅ Unit tests for output formatting

### Phase 3: Loop Orchestration (REQ-030, REQ-032, REQ-033)
8. ✅ `LoopService` class with PLAN and BUILD phases
9. ✅ Integration with `ICopilotService` for streaming
10. ✅ Cancellation token handling (Ctrl+C)
11. ✅ Unit tests with mocked dependencies

### Phase 4: CLI Command (REQ-030)
12. ✅ `lopen loop` command in Program.cs
13. ✅ `lopen loop configure` subcommand
14. ✅ Interactive prompts (non-`--auto` mode)
15. ✅ Integration tests

### Phase 5: Verification (REQ-036)
16. ✅ `VerificationService` class
17. ✅ Verification prompt engineering
18. ✅ JSON response parsing
19. ✅ Integration with BUILD phase
20. ✅ Unit tests

### Phase 6: Documentation & Testing
21. ✅ Update AGENTS.md with loop learnings
22. ✅ Create user documentation (docs/loop-guide.md)
23. ✅ Self-test implementation
24. ✅ Manual end-to-end testing

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Copilot API changes | High | Abstract via `ICopilotService`, monitor SDK releases |
| Infinite loop (no done file) | Medium | Add max iteration limit, user can always Ctrl+C |
| File corruption | Medium | Backup before write, validate JSON on read |
| Large prompt files | Low | Stream reading, check file size limits |
| Permission issues | Low | Clear error messages, check paths early |
| Cancellation mid-write | Medium | Use transactions (write temp → rename) |

---

## Future Enhancements

### Not in MVP, but Designed For:
1. **Memory Module**: Persistent context beyond files (vector DB, embeddings)
2. **Parallel Jobs**: Multiple agents working independently
3. **Custom Verification Rules**: User-defined quality gates (`.lopen/verification.rules`)
4. **Loop Analytics**: Metrics dashboard (success rate, iteration time, cost tracking)
5. **Resume from Checkpoint**: Save mid-iteration state, resume exactly
6. **Multi-repo Loops**: Coordinate changes across repository boundaries
7. **Web Dashboard**: Real-time monitoring, pause/resume, manual intervention
8. **Notification System**: Email/Slack when loop completes or fails
9. **Custom Agents Library**: Shareable verification/research agent templates

### Extensibility Points:
- `ILoopStateManager`: Swap file-based for database-backed
- `ILoopOutputService`: Add web sockets, log files, structured JSON output
- `IVerificationService`: Plugin architecture for custom verifiers
- `LoopConfig`: Extend with new fields without breaking existing configs

---

## Conclusion

The Loop module implementation:
- ✅ Leverages existing `ICopilotService` abstraction
- ✅ Follows established patterns (System.CommandLine, Spectre.Console, async/await)
- ✅ Separates concerns (config, state, output, orchestration)
- ✅ Supports testing with interfaces and mocks
- ✅ Handles cancellation and errors gracefully
- ✅ Provides human-on-the-loop visibility via streaming
- ✅ Uses file-based state for simplicity and git-friendliness
- ✅ Enables autonomous agent behavior with verification gates

**Next Steps**:
1. Review this research document
2. Update `docs/requirements/jobs-to-be-done.json` with loop tasks
3. Begin Phase 1 implementation (LoopConfig, LoopStateManager)
4. Iterate through phases with tests
5. Self-test with bread recipe generation
6. Document learnings in AGENTS.md
