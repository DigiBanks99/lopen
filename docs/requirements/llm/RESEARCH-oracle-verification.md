# Research: Oracle Verification in Copilot SDK Tool-Calling Loop

## Summary

This document covers the pattern for implementing oracle verification tools (`verify_task_completion`, `verify_component_completion`, `verify_module_completion`) within the Copilot SDK's tool-calling loop. The oracle tools collect evidence, dispatch a cheap sub-agent model to review it, and return pass/fail verdicts — all within a single SDK invocation.

## Key Finding: Copilot SDK Architecture

> **⚠️ API Note**: The Copilot SDK's `CopilotSession` does not have a `SendAndWaitAsync` method. The canonical API is `SendAsync` (returns message ID) + event handlers for responses. Examples in this document use `SendAndWaitAsync` for brevity — see [RESEARCH.md § Model API](RESEARCH.md#2-model-api--chat-completions) for the convenience wrapper implementation that must be built.

The Copilot SDK (.NET package `GitHub.Copilot.SDK`) communicates with the Copilot CLI via JSON-RPC. The **CLI handles the tool-calling loop internally** — the SDK registers custom tools via `AIFunctionFactory.Create` (from `Microsoft.Extensions.AI`), and the CLI automatically invokes handlers when the LLM calls them.

**Critical distinction**: Unlike raw OpenAI SDK usage where you manually loop on `FinishReason.ToolCalls`, the Copilot SDK abstracts this. You:
1. Register tools in `SessionConfig.Tools`
2. The CLI runs the LLM → tool-call → handler → result → LLM loop internally
3. You receive events (`ToolExecutionStartEvent`, `ToolExecutionCompleteEvent`, `AssistantMessageEvent`, `SessionIdleEvent`)

The oracle sub-agent call happens **inside your tool handler** — it's just another `CopilotClient.CreateSessionAsync` + `SendAndWaitAsync` call using a cheaper model.

---

## Pattern 1: Tool Registration with Sub-Agent Dispatch

### Copilot SDK Approach (Primary)

```csharp
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

// The CopilotClient is shared — sub-agent sessions use the same CLI server
public class OracleVerificationTools
{
    private readonly CopilotClient _client;
    private readonly EvidenceCollector _evidence;
    private readonly string _oracleModel;

    public OracleVerificationTools(
        CopilotClient client,
        EvidenceCollector evidence,
        string oracleModel = "gpt-4.1")
    {
        _client = client;
        _evidence = evidence;
        _oracleModel = oracleModel;
    }

    /// <summary>
    /// Returns AIFunction instances to register with SessionConfig.Tools
    /// </summary>
    public AIFunction[] CreateTools() =>
    [
        AIFunctionFactory.Create(VerifyTaskCompletionAsync, "verify_task_completion",
            "Verify a task is complete by reviewing its diff against requirements"),
        AIFunctionFactory.Create(VerifyComponentCompletionAsync, "verify_component_completion",
            "Verify all tasks in a component are complete"),
        AIFunctionFactory.Create(VerifyModuleCompletionAsync, "verify_module_completion",
            "Verify the full module meets all acceptance criteria"),
    ];

    [Description("Verify a task is complete by reviewing its diff against requirements")]
    private async Task<OracleVerdict> VerifyTaskCompletionAsync(
        [Description("The task identifier")] string taskId,
        [Description("Component the task belongs to")] string componentId)
    {
        var evidence = await _evidence.CollectTaskEvidenceAsync(taskId, componentId);
        return await DispatchOracleAsync(evidence, VerificationScope.Task);
    }

    [Description("Verify all tasks in a component are complete")]
    private async Task<OracleVerdict> VerifyComponentCompletionAsync(
        [Description("The component identifier")] string componentId)
    {
        var evidence = await _evidence.CollectComponentEvidenceAsync(componentId);
        return await DispatchOracleAsync(evidence, VerificationScope.Component);
    }

    [Description("Verify the full module meets all acceptance criteria")]
    private async Task<OracleVerdict> VerifyModuleCompletionAsync(
        [Description("The module identifier")] string moduleId)
    {
        var evidence = await _evidence.CollectModuleEvidenceAsync(moduleId);
        return await DispatchOracleAsync(evidence, VerificationScope.Module);
    }

    private async Task<OracleVerdict> DispatchOracleAsync(
        VerificationEvidence evidence,
        VerificationScope scope)
    {
        // Create a separate session with a cheap model for the oracle
        await using var oracleSession = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = _oracleModel,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = BuildOracleSystemPrompt(scope)
            },
            // No tools — oracle is a pure reviewer, not an agent
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
        });

        var prompt = FormatEvidencePrompt(evidence, scope);

        var response = await oracleSession.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromSeconds(30));

        var content = response?.Data?.Content ?? "";
        return ParseOracleResponse(content);
    }
}
```

