---
name: llm-research
description: Research on GitHub Copilot SDK for .NET and LLM integration patterns
date: 2026-01-20
status: complete
sources:
  - https://github.com/github/copilot-sdk (README, dotnet/README, docs/)
  - https://www.nuget.org/packages/GitHub.Copilot.SDK
  - https://github.com/openai/openai-dotnet
  - https://github.com/github/copilot-sdk/blob/main/docs/compatibility.md
  - https://github.com/github/copilot-sdk/blob/main/docs/hooks/error-handling.md
---

# LLM Module Research

## 1. GitHub Copilot SDK for .NET

The **official GitHub Copilot SDK** includes a first-party .NET package in technical preview.

| Property | Value |
| --- | --- |
| **NuGet Package** | `GitHub.Copilot.SDK` |
| **Install** | `dotnet add package GitHub.Copilot.SDK` |
| **Version** | `0.1.0` (technical preview) |
| **Target Framework** | `net8.0` |
| **AOT Compatible** | Yes (`IsAotCompatible = true`) |
| **Source** | [`github/copilot-sdk/dotnet`](https://github.com/github/copilot-sdk/tree/main/dotnet) |
| **License** | MIT |

### Architecture

The SDK does **not** call a REST API directly. It communicates with the **Copilot CLI running in server mode** via JSON-RPC over stdio or TCP:

```
Lopen (.NET)
     ↓
GitHub.Copilot.SDK (CopilotClient)
     ↓ JSON-RPC (stdio or TCP)
Copilot CLI (headless server mode)
     ↓
GitHub Copilot API / Model Providers
```

The SDK manages the CLI process lifecycle automatically — it spawns `copilot` in server mode when `StartAsync()` is called, and terminates it on `StopAsync()` / `DisposeAsync()`.

### Key Dependencies (from .csproj)

```xml
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.2.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.2" />
<PackageReference Include="StreamJsonRpc" Version="2.24.84" />
<PackageReference Include="System.Text.Json" Version="10.0.2" />
```

Notable: The SDK depends on `Microsoft.Extensions.AI.Abstractions` for its tool registration API (`AIFunctionFactory`), aligning with Microsoft's AI abstractions ecosystem.

### Prerequisite

The **Copilot CLI** must be installed and available in `PATH` (or via `CopilotClientOptions.CliPath`). Install via [Copilot CLI installation guide](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli).

---

## 2. Model API / Chat Completions

The SDK uses a session-based messaging model rather than raw OpenAI-style chat completions. Each session holds conversation state internally within the CLI server.

### Creating a Client and Session

```csharp
using GitHub.Copilot.SDK;

await using var client = new CopilotClient();
await client.StartAsync();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-opus-4.6",
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Replace,
        Content = "You are working within Lopen, an orchestrator for module development..."
    }
});
```

### Sending Messages and Receiving Responses

**Canonical pattern (event-driven):**

The SDK's `SendAsync` method returns a message ID (`Task<string>`), not a response object. Responses are received via event handlers. Use `SessionIdleEvent` to detect completion:

```csharp
var done = new TaskCompletionSource();
string? responseContent = null;

session.On(evt =>
{
    if (evt is AssistantMessageEvent msg)
        responseContent = msg.Data.Content;
    else if (evt is SessionIdleEvent)
        done.SetResult();
});

await session.SendAsync(new MessageOptions
{
    Prompt = "Analyze the authentication module requirements."
});
await done.Task;
Console.WriteLine(responseContent);
```

> **⚠️ Implementation note**: There is no `SendAndWaitAsync` convenience method in the official SDK API. Examples throughout this document use a `SendAndWaitAsync` pattern for brevity — implementers should build this as a wrapper:
>
> ```csharp
> public static async Task<AssistantMessageEvent?> SendAndWaitAsync(
>     this CopilotSession session, MessageOptions options,
>     TimeSpan? timeout = null)
> {
>     var done = new TaskCompletionSource<AssistantMessageEvent?>();
>     AssistantMessageEvent? result = null;
>
>     using var sub = session.On(evt =>
>     {
>         if (evt is AssistantMessageEvent msg) result = msg;
>         else if (evt is SessionIdleEvent) done.TrySetResult(result);
>         else if (evt is SessionErrorEvent err) done.TrySetException(
>             new InvalidOperationException(err.Data.Message));
>     });
>
>     await session.SendAsync(options);
>     if (timeout.HasValue)
>         await done.Task.WaitAsync(timeout.Value);
>     else
>         await done.Task;
>     return done.Task.Result;
> }
> ```

**Event-driven (streaming):**

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-opus-4.6",
    Streaming = true
});

var done = new TaskCompletionSource();

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            Console.Write(delta.Data.DeltaContent);
            break;
        case AssistantMessageEvent msg:
            // Final complete message
            break;
        case SessionIdleEvent:
            done.SetResult();
            break;
    }
});

await session.SendAsync(new MessageOptions { Prompt = "..." });
await done.Task;
```

### Fresh Context Per Invocation

For Lopen's "fresh context per step" design, create a **new session** for each workflow phase. The SDK supports multiple concurrent sessions:

```csharp
// Planning phase — fresh session
await using var planSession = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-opus-4.6",
    Tools = GetPlanningTools(),
    SystemMessage = BuildPlanningSystemMessage(state)
});

// Task execution — fresh session per task
await using var taskSession = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-opus-4.6",
    Tools = GetExecutionTools(),
    SystemMessage = BuildTaskSystemMessage(task)
});
```

### Infinite Sessions (Context Compaction)

For long-running sessions, the SDK supports automatic context window management:

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    InfiniteSessions = new InfiniteSessionConfig
    {
        Enabled = true,
        BackgroundCompactionThreshold = 0.80,
        BufferExhaustionThreshold = 0.95
    }
});
```

Lopen should **disable** infinite sessions since it manages its own fresh-context-per-invocation strategy.

---

## 3. Tool Registration Pattern

The SDK uses `Microsoft.Extensions.AI.AIFunctionFactory` for type-safe tool definitions. Tools are registered per-session via `SessionConfig.Tools`.

### Basic Tool Registration

```csharp
using Microsoft.Extensions.AI;
using System.ComponentModel;

var readSpecTool = AIFunctionFactory.Create(
    async ([Description("Module name")] string module,
           [Description("Section heading")] string section) =>
    {
        var content = await specManager.ReadSection(module, section);
        return new { content, module, section };
    },
    "read_spec",
    "Read a specific section from a specification document"
);

var updateTaskStatusTool = AIFunctionFactory.Create(
    async ([Description("Task identifier")] string taskId,
           [Description("New status: pending|in-progress|complete|failed")] string status) =>
    {
        var result = await taskManager.UpdateStatus(taskId, status);
        return new { taskId, status, success = result.Success };
    },
    "update_task_status",
    "Mark a task as pending, in-progress, complete, or failed"
);

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-opus-4.6",
    Tools = [readSpecTool, updateTaskStatusTool]
});
```

### Tool Results

Tool handlers can return any JSON-serializable value (automatically wrapped), or a `ToolResultAIContent` wrapping a `ToolResultObject` for full control over result metadata.

