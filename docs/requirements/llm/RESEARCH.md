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