### Session Setup (Orchestrator)

```csharp
public class TaskOrchestrator
{
    private readonly CopilotClient _client;
    private readonly OracleVerificationTools _oracleTools;
    private readonly SessionState _state;

    public async Task ExecuteTaskAsync(TaskDefinition task)
    {
        var oracleTools = _oracleTools.CreateTools();

        // Primary session — premium model for task execution
        await using var session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = _state.GetModelForPhase("building"), // e.g. "claude-opus-4.6"
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = BuildTaskSystemPrompt(task)
            },
            Tools = [
                ..oracleTools,
                // Other Lopen-managed tools
                AIFunctionFactory.Create(ReadSpec, "read_spec"),
                AIFunctionFactory.Create(UpdateTaskStatus, "update_task_status"),
            ]
        });

        // Listen for events to track tool calls and enforce back-pressure
        bool oraclePassed = false;
        session.On(evt =>
        {
            if (evt is ToolExecutionCompleteEvent toolComplete)
            {
                // Track that oracle was called and passed
                if (toolComplete.Data.ToolName?.StartsWith("verify_") == true)
                {
                    var verdict = JsonSerializer.Deserialize<OracleVerdict>(
                        toolComplete.Data.Result ?? "{}");
                    if (verdict?.Passed == true)
                        oraclePassed = true;
                }
            }
        });

        // Send the task prompt — the CLI handles the full tool-calling loop
        // The LLM will: implement → call verify_task_completion → fix if needed → loop
        await session.SendAndWaitAsync(
            new MessageOptions { Prompt = BuildTaskPrompt(task) },
            timeout: TimeSpan.FromMinutes(10));

        // Back-pressure enforcement
        if (!oraclePassed)
        {
            _state.MarkTaskFailed(task.Id, "Oracle verification did not pass");
        }
    }
}
```

---

## Pattern 2: Evidence Collection

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

public record VerificationEvidence
{
    public required VerificationScope Scope { get; init; }
    public required string TargetId { get; init; }
    public string Diff { get; init; } = "";
    public List<string> AcceptanceCriteria { get; init; } = [];
    public TestResults? Tests { get; init; }
    public List<string> FilesChanged { get; init; } = [];
}

public record TestResults
{
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public List<TestFailure> Failures { get; init; } = [];
}

public record TestFailure(string TestName, string ErrorMessage);

public enum VerificationScope { Task, Component, Module }

public class EvidenceCollector
{
    private readonly string _repoPath;
    private readonly SessionState _state;

    public EvidenceCollector(string repoPath, SessionState state)
    {
        _repoPath = repoPath;
        _state = state;
    }

    public async Task<VerificationEvidence> CollectTaskEvidenceAsync(
        string taskId, string componentId)
    {
        var task = _state.GetTask(taskId);
        var baseSha = task.StartCommitSha;
        var headSha = "HEAD";

        // Run diff + tests in parallel
        var diffTask = GetGitDiffAsync(baseSha, headSha);
        var testTask = RunTestsAsync();
        var filesTask = GetChangedFilesAsync(baseSha, headSha);

        await Task.WhenAll(diffTask, testTask, filesTask);

        return new VerificationEvidence
        {
            Scope = VerificationScope.Task,
            TargetId = taskId,
            Diff = await diffTask,
            AcceptanceCriteria = task.AcceptanceCriteria,
            Tests = await testTask,
            FilesChanged = await filesTask
        };
    }

    public async Task<VerificationEvidence> CollectComponentEvidenceAsync(
        string componentId)
    {
        var component = _state.GetComponent(componentId);
        var tasks = _state.GetTasksForComponent(componentId);

        // Aggregate diffs across all tasks
        var baseSha = component.StartCommitSha;
        var diff = await GetGitDiffAsync(baseSha, "HEAD");
        var tests = await RunTestsAsync();
        var files = await GetChangedFilesAsync(baseSha, "HEAD");

        return new VerificationEvidence
        {
            Scope = VerificationScope.Component,
            TargetId = componentId,
            Diff = diff,
            AcceptanceCriteria = component.AcceptanceCriteria,
            Tests = tests,
            FilesChanged = files
        };
    }