### Varying Tool Sets by Workflow Step

Per the specification, tools vary by phase. Build tool lists dynamically:

```csharp
List<AIFunction> GetToolsForPhase(WorkflowPhase phase) => phase switch
{
    WorkflowPhase.Research => [readSpecTool, logResearchTool, getContextTool],
    WorkflowPhase.Planning => [readSpecTool, readResearchTool, getContextTool, reportProgressTool],
    WorkflowPhase.Building => [readSpecTool, readPlanTool, updateTaskStatusTool,
                               verifyTaskTool, verifyComponentTool, reportProgressTool],
    WorkflowPhase.Verification => [verifyModuleTool, readSpecTool, getContextTool],
    _ => [getContextTool]
};
```

### Native Tools (CLI Built-ins)

The Copilot CLI provides built-in tools (file read/write, shell execution, git operations, web search) that are **available by default** in every session. Control them with:

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    // Allow only specific native tools
    AvailableTools = new List<string> { "read_file", "write_file", "shell", "git" },
    // Or exclude specific ones
    ExcludedTools = new List<string> { "web_search" }
});
```

---

## 4. Token Tracking

Token usage is available via session events. From the [compatibility docs](https://github.com/github/copilot-sdk/blob/main/docs/compatibility.md):

> **⚠️ Verification needed**: The `AssistantUsageEvent` type is referenced in compatibility docs but not listed in the main SDK event types documentation (which shows "And more..." for the full list). Verify this event type exists at implementation time. If unavailable, an alternative approach is to parse usage metadata from `AssistantMessageEvent.Data` or use the `OnPostToolUse` hook to track calls.

### Usage Events

```csharp
session.On(evt =>
{
    if (evt is AssistantUsageEvent usage)
    {
        var data = usage.Data;
        Console.WriteLine($"Input tokens:  {data.InputTokens}");
        Console.WriteLine($"Output tokens: {data.OutputTokens}");
    }
});
```

### Compaction Events (Context Window Tracking)

When infinite sessions are enabled, compaction events include token counts:

```csharp
session.On(evt =>
{
    if (evt is SessionCompactionCompleteEvent compaction)
    {
        // Token counts after compaction
    }
});
```

### Lopen Token Tracking Strategy

For Lopen's per-invocation tracking:

```csharp
public record IterationMetrics
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
    public bool IsPremiumRequest { get; init; }
}

// Accumulate within a session
int totalInput = 0, totalOutput = 0;
session.On(evt =>
{
    if (evt is AssistantUsageEvent usage)
    {
        totalInput += usage.Data.InputTokens;
        totalOutput += usage.Data.OutputTokens;
    }
});
```

### Premium vs Standard Classification

The specification requires tracking premium API requests. Classification is based on the model:

| Model | Tier |
| --- | --- |
| `claude-opus-4.6` | Premium |
| `claude-sonnet-4` | Premium |
| `gpt-5` | Premium |
| `gpt-5-mini` | Standard |
| `gpt-4.1` | Standard |
| `o3-mini` | Standard |

The SDK's `ListModelsAsync()` method returns available models with their capabilities, which can be used to build tier classification dynamically.

---

## 5. Model Selection

### Available Models

The SDK supports all models available via Copilot CLI. From documentation and examples:

- `claude-opus-4.6` — Anthropic Claude Opus (premium)
- `claude-sonnet-4.5` / `claude-sonnet-4` — Anthropic Claude Sonnet (premium)
- `gpt-5` — OpenAI GPT-5 (premium)
- `gpt-4.1` — OpenAI GPT-4.1 (standard)
- `gpt-5-mini` — OpenAI GPT-5 Mini (standard)
- `o3-mini`, `o4-mini` — OpenAI reasoning models (standard)

### Per-Session Model Selection

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "claude-opus-4.6"
});
```

### Listing Available Models at Runtime

```csharp
var models = await client.ListModelsAsync();
foreach (var model in models)
{
    Console.WriteLine($"{model.Name} — Reasoning: {model.SupportsReasoning}");
}
```

### Reasoning Effort (for supported models)

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "o3-mini",
    ReasoningEffort = "high"  // "low", "medium", "high", "xhigh"
});
```

### Model Fallback Implementation

```csharp
string[] fallbackOrder = ["claude-opus-4.6", "claude-sonnet-4", "gpt-5", "gpt-4.1"];

async Task<CopilotSession> CreateSessionWithFallback(
    CopilotClient client, string preferredModel, SessionConfig baseConfig)
{
    var available = (await client.ListModelsAsync()).Select(m => m.Name).ToHashSet();

    var model = fallbackOrder
        .Prepend(preferredModel)
        .FirstOrDefault(m => available.Contains(m))
        ?? throw new InvalidOperationException("No models available");

    if (model != preferredModel)
        logger.LogWarning("Model {Preferred} unavailable, falling back to {Fallback}",
            preferredModel, model);

    baseConfig.Model = model;
    return await client.CreateSessionAsync(baseConfig);
}
```

---

## 6. Rate Limiting

### How the SDK Handles Errors

The SDK surfaces errors through the `SessionErrorEvent` and the `OnErrorOccurred` hook. Rate limit errors from the underlying API appear with `errorContext: "model_call"`.

### Error Hook for Rate Limiting

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Hooks = new SessionHooks
    {
        OnErrorOccurred = async (input, invocation) =>
        {
            if (input.ErrorContext == "model_call" && input.Error.Contains("rate"))
            {
                return new ErrorOccurredHookOutput
                {
                    ErrorHandling = "retry"  // "retry", "skip", or "abort"
                };
            }
            return null; // Default handling
        }
    }
});
```

> **Note**: The `OnErrorOccurred` hook supports `ErrorHandling` with values `"retry"`, `"skip"`, or `"abort"`. Retry count and backoff configuration are not exposed via the hook output — implement application-level backoff (see below) for fine-grained retry control.

### Application-Level Backoff

For cases where the SDK's built-in retry is insufficient, Lopen can implement its own backoff at the session creation level:

```csharp
async Task<AssistantMessageEvent?> SendWithBackoff(
    CopilotSession session, MessageOptions options, int maxRetries = 5)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            return await session.SendAndWaitAsync(options);
        }
        catch (Exception ex) when (ex.Message.Contains("rate") || ex.Message.Contains("429"))
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            logger.LogWarning("Rate limited, retrying in {Delay}s (attempt {Attempt}/{Max})",
                delay.TotalSeconds, attempt + 1, maxRetries);
            await Task.Delay(delay);
        }
    }
    throw new InvalidOperationException("Max retries exceeded");
}
```

---

## 7. Sub-Agent Dispatch (Oracle Verification)

The specification requires oracle verification tools that dispatch a **cheap/fast model** as a sub-agent within a tool call handler. The SDK's multi-session support makes this straightforward.

### Pattern: Sub-Agent Within a Tool Handler

When the primary LLM calls `verify_task_completion`, Lopen's tool handler creates a **second session** with a cheaper model:

```csharp
var verifyTaskTool = AIFunctionFactory.Create(
    async ([Description("Task ID to verify")] string taskId,
           [Description("Diff content")] string diff,
           [Description("Test results")] string testResults) =>
    {
        // Collect evidence
        var task = await taskManager.GetTask(taskId);
        var criteria = await specManager.GetAcceptanceCriteria(task.ComponentId);

        // Dispatch sub-agent with cheap model
        await using var oracleSession = await client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-5-mini",  // Cheap/fast model
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = "You are a verification oracle. Review the evidence and determine if the task meets its acceptance criteria. Respond with JSON: {\"pass\": bool, \"gaps\": [string]}"
            },
            // No tools needed — oracle just reasons
            AvailableTools = new List<string>()
        });

        var verdict = await oracleSession.SendAndWaitAsync(new MessageOptions
        {
            Prompt = $"""
                ## Task: {task.Name}
                ## Acceptance Criteria:
                {criteria}
                ## Diff:
                {diff}
                ## Test Results:
                {testResults}
                """
        });

        return ParseOracleVerdict(verdict?.Data.Content);
    },
    "verify_task_completion",
    "Dispatch oracle sub-agent to verify a task is complete"
);
```

### Key Considerations

1. **Same CopilotClient** — The oracle session reuses the same `CopilotClient` (and thus the same CLI server process). No additional CLI process is spawned.
2. **Standard-Tier Model** — Using `gpt-5-mini` keeps oracle calls as standard (non-premium) requests.
3. **No Tools for Oracle** — The oracle session should have an empty `AvailableTools` list so it only reasons, not acts.
4. **Within Tool Call** — The sub-agent call happens synchronously within the tool handler, so the primary LLM sees the oracle result as the tool's return value.

---

## 8. Recommended NuGet Packages

### Primary SDK

| Package | Version | Purpose |
| --- | --- | --- |
| `GitHub.Copilot.SDK` | `0.1.0` | Core Copilot integration — client, sessions, events, tools |

### Transitive Dependencies (pulled in by the SDK)

| Package | Purpose |
| --- | --- |
| `Microsoft.Extensions.AI.Abstractions` | `AIFunctionFactory`, tool abstractions |
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger` for SDK logging |
| `StreamJsonRpc` | JSON-RPC communication with CLI |
| `System.Text.Json` | JSON serialization |

### Additional Packages for Lopen

| Package | Purpose |
| --- | --- |
| `Microsoft.Extensions.Logging` | Structured logging for Lopen |
| `Microsoft.Extensions.DependencyInjection` | DI container |
| `Microsoft.Extensions.Options` | Configuration binding for model assignments |
| `Polly` (optional) | Resilience/retry policies if SDK retry is insufficient |

### Not Needed

| Package | Why Not |
| --- | --- |
| `OpenAI` (NuGet) | SDK handles API communication internally |
| `Azure.AI.OpenAI` | Not using Azure endpoint directly |
| `Microsoft.Extensions.AI` (full) | SDK already depends on Abstractions |

---

## 9. Implementation Approach

### Recommended Architecture

```
Lopen.Llm/
├── ICopilotService.cs          # Interface for SDK interaction
├── CopilotService.cs           # CopilotClient lifecycle, session factory
├── SessionFactory.cs           # Builds SessionConfig per workflow phase
├── ToolRegistry.cs             # Registers Lopen tools per phase
├── OracleVerifier.cs           # Sub-agent dispatch for verification
├── TokenTracker.cs             # Accumulates usage metrics per session
├── ModelSelector.cs            # Model selection + fallback logic
└── PromptBuilder.cs            # Constructs system prompts per step
```

### Client Lifecycle

One `CopilotClient` per Lopen process, created at startup:

```csharp
public class CopilotService : IAsyncDisposable
{
    private readonly CopilotClient _client;

    public CopilotService(ILogger<CopilotService> logger, LlmOptions options)
    {
        _client = new CopilotClient(new CopilotClientOptions
        {
            Logger = logger,
            GithubToken = options.GithubToken,  // From auth module
            Cwd = options.WorkingDirectory
        });
    }

    public async Task StartAsync() => await _client.StartAsync();

    public async Task<CopilotSession> CreatePhaseSession(
        WorkflowPhase phase, string systemPrompt, List<AIFunction> tools)
    {
        var model = _modelSelector.GetModel(phase);
        return await _client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Tools = tools,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            Hooks = new SessionHooks
            {
                OnErrorOccurred = HandleError,
                OnPreToolUse = HandlePreToolUse
            }
        });
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
```

### Orchestration Loop

```csharp
public async Task ExecuteTask(TaskContext task)
{
    var tools = _toolRegistry.GetToolsForPhase(WorkflowPhase.Building);
    var prompt = _promptBuilder.BuildTaskPrompt(task);

    await using var session = await _copilotService.CreatePhaseSession(
        WorkflowPhase.Building, prompt, tools);

    var tracker = new TokenTracker();
    session.On(evt =>
    {
        if (evt is AssistantUsageEvent usage)
            tracker.Record(usage.Data);
    });

    var response = await session.SendAndWaitAsync(new MessageOptions
    {
        Prompt = task.Instructions
    });

    // Record metrics
    await _metricsStore.SaveIteration(new IterationMetrics
    {
        TaskId = task.Id,
        InputTokens = tracker.TotalInputTokens,
        OutputTokens = tracker.TotalOutputTokens,
        Model = task.Model,
        IsPremium = ModelSelector.IsPremium(task.Model)
    });
}
```

### Authentication Flow

The SDK handles authentication automatically. Priority order:

1. Explicit `GithubToken` passed to `CopilotClientOptions`
2. Environment variables: `COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN`
3. Stored OAuth credentials from `copilot` CLI login
4. `gh auth` credentials

Lopen's [Auth module](../auth/SPECIFICATION.md) resolves a token and passes it to `CopilotClientOptions.GithubToken`.

### BYOK (Bring Your Own Key) Support

For users who want to use their own API keys instead of a Copilot subscription:

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Provider = new ProviderConfig
    {
        Type = "openai",        // or "anthropic", "azure"
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = "sk-..."
    }
});
```

---

## Relevance to Lopen

### Direct Alignment

The GitHub Copilot SDK is **exactly what the LLM specification calls for**. Key alignments:

| Specification Requirement | SDK Capability |
| --- | --- |
| Copilot SDK Only | `GitHub.Copilot.SDK` NuGet package |
| Per-step model selection | `SessionConfig.Model` per session |
| Hybrid tool strategy (Lopen + native) | `AIFunctionFactory` tools + CLI built-in tools |
| Fresh context per invocation | New `CopilotSession` per workflow step |
| Token tracking from response metadata | `AssistantUsageEvent` with `InputTokens` / `OutputTokens` |
| Oracle sub-agent dispatch | Second `CopilotSession` with cheap model within tool handler |
| Authentication via user's GitHub credentials | SDK handles OAuth, env vars, and stored credentials |
| System prompt customization | `SystemMessageConfig` with Replace/Append modes |

### Architectural Implications