    public async Task<VerificationEvidence> CollectModuleEvidenceAsync(
        string moduleId)
    {
        var module = _state.GetModule(moduleId);
        var baseSha = module.StartCommitSha;
        var diff = await GetGitDiffAsync(baseSha, "HEAD");
        var tests = await RunTestsAsync();
        var files = await GetChangedFilesAsync(baseSha, "HEAD");

        return new VerificationEvidence
        {
            Scope = VerificationScope.Module,
            TargetId = moduleId,
            Diff = diff,
            AcceptanceCriteria = module.AcceptanceCriteria,
            Tests = tests,
            FilesChanged = files
        };
    }

    // --- Git helpers ---

    private async Task<string> GetGitDiffAsync(string baseSha, string headSha)
    {
        return await RunProcessAsync("git",
            $"diff {baseSha}..{headSha} --unified=3 --stat-width=80");
    }

    private async Task<List<string>> GetChangedFilesAsync(string baseSha, string headSha)
    {
        var output = await RunProcessAsync("git",
            $"diff {baseSha}..{headSha} --name-only");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    // --- Test runner ---

    private async Task<TestResults> RunTestsAsync()
    {
        var trxPath = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}.trx");
        try
        {
            var output = await RunProcessAsync("dotnet",
                $"test --logger \"trx;LogFileName={trxPath}\" --no-build -v q",
                throwOnError: false);

            if (!File.Exists(trxPath))
            {
                return new TestResults { Total = 0, Passed = 0, Failed = 0 };
            }

            return ParseTrxResults(trxPath);
        }
        finally
        {
            if (File.Exists(trxPath)) File.Delete(trxPath);
        }
    }

    private static TestResults ParseTrxResults(string trxPath)
    {
        var doc = XDocument.Load(trxPath);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
        int total = int.Parse(counters?.Attribute("total")?.Value ?? "0");
        int passed = int.Parse(counters?.Attribute("passed")?.Value ?? "0");
        int failed = int.Parse(counters?.Attribute("failed")?.Value ?? "0");

        var failures = doc.Descendants(ns + "UnitTestResult")
            .Where(r => r.Attribute("outcome")?.Value == "Failed")
            .Select(r => new TestFailure(
                r.Attribute("testName")?.Value ?? "unknown",
                r.Descendants(ns + "Message").FirstOrDefault()?.Value ?? ""))
            .ToList();

        return new TestResults
        {
            Total = total,
            Passed = passed,
            Failed = failed,
            Failures = failures
        };
    }

    private async Task<string> RunProcessAsync(
        string fileName, string arguments, bool throwOnError = true)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (throwOnError && process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{fileName} exited with code {process.ExitCode}: {stderr}");

        return stdout;
    }
}
```

---

## Pattern 3: Oracle Prompt Template & Structured Output

### Verdict Schema

```csharp
public record OracleVerdict
{
    [JsonPropertyName("passed")]
    public bool Passed { get; init; }

    [JsonPropertyName("confidence")]
    public string Confidence { get; init; } = "medium"; // "high", "medium", "low"

    [JsonPropertyName("findings")]
    public List<OracleFinding> Findings { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";
}

public record OracleFinding
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "info"; // "blocker", "warning", "info"

    [JsonPropertyName("category")]
    public string Category { get; init; } = ""; // "missing_requirement", "test_failure", "code_quality"

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("location")]
    public string? Location { get; init; } // file:line if applicable
}
```

### Oracle System Prompt

```csharp
private static string BuildOracleSystemPrompt(VerificationScope scope)
{
    var scopeLabel = scope switch
    {
        VerificationScope.Task => "task",
        VerificationScope.Component => "component",
        VerificationScope.Module => "module",
        _ => "unknown"
    };

    return $$"""
        You are an oracle verification agent. Your job is to review code changes against
        requirements and determine if the work is complete.

        ## Instructions

        1. Review the diff against the acceptance criteria provided
        2. Check that all tests pass (if test results are provided)
        3. Look for missing requirements, regressions, or quality issues
        4. Be strict but fair — only flag genuine gaps, not style preferences

        ## Scope

        You are reviewing at the **{{scopeLabel}}** level.
        {{scope switch {
            VerificationScope.Task =>
                "Focus on: Does the diff satisfy the specific task requirements? Do tests exist and pass?",
            VerificationScope.Component =>
                "Focus on: Do all task diffs together satisfy the component acceptance criteria? Any regressions across tasks? Integration coherence?",
            VerificationScope.Module =>
                "Focus on: Does the full module meet ALL acceptance criteria in the spec? Full test suite passes? Cross-component integration works?",
            _ => ""
        }}}

        ## Output Format

        Respond with ONLY a JSON object matching this schema — no markdown, no explanation outside the JSON:

        ```json
        {
          "passed": true|false,
          "confidence": "high"|"medium"|"low",
          "summary": "Brief one-line summary of verdict",
          "findings": [
            {
              "severity": "blocker"|"warning"|"info",
              "category": "missing_requirement"|"test_failure"|"code_quality"|"regression",
              "description": "What is wrong or missing",
              "location": "file:line or null"
            }
          ]
        }
        ```

        Rules:
        - "passed" is false if ANY finding has severity "blocker"
        - "passed" is true only if all acceptance criteria are met and tests pass
        - Keep findings actionable and specific
        - An empty findings list with passed=true means everything looks good
        """;
}
```

### Evidence Formatting

```csharp
private static string FormatEvidencePrompt(
    VerificationEvidence evidence, VerificationScope scope)
{
    var sb = new StringBuilder();

    sb.AppendLine($"## Verification Request: {scope} `{evidence.TargetId}`");
    sb.AppendLine();

    // Acceptance criteria
    sb.AppendLine("### Acceptance Criteria");
    foreach (var criterion in evidence.AcceptanceCriteria)
    {
        sb.AppendLine($"- [ ] {criterion}");
    }
    sb.AppendLine();

    // Test results
    if (evidence.Tests is { } tests)
    {
        sb.AppendLine("### Test Results");
        sb.AppendLine($"Total: {tests.Total} | Passed: {tests.Passed} | Failed: {tests.Failed}");
        if (tests.Failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Failures:**");
            foreach (var f in tests.Failures)
            {
                sb.AppendLine($"- `{f.TestName}`: {f.ErrorMessage}");
            }
        }
        sb.AppendLine();
    }

    // Files changed
    sb.AppendLine("### Files Changed");
    foreach (var file in evidence.FilesChanged)
    {
        sb.AppendLine($"- {file}");
    }
    sb.AppendLine();

    // Diff (potentially large — truncate for context window)
    sb.AppendLine("### Diff");
    sb.AppendLine("```diff");
    var diff = evidence.Diff;
    if (diff.Length > 50_000)
    {
        sb.AppendLine(diff[..50_000]);
        sb.AppendLine($"\n... (truncated, {diff.Length - 50_000} chars omitted)");
    }
    else
    {
        sb.AppendLine(diff);
    }
    sb.AppendLine("```");

    return sb.ToString();
}
```

### Response Parsing

```csharp
private static OracleVerdict ParseOracleResponse(string content)
{
    // Strip markdown code fences if present
    var json = content.Trim();
    if (json.StartsWith("```"))
    {
        var firstNewline = json.IndexOf('\n');
        var lastFence = json.LastIndexOf("```");
        if (firstNewline >= 0 && lastFence > firstNewline)
        {
            json = json[(firstNewline + 1)..lastFence].Trim();
        }
    }

    try
    {
        var verdict = JsonSerializer.Deserialize<OracleVerdict>(json);
        return verdict ?? FallbackVerdict(content);
    }
    catch (JsonException)
    {
        return FallbackVerdict(content);
    }
}

private static OracleVerdict FallbackVerdict(string rawContent) => new()
{
    Passed = false,
    Confidence = "low",
    Summary = "Oracle response could not be parsed as structured JSON",
    Findings =
    [
        new OracleFinding
        {
            Severity = "warning",
            Category = "oracle_error",
            Description = $"Raw oracle response: {rawContent[..Math.Min(500, rawContent.Length)]}"
        }
    ]
};
```

---

## Pattern 4: Retry Loop (Within Single SDK Invocation)

The retry loop is **implicit** in the Copilot SDK. The primary LLM controls the loop via its own tool-calling behavior. Lopen's role is to:

1. **Instruct the LLM** via system prompt to call `verify_*` tools and fix issues if they fail
2. **The tool handler** dispatches the oracle and returns the verdict as the tool result
3. **The LLM reads the verdict** in its context, fixes issues, and calls the tool again
4. **The CLI's internal loop** continues until the LLM stops calling tools

### System Prompt Excerpt (Instructs the Retry Pattern)

```csharp
private static string BuildTaskSystemPrompt(TaskDefinition task) => $$"""
    ## Workflow

    You are executing task `{{task.Id}}` for component `{{task.ComponentId}}`.

    ### Instructions
    1. Implement the task requirements (see acceptance criteria below)
    2. Write or update tests as needed
    3. Run tests to verify they pass
    4. Call `verify_task_completion` with taskId="{{task.Id}}" and componentId="{{task.ComponentId}}"
    5. If verification **fails**, review the findings, fix the issues, and call `verify_task_completion` again
    6. If verification **passes**, call `update_task_status` with status="complete"
    7. Do NOT call `update_task_status(complete)` without a passing verification — it will be rejected

    ### Retry Limits
    - You may retry verification up to 3 times
    - If you cannot pass verification after 3 attempts, call `update_task_status` with status="failed"
      and include a summary of remaining issues

    ### Acceptance Criteria
    {{string.Join("\n", task.AcceptanceCriteria.Select(c => $"- {c}"))}}
    """;