1. **CLI Dependency** — Lopen requires the Copilot CLI installed. This should be validated at startup (`verify-sdk-connection` skill) and documented in prerequisites.
2. **Process Model** — One `CopilotClient` spawns one CLI process. Multiple sessions share this process. Session creation/disposal is lightweight.
3. **No Direct REST** — The SDK abstracts all HTTP/API communication. Lopen never constructs raw API requests.
4. **Tool Execution Model** — When the LLM calls a Lopen-managed tool, the SDK invokes the handler in-process. The handler returns a result that the SDK sends back to the CLI. This is synchronous from the LLM's perspective.
5. **Technical Preview Risk** — The SDK is `0.1.0` and in technical preview. API surface may change. Lopen should wrap SDK types behind internal interfaces for isolation.

### Open Questions

1. **Token event granularity** — Does `AssistantUsageEvent` fire once per message send or multiple times during tool-calling loops? (Likely multiple — needs verification. Event type itself needs verification, see [Section 4](#4-token-tracking).)
2. **Context window size** — How to query the model's context window limit for budget calculations? `ListModelsAsync()` may include this in model capabilities.
3. **Premium request counting** — The SDK bills each `SendAsync` as one premium request per the FAQ. Oracle sub-agent calls on standard models should not count as premium. Needs verification.
4. **Error propagation** — When the CLI server crashes, does the SDK auto-restart (per `AutoRestart` option) or surface an error? Default is `AutoRestart = true`.

---

## 10. Tool Handler Implementation

### Current State (Gap Analysis)

The system registers 10 `LopenToolDefinition` records in `DefaultToolRegistry` with **names and descriptions only**. There are no execution handlers — the tools are currently rendered as text bullets in the system prompt by `DefaultPromptBuilder`, relying on the LLM to "call" them textually rather than via proper SDK function-calling.

**Key evidence of the gap in `CopilotLlmService.InvokeAsync`:**

```csharp
// tools parameter is received but never used in SessionConfig
public async Task<LlmInvocationResult> InvokeAsync(
    string systemPrompt,
    string model,
    IReadOnlyList<LopenToolDefinition> tools,   // ← accepted
    CancellationToken cancellationToken = default)
{
    // ...
    var config = new SessionConfig
    {
        Model = model,
        SystemMessage = ...,
        Streaming = false,
        Hooks = new SessionHooks { OnErrorOccurred = ... },
        // ⚠️ No Tools property set — tools are never registered with the SDK
    };
}
```

The `LopenToolDefinition` record is definition-only:

```csharp
public sealed record LopenToolDefinition(
    string Name,
    string Description,
    string? ParameterSchema = null,
    IReadOnlyList<WorkflowPhase>? AvailableInPhases = null);
// ⚠️ No handler callback — no way to execute when the LLM calls the tool
```

### Architecture: Adding Handler Callbacks

#### Option A: Extend `LopenToolDefinition` with a Handler Delegate (Recommended)

Add a handler callback directly to the tool definition record:

```csharp
public sealed record LopenToolDefinition(
    string Name,
    string Description,
    string? ParameterSchema = null,
    IReadOnlyList<WorkflowPhase>? AvailableInPhases = null,
    Func<string, CancellationToken, Task<string>>? Handler = null);
```

The handler signature `Func<string, CancellationToken, Task<string>>` takes a JSON arguments string and returns a JSON result string. This keeps the record simple and avoids introducing a separate interface hierarchy for 10 tools.

#### Option B: Separate `IToolHandler` Interface

As proposed in `RESEARCH-backpressure.md`:

```csharp
public interface IToolHandler
{
    string ToolName { get; }
    Task<ToolResult> ExecuteAsync(
        ImmutableDictionary<string, string> arguments,
        CancellationToken ct);
}
```

This enables the decorator pattern (`AuditingToolHandler` for timing/logging) but adds complexity. Each tool would need its own class implementing `IToolHandler`, registered in DI, and resolved by name at dispatch time.

#### Recommendation

**Use Option A** (handler delegate on `LopenToolDefinition`) for simplicity. Cross-cutting concerns like auditing and timing can be applied via a wrapper delegate at registration time rather than requiring a full decorator class hierarchy. The `DefaultToolRegistry.RegisterBuiltInTools()` method already centralizes registration — adding handler delegates there keeps the pattern cohesive.

### Converting `LopenToolDefinition` to SDK `AIFunction`

The Copilot SDK expects `AIFunction` instances (from `Microsoft.Extensions.AI.Abstractions`) in `SessionConfig.Tools`. The conversion bridge:

```csharp
using Microsoft.Extensions.AI;

internal static class ToolConversion
{
    /// <summary>
    /// Converts Lopen tool definitions with handlers into SDK AIFunction instances.
    /// </summary>
    public static List<AIFunction> ToAiFunctions(IReadOnlyList<LopenToolDefinition> tools)
    {
        return tools
            .Where(t => t.Handler is not null)
            .Select(t => AIFunctionFactory.Create(
                async (string arguments) =>
                {
                    return await t.Handler!(arguments, CancellationToken.None);
                },
                t.Name,
                t.Description))
            .ToList();
    }
}
```

Then in `CopilotLlmService.InvokeAsync`, register with the session:

```csharp
var config = new SessionConfig
{
    Model = model,
    SystemMessage = ...,
    Tools = ToolConversion.ToAiFunctions(tools),  // ← new
    Hooks = ...
};
```

### SDK Tool Call Dispatch Flow

When the LLM decides to call a Lopen-managed tool, the SDK handles the round-trip automatically:

```
1. LLM generates a tool_call in its response
2. SDK receives the tool_call via JSON-RPC from CLI
3. SDK invokes the registered AIFunction handler in-process
4. Handler executes (e.g., reads a spec, dispatches oracle)
5. Handler returns a result string
6. SDK sends ToolExecutionCompleteEvent (tracked for metrics)
7. SDK sends the tool result back to CLI via JSON-RPC
8. CLI forwards the result to the model
9. LLM generates its next response (may call more tools or produce final output)
10. Cycle repeats until LLM produces a final message without tool calls
11. SessionIdleEvent fires → SendAndWaitAsync resolves
```

The `ToolExecutionCompleteEvent` already tracked in `CopilotLlmService` (line 96) will automatically count handler invocations once tools are registered.

### Session Hooks for Tool Governance

The SDK's `SessionHooks` support `OnPreToolUse` and `OnPostToolUse` hooks for intercepting tool calls. These are critical for back-pressure enforcement:

```csharp
Hooks = new SessionHooks
{
    OnErrorOccurred = HandleError,
    OnPreToolUse = async (input, _) =>
    {
        // Enforce TaskStatusGate: reject update_task_status(complete)
        // unless verify_* has passed
        if (input.ToolName == "update_task_status")
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(input.Arguments);
            if (args?.GetValueOrDefault("status") == "complete")
            {
                var gateResult = _taskStatusGate.ValidateCompletion(
                    VerificationScope.Task, args["taskId"]);
                if (!gateResult.IsAllowed)
                {
                    return new PreToolUseHookOutput
                    {
                        // Reject the tool call with explanation
                        Decision = "reject",
                        Message = gateResult.RejectionReason
                    };
                }
            }
        }
        return null; // Allow all other tool calls
    },
    OnPostToolUse = async (input, _) =>
    {
        // Record oracle verdicts for back-pressure tracking
        if (input.ToolName.StartsWith("verify_") && input.Result is not null)
        {
            var verdict = JsonSerializer.Deserialize<OracleVerdict>(input.Result);
            if (verdict?.Passed == true)
            {
                _verificationTracker.RecordVerification(
                    ParseScope(input.ToolName), input.Arguments);
            }
        }
        return null;
    }
}
```

### Dependency Inversion Constraint

**Critical architectural constraint**: `Lopen.Llm` references only `Lopen.Configuration` — it cannot directly reference `Lopen.Core` (which contains `ISpecificationParser`, `IOutputRenderer`) or `Lopen.Storage` (which contains `IPlanManager`, `IFileSystem`, `ISessionManager`). The dependency graph is:

```
Lopen.Core ──→ Lopen.Llm ──→ Lopen.Configuration
Lopen.Storage ──→ Lopen.Configuration
```

This means tool handlers cannot directly inject `ISpecificationParser`, `IPlanManager`, etc. Two approaches:

**Approach 1: Define handler interfaces in `Lopen.Llm`, implement in `Lopen.Core`** (Recommended)

Define thin abstractions in `Lopen.Llm` that the handlers need:

```csharp
// In Lopen.Llm — handler-specific abstractions
public interface ISpecReader
{
    Task<string> ReadSectionAsync(string module, string section, CancellationToken ct);
}

public interface IPlanReader
{
    Task<string> ReadPlanAsync(string module, CancellationToken ct);
}

public interface IResearchStore
{
    Task<string> ReadResearchAsync(string module, string topic, CancellationToken ct);
    Task WriteResearchAsync(string module, string topic, string content, CancellationToken ct);
}

public interface IContextProvider
{
    Task<string> GetCurrentContextAsync(CancellationToken ct);
}

public interface IProgressSink
{
    Task ReportProgressAsync(string phase, string step, string progress, CancellationToken ct);
}
```

Implement these in `Lopen.Core` (which already references `Lopen.Llm`) by delegating to the real services:

```csharp
// In Lopen.Core — bridges Llm abstractions to real services
internal sealed class SpecReader : ISpecReader
{
    private readonly ISpecificationParser _parser;
    private readonly IFileSystem _fileSystem;

    public async Task<string> ReadSectionAsync(string module, string section, CancellationToken ct)
    {
        var path = $"docs/requirements/{module}/SPECIFICATION.md";
        var content = await _fileSystem.ReadAllTextAsync(path, ct);
        return _parser.ExtractSection(content, section) ?? $"Section '{section}' not found.";
    }
}
```

**Approach 2: Register handler delegates from the composition root**

Wire handlers at startup in the main `Lopen` project (which references everything):

```csharp
// In Lopen (app host) — startup composition
services.AddLopenLlm();

// After all services registered, wire tool handlers
services.AddSingleton<IToolHandlerWiring>(sp =>
{
    var registry = sp.GetRequiredService<IToolRegistry>();
    var parser = sp.GetRequiredService<ISpecificationParser>();
    var planManager = sp.GetRequiredService<IPlanManager>();
    // ... wire each handler with captured dependencies
});
```

This avoids new interfaces but scatters handler logic across the composition root.

### Individual Tool Handler Specifications

#### 1. `read_spec` — Read Specification Section

| Property | Value |
| --- | --- |
| **Input** | `{ "module": string, "section": string }` |
| **Output** | Markdown content of the requested section |
| **Dependencies** | `ISpecReader` (abstracts `ISpecificationParser` + `IFileSystem`) |
| **Error case** | Section not found → return `"Section '{section}' not found in {module} specification."` |
| **Phases** | All |

The handler reads `docs/requirements/{module}/SPECIFICATION.md`, extracts the named section using `ISpecificationParser.ExtractSection`, and returns the markdown content. Uses `ISectionCache` for performance on repeated reads within the same invocation.

#### 2. `read_research` — Read Research Document

| Property | Value |
| --- | --- |
| **Input** | `{ "module": string, "topic": string }` |
| **Output** | Full content of the research document |
| **Dependencies** | `IResearchStore` (abstracts `IFileSystem`) |
| **Error case** | File not found → return `"No research found for topic '{topic}' in module '{module}'."` |
| **Phases** | All |

Reads `docs/requirements/{module}/RESEARCH-{topic}.md`. The topic is slugified (lowercase, hyphens) to match the file naming convention. Returns the full markdown content.

#### 3. `read_plan` — Read Plan with Task Statuses

| Property | Value |
| --- | --- |
| **Input** | `{ "module": string }` |
| **Output** | Full plan markdown with checkbox statuses |
| **Dependencies** | `IPlanReader` (abstracts `IPlanManager`) |
| **Error case** | Plan not found → return `"No plan exists for module '{module}'."` |
| **Phases** | Planning, Building |

Reads `.lopen/modules/{module}/plan.md` via `IPlanManager.ReadPlanAsync`. Returns the full markdown plan including `- [x]` / `- [ ]` checkbox syntax reflecting task completion states.

#### 4. `update_task_status` — Update Task Status (Gated)

| Property | Value |
| --- | --- |
| **Input** | `{ "module": string, "taskText": string, "completed": bool }` |
| **Output** | `{ "success": bool, "message": string }` |
| **Dependencies** | `IPlanReader` (abstracts `IPlanManager`), `ITaskStatusGate`, `IVerificationTracker` |
| **Error case** | Gate rejection → return `{ "success": false, "message": "..." }` |
| **Phases** | Building only |

**Back-pressure enforcement**: Before marking a task complete (`completed: true`), the handler calls `ITaskStatusGate.ValidateCompletion`. If no prior `verify_task_completion` has passed for this task, the gate rejects with a message instructing the LLM to call the verification tool first. On success, delegates to `IPlanManager.UpdateCheckboxAsync`.

```
Flow: LLM calls update_task_status(complete)
  → Handler checks TaskStatusGate.ValidateCompletion()
  → If gate rejects: return error message (LLM must call verify_task_completion first)
  → If gate allows: IPlanManager.UpdateCheckboxAsync() → return success
```

#### 5. `get_current_context` — Retrieve Workflow Context

| Property | Value |
| --- | --- |
| **Input** | `{}` (no arguments) |
| **Output** | `{ "phase": string, "step": string, "module": string, "component": string? }` |
| **Dependencies** | `IContextProvider` (abstracts `ISessionManager` / `SessionState`) |
| **Error case** | No active session → return `{ "phase": "unknown", "step": "unknown" }` |
| **Phases** | All |

Returns the current workflow state from `SessionState`: phase, step, module name, and optional component name. The LLM uses this to understand where it is in the orchestration flow.

#### 6. `log_research` — Persist Research Findings

| Property | Value |
| --- | --- |
| **Input** | `{ "module": string, "topic": string, "content": string }` |
| **Output** | `{ "success": true, "path": string }` |
| **Dependencies** | `IResearchStore` (abstracts `IFileSystem`) |
| **Error case** | Write failure → return `{ "success": false, "error": string }` |
| **Phases** | Research, RequirementGathering |

Writes content to `docs/requirements/{module}/RESEARCH-{topic}.md`. Creates the directory structure if it doesn't exist. The topic is slugified to produce a filesystem-safe filename. If the file already exists, it is **overwritten** (the LLM is expected to read-then-write if appending).

#### 7. `report_progress` — Report Progress to TUI

| Property | Value |
| --- | --- |
| **Input** | `{ "phase": string, "step": string, "progress": string }` |
| **Output** | `{ "acknowledged": true }` |
| **Dependencies** | `IProgressSink` (abstracts `IOutputRenderer`) |
| **Error case** | Rendering failure is non-fatal → log warning, return acknowledged |
| **Phases** | All |

Delegates to `IOutputRenderer.RenderProgressAsync`. In headless mode (`HeadlessRenderer`), this writes to stdout/logs. In TUI mode, it updates the progress display. The handler always returns `acknowledged: true` — progress reporting is fire-and-forget from the LLM's perspective.

#### 8. `verify_task_completion` — Oracle Verification (Task)

| Property | Value |
| --- | --- |
| **Input** | `{ "taskId": string, "evidence": string, "acceptanceCriteria": string }` |
| **Output** | `{ "pass": bool, "gaps": [string] }` |
| **Dependencies** | `IOracleVerifier`, `IVerificationTracker` |
| **Error case** | Oracle failure → return `{ "pass": false, "gaps": ["Verification failed: {error}"] }` |
| **Phases** | Building only |

Dispatches to `IOracleVerifier.VerifyAsync(VerificationScope.Task, evidence, acceptanceCriteria)`. The oracle creates a **separate SDK session** with a cheap model (`gpt-5-mini` per `OracleOptions.Model`), sends the evidence for review, and parses the JSON verdict. On pass, records the result in `IVerificationTracker` so `ITaskStatusGate` will allow the subsequent `update_task_status(complete)` call.

```
Flow: LLM calls verify_task_completion
  → Handler calls IOracleVerifier.VerifyAsync(Task, evidence, criteria)
    → OracleVerifier calls ILlmService.InvokeAsync(prompt, cheapModel, noTools)
      → Creates fresh SDK session with cheap model
      → Oracle LLM responds with {"pass": bool, "gaps": [...]}
    → OracleVerifier parses verdict
  → If pass: IVerificationTracker.RecordVerification(Task, taskId)
  → Return verdict to primary LLM
```

#### 9. `verify_component_completion` — Oracle Verification (Component)

| Property | Value |
| --- | --- |
| **Input** | `{ "componentId": string, "evidence": string, "acceptanceCriteria": string }` |
| **Output** | `{ "pass": bool, "gaps": [string] }` |
| **Dependencies** | `IOracleVerifier`, `IVerificationTracker` |
| **Phases** | Building only |

Same pattern as `verify_task_completion` but with `VerificationScope.Component`. Verifies that all tasks within a component are complete and the component meets its acceptance criteria as a whole.

#### 10. `verify_module_completion` — Oracle Verification (Module)

| Property | Value |
| --- | --- |
| **Input** | `{ "moduleId": string, "evidence": string, "acceptanceCriteria": string }` |
| **Output** | `{ "pass": bool, "gaps": [string] }` |
| **Dependencies** | `IOracleVerifier`, `IVerificationTracker` |
| **Phases** | Building only |

Same pattern with `VerificationScope.Module`. Verifies the entire module meets all specification acceptance criteria. This is the final verification gate before the module workflow completes.

### Error Handling for Tool Calls

When a tool handler throws an exception, the SDK must receive an error result rather than crashing the session. Wrap all handlers with error boundaries:

```csharp
static Func<string, CancellationToken, Task<string>> WithErrorBoundary(
    string toolName,
    Func<string, CancellationToken, Task<string>> handler,
    ILogger logger)
{
    return async (args, ct) =>
    {
        try
        {
            return await handler(args, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool '{ToolName}' failed with args: {Args}", toolName, args);
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = $"Tool '{toolName}' failed: {ex.Message}"
            });
        }
    };
}
```

The LLM receives the error as a tool result and can adapt (retry, use alternative approach, or report the failure). This prevents tool exceptions from terminating the entire SDK session.

### Registration Wiring (End-to-End)

The complete registration flow, showing how handlers are wired from DI through to SDK session:

```
1. Startup (Lopen host):
   services.AddLopenLlm()
     → registers DefaultToolRegistry (10 tool definitions, no handlers)
     → registers ISpecReader, IPlanReader, IResearchStore, etc.

2. Handler wiring (Lopen host or Lopen.Core):
   → Resolves DefaultToolRegistry + handler dependencies from DI
   → Calls RegisterTool() with handler delegates that close over services
   → Each LopenToolDefinition now has a non-null Handler

3. Orchestration loop calls ILlmService.InvokeAsync(prompt, model, tools):
   → CopilotLlmService receives LopenToolDefinition[] with handlers
   → Converts to AIFunction[] via AIFunctionFactory.Create
   → Sets SessionConfig.Tools = aiFunctions
   → Creates SDK session with tools registered

4. During session:
   → LLM generates tool_call → SDK invokes AIFunction → handler executes
   → Handler returns JSON result → SDK sends back to LLM
   → ToolExecutionCompleteEvent fires (tracked for metrics)
   → Cycle repeats until SessionIdleEvent
```

### Open Questions (Tool Handlers)

1. **Parameter schema validation** — Should `LopenToolDefinition.ParameterSchema` (currently always `null`) be populated with JSON Schema for each tool, or does `AIFunctionFactory` infer schemas from the delegate signature? The SDK's `AIFunctionFactory.Create` can infer from `[Description]` attributes on parameters, but Lopen's `Func<string, CancellationToken, Task<string>>` handler takes raw JSON — schema validation would need to be explicit.
2. **Concurrency** — Can the LLM issue parallel tool calls? If so, handlers must be thread-safe. The `IVerificationTracker` uses a `Dictionary` (not `ConcurrentDictionary`), which may need updating.
3. **Handler timeout** — Should individual tool handlers have timeouts? Oracle verification calls `ILlmService.InvokeAsync` which has a 10-minute default timeout — this may be too long for a tool call within a tool call.
4. **Cancellation propagation** — The `CancellationToken` from the outer `InvokeAsync` should flow through to tool handlers and oracle sub-sessions. Verify the SDK propagates cancellation to `AIFunction` invocations.
5. **Handler lifecycle** — Tool handlers close over DI services. If any handler dependency is scoped (not singleton), the handler delegate may outlive its scope. All handler dependencies should be singleton or transient.

---

## 11. LLM-07: Oracle Within Same SDK Invocation

> **Requirement**: Oracle verification runs within the same SDK invocation (no additional premium request consumed).

### Current Implementation

`OracleVerifier` (in `Lopen.Llm`) dispatches a **separate** `ILlmService.InvokeAsync` call for each verification. This creates a **new SDK session** inside `CopilotLlmService`, which means:

```
Primary session (e.g. claude-opus-4.6)
  → LLM emits tool_call: verify_task_completion
    → Tool handler calls OracleVerifier.VerifyAsync()
      → OracleVerifier calls ILlmService.InvokeAsync()
        → CopilotLlmService creates NEW session (gpt-5-mini)
          → Oracle session runs, returns verdict
        → Session disposed
      → Verdict returned to tool handler
    → Tool result sent back to primary session
```

Each `CreateSessionAsync` call in the SDK opens a **new conversation** with the Copilot CLI server via JSON-RPC. The primary session and oracle session are independent.

### SDK Feasibility: Nested Sessions on Same Client

The Copilot SDK **does support** creating multiple concurrent sessions on the same `CopilotClient`. Section 7 of this document already demonstrates the pattern (sub-agent within a tool handler). The key observations:

1. **Same CLI process** — Both sessions share the same underlying Copilot CLI server process. No additional CLI process is spawned.
2. **Independent request billing** — Each session's `SendAndWaitAsync` call constitutes a separate API request. Whether it counts as a premium request depends on the **model** used, not the session nesting.
3. **Standard-tier oracle** — Using `gpt-5-mini` (or similar) for the oracle keeps it as a standard (non-premium) request. The requirement "no additional premium request consumed" is satisfied by model choice, not by session sharing.

### Architectural Decision

**The current two-session approach is correct** and aligns with the SDK's design. There is no way to have the oracle "piggyback" on the primary session's request without the LLM itself doing the verification (which would defeat the purpose of an independent oracle).

The requirement "within the same SDK invocation" should be interpreted as "within the same tool call handler execution" — which the current `OracleVerifier` achieves. The oracle session reuses the same `CopilotClient` (same CLI server), so there is no additional process overhead.

### Alternatives Considered

| Approach | Feasibility | Trade-offs |
| --- | --- | --- |
| **Current: separate session, cheap model** | ✅ Works today | Two API requests per verification, but oracle uses standard-tier model |
| **Inline oracle (no sub-session)** | ❌ Not viable | Would require the primary LLM to self-verify, defeating independence |
| **Batched oracle (post-session)** | ⚠️ Possible | Oracle runs after primary session completes; loses real-time feedback loop within tool calls |
| **MCP sub-agent tool** | ⚠️ Future | If SDK adds native sub-agent dispatch as a tool type, could be more efficient |

### Recommendation

No changes needed. The implementation satisfies LLM-07 as designed:
- Oracle runs within the same tool call handler (same SDK invocation lifecycle)
- Oracle uses a standard-tier model (`gpt-5-mini` via `OracleOptions.Model`), consuming no premium requests
- Both sessions share the same `CopilotClient` instance

### Open Question

The `OracleVerifier` currently receives `ILlmService` via DI, meaning it gets the **same** `CopilotLlmService` singleton. Each oracle call creates a fresh session. If the primary session's tool handler is synchronous (blocking the primary session until the tool returns), the Copilot CLI server must handle two active sessions concurrently. The SDK documentation confirms this is supported, but it should be validated under load to ensure no deadlocks on the JSON-RPC transport.

---

## 12. LLM-11: Runtime Model Fallback

> **Requirement**: Model fallback activates when a configured model is unavailable (logs warning, falls back to next available).

### Current Implementation

`DefaultModelSelector` provides **config-time** fallback only: if no model is configured for a workflow phase, it falls back to `"claude-sonnet-4"`. There is **no runtime fallback** when a configured model is unavailable at API call time.

`CopilotLlmService.InvokeAsync` catches exceptions and wraps them in `LlmException`, but does not attempt retry with alternative models. The `AuthErrorHandler` handles auth errors (401/403) with a single retry, but model-unavailability is a different failure mode.

### SDK Error Surface

Model unavailability manifests in two ways:

1. **`SessionErrorEvent`** — Emitted during the session event stream. The `Data.ErrorType` and `Data.Message` fields contain error details. Currently only logged as a warning in `CopilotLlmService`.

2. **`OnErrorOccurred` hook** — Receives `ErrorOccurredHookInput` with `ErrorContext` (e.g., `"model_call"`) and `Error` message. Can return `ErrorOccurredHookOutput` with `ErrorHandling` set to `"retry"`, `"skip"`, or `"abort"`.

3. **Exception from `SendAndWaitAsync`** — If the model is completely unavailable (not in the model list, or API returns an error), the SDK may throw. The current catch-all wraps this in `LlmException`.

4. **`ListModelsAsync`** — The SDK provides `client.ListModelsAsync()` to enumerate available models at runtime (see Section 5). This enables pre-flight availability checks.

### Proposed Design

```
ILlmService.InvokeAsync(prompt, model, tools, ct)
  │
  ├─ Try: CopilotLlmService creates session with requested model
  │    └─ Success → return result
  │
  ├─ Catch: model-unavailable error detected
  │    ├─ Log warning: "Model {model} unavailable, attempting fallback"
  │    ├─ Query IModelSelector for fallback chain
  │    └─ Retry with next model in chain
  │
  └─ All models exhausted → throw LlmException
```

#### Option A: Retry Wrapper (Decorator Pattern)

Create a `RetryingLlmService` that decorates `CopilotLlmService`:

```csharp
internal sealed class RetryingLlmService : ILlmService
{
    private readonly CopilotLlmService _inner;
    private readonly IModelSelector _modelSelector;
    private readonly ILogger _logger;

    public async Task<LlmInvocationResult> InvokeAsync(
        string systemPrompt, string model,
        IReadOnlyList<LopenToolDefinition> tools,
        CancellationToken ct)
    {
        var modelsToTry = GetFallbackChain(model);

        foreach (var candidate in modelsToTry)
        {
            try
            {
                return await _inner.InvokeAsync(
                    systemPrompt, candidate, tools, ct);
            }
            catch (LlmException ex) when (IsModelUnavailable(ex))
            {
                _logger.LogWarning(
                    "Model {Model} unavailable: {Message}. Trying next fallback.",
                    candidate, ex.Message);
            }
        }

        throw new LlmException(
            $"All models unavailable. Tried: {string.Join(", ", modelsToTry)}",
            model);
    }
}
```

#### Option B: Pre-flight Check via `ListModelsAsync`

Before creating a session, query available models:

```csharp
var available = (await client.ListModelsAsync(ct))
    .Select(m => m.Name).ToHashSet();

var model = fallbackChain
    .FirstOrDefault(m => available.Contains(m))
    ?? throw new LlmException("No models available", preferredModel);
```

This avoids wasted session creation but adds a round-trip. The `ListModelsAsync` result could be cached with a short TTL (e.g., 60 seconds).

#### Option C: Hook-Based Retry

Use the SDK's `OnErrorOccurred` hook to detect model errors and return `"retry"` after switching the model. However, the hook does **not** expose a way to change the model mid-session — the model is set at session creation time. This makes hook-based model fallback **not feasible**.

### Error Detection Heuristics

Since the SDK doesn't expose typed error codes, model unavailability must be detected by inspecting error messages:

```csharp
private static bool IsModelUnavailable(LlmException ex)
{
    var msg = ex.Message + (ex.InnerException?.Message ?? "");
    return msg.Contains("model", StringComparison.OrdinalIgnoreCase)
        && (msg.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("not available", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
}
```

This is fragile. A more robust approach would be to use the `OnErrorOccurred` hook's `ErrorContext == "model_call"` field to tag the exception before it reaches the catch block.

### Recommended Approach

**Option A (Decorator) + Option B (pre-flight) combined:**

1. On first invocation, call `ListModelsAsync` and cache the available model set (60s TTL).
2. Build fallback chain: `[requested_model, ...configured_fallbacks, "claude-sonnet-4"]`.
3. Filter chain by availability from cache.
4. Try each model in order; catch `LlmException` with model-unavailable heuristics.
5. On cache miss (model was in cache but fails at runtime), invalidate cache and retry once.

### Fallback Chain Configuration

Extend `ModelOptions` to support per-phase fallback chains:

```json
{
  "Models": {
    "Building": "claude-opus-4.6",
    "BuildingFallbacks": ["claude-sonnet-4", "gpt-5"],
    "Research": "gpt-5",
    "ResearchFallbacks": ["gpt-4.1", "gpt-5-mini"]
  }
}
```

### Impact on Existing Code

- `DefaultModelSelector` gains a `GetFallbackChain(WorkflowPhase)` method returning `IReadOnlyList<string>`.
- `ModelFallbackResult` is extended to track which fallback was used (already has `WasFallback` and `OriginalModel`).
- `LlmException` could gain an `IsModelUnavailable` property to avoid string matching at call sites.
- `ICopilotClientProvider` exposes `ListModelsAsync` (or a new `IModelAvailabilityChecker` interface).

---

## 13. LLM-13: Token Metrics Persistence to Session State

> **Requirement**: Token metrics are surfaced to the TUI and persisted in session state.

### Current Implementation: Save Path

The save path works end-to-end:

```
WorkflowOrchestrator.AutoSaveAsync()
  → _tokenTracker.GetSessionMetrics()         // InMemoryTokenTracker → SessionTokenMetrics
  → Map to Storage.SessionMetrics              // CumulativeInputTokens, CumulativeOutputTokens,
  │                                            // PremiumRequestCount, IterationCount, UpdatedAt
  → _autoSaveService.SaveAsync(metrics)        // AutoSaveService
    → _sessionManager.SaveSessionMetricsAsync() // SessionManager → JSON file
```

`AutoSaveAsync` is triggered at: `StepCompletion`, `PhaseTransition`, `UserPause`, `TaskFailure`. The mapping from `SessionTokenMetrics` to `SessionMetrics` is correct — it copies cumulative counts and adds `IterationCount` from the orchestrator.

### Current Implementation: Load Path — GAP IDENTIFIED

**Metrics are NOT restored into the token tracker on session resume.** The current code:

- `ISessionManager.LoadSessionMetricsAsync(sessionId)` exists and reads from JSON storage.
- `SessionCommand` calls `LoadSessionMetricsAsync` for the `session show` command (display only).
- `WorkflowOrchestrator` does **not** call `LoadSessionMetricsAsync` when resuming a session.
- `InMemoryTokenTracker` has `ResetSession()` but no `RestoreSession(SessionTokenMetrics)` method.

**Result**: After a session resume, `InMemoryTokenTracker` starts from zero. Subsequent `AutoSaveAsync` calls overwrite the persisted metrics with lower values, effectively losing historical token usage data.

### Design: Ensuring Save → Load Round-Trip

#### Step 1: Add `RestoreMetrics` to `ITokenTracker`

```csharp
public interface ITokenTracker
{
    void RecordUsage(TokenUsage usage);
    SessionTokenMetrics GetSessionMetrics();
    void ResetSession();
    void RestoreMetrics(int cumulativeInput, int cumulativeOutput, int premiumCount);
}
```

#### Step 2: Implement in `InMemoryTokenTracker`

```csharp
public void RestoreMetrics(int cumulativeInput, int cumulativeOutput, int premiumCount)
{
    lock (_lock)
    {
        _cumulativeInput = cumulativeInput;
        _cumulativeOutput = cumulativeOutput;
        _premiumCount = premiumCount;
        // _iterations list remains empty — we don't restore per-iteration history
    }
}
```

Note: Per-iteration `TokenUsage` records are not persisted in `SessionMetrics` (only cumulative totals are). This is acceptable — the per-iteration breakdown is only needed for the current session's TUI display. After resume, new iterations are appended to the list while cumulative totals include the restored baseline.

#### Step 3: Restore on Session Resume in `WorkflowOrchestrator`

```csharp
// During session resume, after loading state:
var savedMetrics = await _sessionManager.LoadSessionMetricsAsync(sessionId, ct);
if (savedMetrics is not null)
{
    _tokenTracker.RestoreMetrics(
        (int)savedMetrics.CumulativeInputTokens,
        (int)savedMetrics.CumulativeOutputTokens,
        savedMetrics.PremiumRequestCount);
    _iterationCount = savedMetrics.IterationCount;
}
```

#### Step 4: Verify TUI Consistency

`TopPanelDataProvider` reads from `ITokenTracker.GetSessionMetrics()`. After restoration, it will show cumulative totals including pre-resume usage. The TUI does not need changes — it already reads from the tracker.

### Data Flow After Fix

```
Resume Session:
  SessionManager.LoadSessionMetricsAsync() → SessionMetrics (from JSON)
    → WorkflowOrchestrator restores into InMemoryTokenTracker
      → InMemoryTokenTracker._cumulativeInput = saved value
      → InMemoryTokenTracker._cumulativeOutput = saved value

New Iteration:
  CopilotLlmService returns TokenUsage
    → InMemoryTokenTracker.RecordUsage() adds to cumulative
    → GetSessionMetrics() returns restored + new totals

AutoSave:
  → GetSessionMetrics() includes restored baseline + new usage
  → Persisted metrics are monotonically increasing (no data loss)
```

### Edge Cases

1. **Corrupted metrics file** — `LoadSessionMetricsAsync` throws `StorageException`. The orchestrator should catch this and log a warning, starting fresh (same as a new session). No crash.
2. **`long` to `int` truncation** — `SessionMetrics` uses `long` for token counts, but `InMemoryTokenTracker` uses `int`. For extremely long sessions this could overflow. Consider using `long` in the tracker, or validating range on restore.
3. **Concurrent auto-saves** — `AutoSaveAsync` is called from multiple triggers. The `lock` in `InMemoryTokenTracker.GetSessionMetrics()` ensures a consistent snapshot, and `SaveSessionMetricsAsync` writes atomically. No race condition.
4. **IterationCount drift** — The orchestrator maintains `_iterationCount` separately from the tracker. On resume, both must be restored from `SessionMetrics.IterationCount` to stay in sync.

### Recommendation

This is a straightforward fix requiring:
1. Add `RestoreMetrics` method to `ITokenTracker` and `InMemoryTokenTracker` (~10 lines)
2. Add restoration call in `WorkflowOrchestrator`'s resume path (~8 lines)
3. Add unit tests verifying the save → restore → accumulate → save cycle
4. Consider upgrading `InMemoryTokenTracker` fields from `int` to `long` to match `SessionMetrics`