```

### Back-Pressure Enforcement

```csharp
// This is registered as the update_task_status tool handler
private string UpdateTaskStatus(
    [Description("Task identifier")] string taskId,
    [Description("New status: pending, in-progress, complete, failed")] string status,
    [Description("Optional reason for failure")] string? reason = null)
{
    if (status == "complete" && !_verificationTracker.HasPassedVerification(taskId))
    {
        return JsonSerializer.Serialize(new
        {
            error = true,
            message = "Cannot mark task complete without a passing verify_task_completion call. " +
                      "Please call verify_task_completion first."
        });
    }

    _state.UpdateTaskStatus(taskId, status, reason);
    return JsonSerializer.Serialize(new
    {
        success = true,
        taskId,
        status,
        message = $"Task {taskId} marked as {status}"
    });
}
```

### Verification Tracker (Enforces Oracle-Before-Complete)

```csharp
public class VerificationTracker
{
    private readonly Dictionary<string, OracleVerdict> _lastVerdicts = new();

    public void RecordVerdict(string targetId, OracleVerdict verdict)
    {
        _lastVerdicts[targetId] = verdict;
    }

    public bool HasPassedVerification(string targetId)
    {
        return _lastVerdicts.TryGetValue(targetId, out var v) && v.Passed;
    }

    public OracleVerdict? GetLastVerdict(string targetId)
    {
        return _lastVerdicts.GetValueOrDefault(targetId);
    }

    public void Reset() => _lastVerdicts.Clear();
}
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    Lopen Orchestrator                    │
│                                                         │
│  1. Prepares context, system prompt, tools              │
│  2. Calls session.SendAndWaitAsync(taskPrompt)          │
│     ┌──────────────────────────────────────────────┐    │
│     │           Copilot CLI (via JSON-RPC)          │    │
│     │                                               │    │
│     │  LLM (primary model, e.g. claude-opus-4.6)   │    │
│     │    │                                          │    │
│     │    ├── implements code (native tools)          │    │
│     │    ├── runs tests (native tools)              │    │
│     │    ├── calls verify_task_completion ◄──────────┤───┤─ Tool registered by Lopen
│     │    │     │                                    │    │
│     │    │     ▼                                    │    │
│     │    │   Lopen tool handler                     │    │
│     │    │     │                                    │    │
│     │    │     ├── EvidenceCollector.Collect()       │    │
│     │    │     │     ├── git diff                    │    │
│     │    │     │     ├── dotnet test                 │    │
│     │    │     │     └── acceptance criteria         │    │
│     │    │     │                                    │    │
│     │    │     ├── DispatchOracleAsync()             │    │
│     │    │     │     ├── CreateSessionAsync(         │    │
│     │    │     │     │     model: "gpt-4.1")        │    │
│     │    │     │     └── SendAndWaitAsync(evidence)  │    │
│     │    │     │           ▼                        │    │
│     │    │     │     Oracle verdict (JSON)           │    │
│     │    │     │                                    │    │
│     │    │     └── Returns OracleVerdict to LLM     │    │
│     │    │                                          │    │
│     │    ├── if FAILED: fixes issues, retries ──────┘    │
│     │    ├── if PASSED: calls update_task_status         │
│     │    └── done → SessionIdleEvent                     │
│     └──────────────────────────────────────────────┘    │
│                                                         │
│  3. Back-pressure: reject complete if no passing oracle  │
│  4. Update session state                                 │
└─────────────────────────────────────────────────────────┘
```

---

## Key Design Decisions

### Why Copilot SDK for the Oracle (not raw OpenAI HTTP)?

1. **Authentication reuse** — Same `CopilotClient` instance, same GitHub credentials
2. **Model availability** — All models available via Copilot CLI are accessible
3. **Billing alignment** — Oracle calls use standard-tier models (e.g., `gpt-4.1`), counted as standard requests
4. **No additional dependencies** — No need for `Azure.AI.OpenAI` or `OpenAI` NuGet packages

### Why `SystemMessageMode.Replace` for Oracle?

The oracle session needs a completely controlled system prompt — no Copilot CLI defaults about being helpful, file editing capabilities, etc. The oracle is a pure reviewer that outputs structured JSON.

### Why `InfiniteSessions = Enabled: false` for Oracle?

Oracle sessions are single-shot (one prompt → one response). No need for context compaction or session persistence. This keeps them fast and lightweight.

### Tool Handler is Synchronous from CLI's Perspective

When the LLM calls `verify_task_completion`, the CLI waits for the tool handler to return. Inside the handler, we create an oracle session and wait for its response. The CLI doesn't know or care that we're making another LLM call — it just sees a tool handler that takes a bit longer than usual.

### Diff Size Management

Large diffs can exceed the oracle's context window. Strategies:
- **Truncation**: Cut at 50K chars with a note (shown in `FormatEvidencePrompt`)
- **Stat-only mode**: For very large changes, send `git diff --stat` instead of full diff
- **Per-file splitting**: For component/module scope, send most-relevant files' diffs only

---

## NuGet Dependencies

| Package | Purpose |
|---|---|
| `GitHub.Copilot.SDK` | Core SDK — client, session, tool registration |
| `Microsoft.Extensions.AI` | `AIFunctionFactory.Create` for type-safe tool definitions |
| `System.Text.Json` | Verdict serialization/deserialization |

No additional packages needed. `System.Diagnostics.Process` (for git/dotnet commands) and `System.Xml.Linq` (for TRX parsing) are in the base class library.

---

## Alternative: OpenAI-Style Manual Loop (Not Recommended for Lopen)

For reference, the raw OpenAI SDK pattern is included below. This would only apply if Lopen needed to bypass the Copilot CLI and call the API directly.

```csharp
// NOT RECOMMENDED — shown only for completeness.
// The Copilot SDK handles this loop internally.

using OpenAI.Chat;

ChatClient client = new("gpt-5", apiKey);
List<ChatMessage> messages = [new SystemChatMessage(systemPrompt), new UserChatMessage(taskPrompt)];

ChatCompletionOptions options = new()
{
    Tools = {
        ChatTool.CreateFunctionTool("verify_task_completion", "...",
            BinaryData.FromString("""{"type":"object","properties":{"taskId":{"type":"string"}}}"""))
    }
};

bool requiresAction;
do
{
    requiresAction = false;
    ChatCompletion completion = await client.CompleteChatAsync(messages, options);

    if (completion.FinishReason == ChatFinishReason.ToolCalls)
    {
        messages.Add(new AssistantChatMessage(completion));
        foreach (var toolCall in completion.ToolCalls)
        {
            string result = toolCall.FunctionName switch
            {
                "verify_task_completion" => await HandleVerifyAsync(toolCall.FunctionArguments),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolCall.FunctionName}" })
            };
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        requiresAction = true;
    }
    else
    {
        // LLM is done — process final response
        ProcessFinalResponse(completion.Content[0].Text);
    }
} while (requiresAction);
```

This pattern requires manual management of the message list, tool call IDs, and the loop condition. The Copilot SDK handles all of this automatically.

---

## References

- [GitHub Copilot SDK — .NET README](https://github.com/github/copilot-sdk/blob/main/dotnet/README.md)
- [Copilot SDK — Getting Started Guide](https://github.com/github/copilot-sdk/blob/main/docs/getting-started.md)
- [Copilot SDK — Session.cs (tool handler registration)](https://github.com/github/copilot-sdk/blob/main/dotnet/src/Session.cs)
- [Copilot SDK — ToolsTests.cs (tool testing patterns)](https://github.com/github/copilot-sdk/blob/main/dotnet/test/ToolsTests.cs)
- [OpenAI .NET SDK — openai/openai-dotnet](https://github.com/openai/openai-dotnet)
- [Microsoft.Extensions.AI — AIFunctionFactory](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aifunctionfactory)
- [LLM Specification — Oracle Verification Tools](../llm/SPECIFICATION.md#oracle-verification-tools)
