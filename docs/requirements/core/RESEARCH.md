---
module: core
description: Research findings for implementing the Lopen core orchestration engine
date: 2025-07-15
status: complete
---

# Core Module Research

Research into implementation patterns and technology choices for the Lopen core orchestration module — a .NET 8+ CLI application that drives the GitHub Copilot SDK through a structured 7-step workflow.

## Table of Contents

1. [Workflow State Machine](#1-workflow-state-machine)
2. [Task Hierarchy Pattern](#2-task-hierarchy-pattern)
3. [Back-Pressure Mechanisms](#3-back-pressure-mechanisms)
4. [Specification Drift Detection](#4-specification-drift-detection)
5. [Git Integration in .NET](#5-git-integration-in-net)
6. [Oracle Verification Pattern](#6-oracle-verification-pattern)
7. [Document Section Extraction](#7-document-section-extraction)
8. [Implementation Approach](#8-implementation-approach)
9. [Orchestration Loop Pattern](#9-orchestration-loop-pattern)
10. [DI Registration Gap](#10-di-registration-gap)
11. [Integration Wiring Patterns](#11-integration-wiring-patterns)

---

## 1. Workflow State Machine

**Problem**: Implement a re-entrant state machine for Lopen's 7-step workflow across 3 phases, where each iteration assesses actual codebase state rather than trusting stale session data.

### Patterns Evaluated

| Pattern | Library | Fit |
|---|---|---|
| **Stateless Library** | [`Stateless`](https://www.nuget.org/packages/Stateless) v5.20.1 | ✅ Strong |
| **Manual Enum/Switch** | None | ✅ Good (simpler) |
| **Workflow-as-Data** | None (hand-built) | ✅ Strong (evolvable) |

### Recommended: Hybrid — Stateless + Declarative Definition

Define the workflow declaratively as data (transitions, gates, phases as records), then configure a `Stateless` state machine from that definition at startup. The re-entrant assessor is the state accessor delegate.

**Why Stateless**: External state storage via `() => assessor.GetCurrentStep()` delegate means every state read is an assessment of reality. Guard clauses model the human gate (Requirement Gathering → Planning). `PermitDynamic` handles the Step 7 → Step 4 loop. Built-in Mermaid export supports the TUI. Battle-tested (15M+ downloads, used by GitHub/Microsoft).

**Why declarative definition on top**: Transition table is queryable, serializable, and validatable at startup. Adding a step = adding a record. Aligns with Lopen's spec-driven philosophy.

```csharp
// Stateless with external state storage — the core re-entrant pattern
_machine = new StateMachine<WorkflowStep, WorkflowTrigger>(
    () => _assessor.GetCurrentStep(),    // Always assess reality
    step => _assessor.PersistStep(step));

// Human-gated transition
_machine.Configure(WorkflowStep.DraftSpecification)
    .Permit(WorkflowTrigger.SpecApproved, WorkflowStep.DetermineDependencies)
    .PermitReentryIf(WorkflowTrigger.Assess, () => !_assessor.IsSpecReady());

// Dynamic routing for Step 7 → Step 4 or Complete
_machine.Configure(WorkflowStep.Repeat)
    .PermitDynamic(WorkflowTrigger.Assess, () =>
        _assessor.HasMoreComponents()
            ? WorkflowStep.SelectNextComponent
            : WorkflowStep.Complete);
```

**NuGet**: `Stateless` v5.20.1 — .NET 8/9/10, zero dependencies, async-first.

### Relevance to Lopen

The external state storage pattern is the exact mechanism needed for re-entrant assessment. Each `_machine.State` read calls `_assessor.GetCurrentStep()`, which inspects the codebase, session state, and task hierarchy — never trusting cached state. Guard clauses naturally express the human gate at the Requirement Gathering → Planning boundary. `PermitDynamic` handles the Building phase loop.

---

## 2. Task Hierarchy Pattern

**Problem**: Data structures for Module → Component → Task → Subtask hierarchy with state tracking, aggregate computation, and JSON serialization.

### Recommended: Generic Composite with Typed Nesting

Use `WorkNode<TChild>` generic base class for compile-time hierarchy enforcement, with an `IWorkNode` interface for homogeneous traversal.

```csharp
public interface IWorkNode
{
    Guid Id { get; }
    string Name { get; }
    WorkNodeState State { get; }
    IWorkNode? Parent { get; }
    IReadOnlyList<IWorkNode> Children { get; }
}

public abstract class WorkNode<TChild> : IWorkNode where TChild : IWorkNode
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public WorkNodeState State { get; private set; } = WorkNodeState.Pending;

    public void TransitionTo(WorkNodeState newState)
    {
        State = State.TransitionTo(newState); // Validated
    }

    internal void RestoreState(WorkNodeState state) => State = state; // Deserialization bypass
}

// Typed hierarchy prevents invalid nesting at compile time
public sealed class Module    : WorkNode<Component> { }
public sealed class Component : WorkNode<TaskItem>  { }
public sealed class TaskItem  : WorkNode<Subtask>   { }
public sealed class Subtask   : IWorkNode           { } // Leaf
```

### State Transitions

Validated via tuple switch expression — the idiomatic .NET 8 transition table:

```csharp
public static WorkNodeState TransitionTo(this WorkNodeState current, WorkNodeState target) =>
    (current, target) switch
    {
        (Pending,    InProgress) => InProgress,
        (InProgress, Complete)   => Complete,
        (InProgress, Failed)     => Failed,
        (Failed,     InProgress) => InProgress, // Retry
        _ => throw new InvalidOperationException($"Invalid: {current} → {target}")
    };
```

### Aggregate State

Bottom-up recursive computation: Failed takes priority → All Complete → All Pending → else InProgress.

### Serialization

`[JsonPolymorphic]` + `[JsonDerivedType]` with source generators for AOT safety. `[JsonIgnore]` on `Parent` references, `WireParents()` post-deserialization.

### Key Decision: Mutable Classes over Records

Full immutability fails for 4-level trees — path copying (`with { Children = ... }`) is unreadable. Lopen uses Git for rollback, not tree snapshots. `private set` + `TransitionTo()` guards provide mutation safety without immutability overhead.

### Relevance to Lopen

Maps directly to the spec's task hierarchy. `Descendants()` + LINQ enables queries like "find next pending task" and "compute completion percentage" used by the TUI and orchestration loop. Source-generated JSON serialization matches the Storage spec's `state.json` format.

---

## 3. Back-Pressure Mechanisms

**Problem**: Implement 4 categories of guardrails — resource limits, churn detection, tool discipline, and quality gates — as a composable pipeline.

### Architecture: Guardrail Pipeline

An ordered chain of `IGuardrail` implementations evaluated before each iteration. Each returns Pass/Warn/Block. Composed via DI, ordered by `IGuardrail.Order`.

```
BudgetGuardrail (100) → ChurnDetection (200) → OracleVerification (300) → ToolDiscipline (400)
```

### Category 1: Budget Tracker

Thread-safe `Interlocked`-based counters with `event` notifications at threshold crossings. **Soft-limit**: warns at 80%, pauses for confirmation at 90%, never hard-blocks. CAS prevents duplicate notifications.

```csharp
public sealed class BudgetTracker
{
    private long _tokensConsumed;
    public BudgetCheckResult RecordTokenUsage(long tokens)
    {
        var newTotal = Interlocked.Add(ref _tokensConsumed, tokens);
        return CheckThresholds(newTotal, Settings.TokenBudgetPerModule, ...);
    }
}
```

### Category 2: Churn & Circular Behavior

- **ChurnDetector**: `ConcurrentDictionary<string, int>` per-task failure counter. Escalation at configurable threshold (default 3).
- **CircularBehaviorDetector**: Content-hash-aware frequency counter. Distinguishes "same file read 3 times, unchanged" (circular) from "read 3 times, changed each time" (legitimate).
- **SlidingWindowCounter**: Time-windowed via `TimeProvider` (.NET 8) for testability.
- **BackPressureStateMachine**: Normal → Warning → InterventionRequired → Halted (Polly circuit-breaker inspired).

### Category 3: Quality Gates

`RequiredToolVerifier` enforces that mandatory tools (e.g., `verify_task_completion`) were called before allowing completion claims. Uses the `ToolCallAuditLog`.

### Category 4: Tool Discipline

- **ToolCallAuditLog**: Immutable append-only log with lock-free CAS.
- **PatternDetector**: LINQ-based analysis of audit trail for repeated reads, command failures, shotgun debugging.
- **CorrectiveInstructionGenerator**: Transforms detected patterns into prompt instructions. Soft limits only — warns but never hard-blocks.

### Key Design Decisions

| Decision | Rationale |
|---|---|
| `IGuardrail.ShortCircuitOnBlock` | Budget = hard stop; tool discipline = soft warning |
| Fail-open for guardrail errors | Infrastructure must never crash the orchestration loop |
| `TimeProvider` for all time logic | Deterministic testing with `FakeTimeProvider` |
| Discriminated unions via abstract records | Forces callers to handle all cases via pattern matching |

### Relevance to Lopen

Maps 1:1 to the four back-pressure categories in the Core spec. The pipeline pattern integrates naturally into the orchestration loop — `guardrails.EvaluateAsync(ctx)` runs before each SDK invocation. `BuildCorrectiveInstructions()` output appends to the system prompt. DI registration via `services.AddGuardrailPipeline()`.

---

## 4. Specification Drift Detection

**Problem**: Hash section content to detect when specifications change between iterations. Flag drift and determine re-entry point.

### Recommended: XxHash128 with Content Normalization

**Hash algorithm**: XxHash128 from `System.IO.Hashing` (.NET 8 BCL). 20–60× faster than SHA256, 128-bit output, non-cryptographic (sufficient for content addressing).

**Normalization** prevents false-positive drift from whitespace/line-ending differences:

```csharp
public static class ContentNormalizer
{
    public static string Normalize(string content)
    {
        content = content.ReplaceLineEndings("\n"); // Normalize line endings
        content = content.Trim();
        content = MultipleBlankLines().Replace(content, "\n\n"); // Collapse blanks
        return content;
    }
}

public static class ContentHasher
{
    public static string ComputeHash(string content)
    {
        string normalized = ContentNormalizer.Normalize(content);
        byte[] hash = XxHash128.Hash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash); // 32-char hex
    }

    public static bool HasDrifted(string currentContent, string previousHash)
        => !ComputeHash(currentContent).Equals(previousHash, StringComparison.OrdinalIgnoreCase);
}
```

### Drift Detection Flow

1. At session save: extract each spec section, compute hash, store in `state.json`
2. On resume: re-extract sections, compare hashes
3. If changed: flag drift, report to user, determine re-entry step (e.g., Acceptance Criteria changed → re-assess from step 3)

### Cache Key Strategy

Per the Storage spec, section cache is keyed by `file path + section header + file modification timestamp`. XxHash64 of the composite key generates a deterministic cache filename.

### Relevance to Lopen

Implements the "Specification Drift Detection" section of the Core spec. Content normalization prevents false positives when editors change line endings or trailing whitespace. XxHash128 is in the .NET 8 BCL — no NuGet dependency. The hash approach is simpler and more reliable than file-level modification timestamps alone, since timestamps don't detect which *section* changed.

---

## 5. Git Integration in .NET

**Problem**: Auto-commit, branch management, revert, diff reading, and file timestamps for the orchestrator.

### Approaches Compared

| Criterion | LibGit2Sharp | Process-based `git` CLI |
|---|---|---|
| Maintenance | v0.31.0 (Dec 2024), .NET 8 | Always current |
| Async support | ❌ Synchronous only | ✅ Native async |
| Deployment | +15–25 MB native blobs | Zero (git on PATH) |
| Features | Subset (no rebase, no GPG) | Full git feature set |
| Docker/CI | glibc issues on Alpine | Works everywhere |
| AOT/Trimming | ⚠️ Requires configuration | ✅ No issues |

### Recommended: Process-Based `git` CLI

```csharp
public interface IGitService
{
    Task<string> CommitAllAsync(string module, string taskName, CancellationToken ct = default);
    Task CreateBranchAsync(string branchName, CancellationToken ct = default);
    Task ResetToCommitAsync(string commitSha, CancellationToken ct = default);
    Task<DateTimeOffset?> GetLastCommitDateAsync(string filePath, CancellationToken ct = default);
    Task<string> GetDiffAsync(CancellationToken ct = default);
    Task<string> GetCommitDiffAsync(string commitSha, CancellationToken ct = default);
}
```

Implementation wraps `System.Diagnostics.Process` with:
- `LC_ALL=C` and `GIT_TERMINAL_PROMPT=0` environment variables for consistent output
- `GitException` with exit code, stderr, and command for structured error handling
- Retry with exponential backoff for `index.lock` contention

### Why Not LibGit2Sharp

1. **Lopen requires git installed** — eliminates the "must be pre-installed" concern
2. **Async-first** — LibGit2Sharp is synchronous; wrapping with `Task.Run` wastes thread pool threads
3. **Zero native dependency risk** — No Alpine/musl/AOT issues
4. **Operations are infrequent** — ~5-20 git operations per module build; fork/exec overhead is negligible
5. **Full feature parity** — GPG signing, stash, worktrees available immediately if needed later

### Relevance to Lopen

The `IGitService` interface covers all five git operations the Core spec requires (auto-commit, branch, revert, timestamps, diffs). `FakeGitService` implementation enables unit testing without a real repository. The process-based approach aligns with Lopen's existing requirement of `git` as a system dependency.

---

## 6. Oracle Verification Pattern

**Problem**: Dispatch a cheap sub-agent model to verify work within the primary model's tool-calling loop, without consuming additional premium requests.

### Key Finding: Copilot SDK Architecture

The Copilot SDK communicates with the CLI via JSON-RPC. The **CLI handles the tool-calling loop internally** — unlike raw OpenAI SDK usage where you manually loop on `FinishReason.ToolCalls`. Tool handlers are registered via `AIFunctionFactory.Create` from `Microsoft.Extensions.AI`.

### Pattern: Tool Handler with Sub-Agent Dispatch

```csharp
public class OracleVerificationTools
{
    private readonly CopilotClient _client;
    private readonly EvidenceCollector _evidence;
    private readonly string _oracleModel;

    public AIFunction[] CreateTools() =>
    [
        AIFunctionFactory.Create(VerifyTaskCompletionAsync, "verify_task_completion",
            "Verify a task is complete by reviewing its diff against requirements"),
        // ... verify_component_completion, verify_module_completion
    ];

    private async Task<OracleVerdict> VerifyTaskCompletionAsync(string taskId, string componentId)
    {
        var evidence = await _evidence.CollectTaskEvidenceAsync(taskId, componentId);
        return await DispatchOracleAsync(evidence, VerificationScope.Task);
    }

    private async Task<OracleVerdict> DispatchOracleAsync(
        VerificationEvidence evidence, VerificationScope scope)
    {
        // Create a separate session with a cheap model for the oracle
        await using var oracleSession = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = _oracleModel, // e.g., "gpt-4.1"
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = BuildOracleSystemPrompt(scope)
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
        });

        var response = await oracleSession.SendAndWaitAsync(
            new MessageOptions { Prompt = FormatEvidencePrompt(evidence, scope) },
            timeout: TimeSpan.FromSeconds(30));

        return ParseOracleResponse(response?.Data?.Content ?? "");
    }
}
```

### Evidence Collection

`EvidenceCollector` runs `git diff` and `dotnet test` in parallel, parses TRX results, and returns structured `VerificationEvidence` (diff, test results, acceptance criteria, files changed).

### Oracle Verdict Schema

```csharp
public record OracleVerdict
{
    public bool Passed { get; init; }
    public string Confidence { get; init; } = "medium"; // high/medium/low
    public List<OracleFinding> Findings { get; init; } = [];
    public string Summary { get; init; } = "";
}
```

### Retry Loop

The retry loop is **implicit** — the Copilot CLI runs the LLM → tool → handler → result → LLM loop internally. The system prompt instructs the LLM to call `verify_task_completion`, fix issues if it fails, and retry. Back-pressure enforcement rejects `update_task_status(complete)` unless a passing `verify_*` call preceded it.

### Back-Pressure Integration

```csharp
// Registered as update_task_status tool handler
private string UpdateTaskStatus(string taskId, string status)
{
    if (status == "complete" && !_verificationTracker.HasPassedVerification(taskId))
        return """{"error":true,"message":"Call verify_task_completion first"}""";
    _state.UpdateTaskStatus(taskId, status);
    return """{"success":true}""";
}
```

### Relevance to Lopen

This is the exact pattern defined in the LLM and Core specs. The oracle runs within the primary model's SDK invocation — no additional premium request consumed. `SystemMessageMode.Replace` ensures the oracle gets a controlled prompt. `InfiniteSessions = false` keeps oracle sessions single-shot. The `VerificationTracker` enforces the mandatory oracle-before-complete rule from Back-Pressure Category 2.

---

## 7. Document Section Extraction

**Problem**: Parse markdown headers to extract specific sections for LLM context injection, minimizing token usage.

### Recommended: Markdig

[`Markdig`](https://www.nuget.org/packages/Markdig) v0.45.0 — the most popular .NET markdown parser. Full AST with YAML frontmatter support. Thread-safe pipeline.

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseYamlFrontMatter()
    .UseAdvancedExtensions()
    .Build(); // Immutable, cache and reuse

MarkdownDocument doc = Markdown.Parse(markdown, pipeline);
```

### Section Extraction Algorithm

The AST is a **flat list of blocks** — headings don't create nested sections. Walk linearly, capture from target heading until next heading of same or higher level:

```csharp
public static string GetSectionRawText(
    string originalMarkdown, MarkdownDocument doc, string headerText)
{
    var blocks = new List<Block>();
    int? sectionLevel = null;
    bool capturing = false;

    foreach (var block in doc)
    {
        if (block is HeadingBlock heading)
        {
            string text = GetHeadingPlainText(heading);
            if (!capturing && text.Equals(headerText, StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                sectionLevel = heading.Level;
                blocks.Add(block);
            }
            else if (capturing && heading.Level <= sectionLevel)
                break;
            else if (capturing)
                blocks.Add(block);
        }
        else if (capturing)
            blocks.Add(block);
    }

    if (blocks.Count == 0) return string.Empty;
    int start = blocks[0].Span.Start;
    int end = blocks[^1].Span.End;
    return originalMarkdown[start..(end + 1)];
}
```

### Why Not Regex

Regex fails on headings inside fenced code blocks (e.g., ```` ```markdown\n# Not a heading\n``` ````). Markdig's AST handles this correctly. Regex is acceptable only for quick prototyping.

### Integrated Flow

Parse → extract section → normalize → hash (XxHash128) → cache. Cache key: `file path + header + modification timestamp` per the Storage spec.

```csharp
public CachedSection? ExtractSection(string filePath, string headerText)
{
    string markdown = File.ReadAllText(filePath);
    var doc = Markdown.Parse(markdown, Pipeline);
    string sectionText = GetSectionRawText(markdown, doc, headerText);
    string contentHash = ContentHasher.ComputeHash(sectionText);

    return new CachedSection(filePath, headerText,
        new FileInfo(filePath).LastWriteTimeUtc, sectionText, contentHash);
}
```

### Relevance to Lopen

This is the foundation of the Document Management system in the Core spec. Section-level extraction means the LLM receives only `§ Authentication` when working on auth, not the entire spec. Combined with drift detection (§4), Lopen detects when specific sections change between iterations. Markdig's YAML frontmatter extension parses the `name` and `description` fields from spec files.

---

## 8. Implementation Approach

### Recommended Architecture

```
Lopen.Core/
├── Workflow/
│   ├── WorkflowOrchestrator.cs       # Stateless state machine + orchestration loop
│   ├── WorkflowStep.cs               # Enum: 7 steps
│   ├── WorkflowPhase.cs              # Enum: 3 phases
│   ├── WorkflowDefinition.cs         # Declarative transition table
│   ├── IStateAssessor.cs             # Re-entrant assessment interface
│   └── StateAssessor.cs              # Inspects codebase, session, task tree
├── Tasks/
│   ├── IWorkNode.cs                  # Homogeneous traversal interface
│   ├── WorkNode{T}.cs                # Generic composite base
│   ├── Module.cs, Component.cs, ...  # Concrete hierarchy types
│   ├── WorkNodeState.cs              # Enum + transition validation
│   └── WorkNodeExtensions.cs         # Aggregate state, Descendants(), queries
├── BackPressure/
│   ├── IGuardrail.cs                 # Guardrail interface
│   ├── GuardrailPipeline.cs          # Ordered evaluation pipeline
│   ├── BudgetTracker.cs              # Category 1: Resource limits
│   ├── ChurnDetector.cs              # Category 2: Progress integrity
│   ├── CircularBehaviorDetector.cs   # Category 2: Circular behavior
│   ├── RequiredToolVerifier.cs       # Category 3: Quality gates
│   ├── ToolCallAuditLog.cs           # Category 4: Tool discipline
│   ├── PatternDetector.cs            # Category 4: Pattern analysis
│   └── CorrectiveInstructionGenerator.cs
├── Documents/
│   ├── SpecificationParser.cs        # Markdig-based section extraction
│   ├── ContentHasher.cs              # XxHash128 drift detection
│   ├── ContentNormalizer.cs          # Line ending + whitespace normalization
│   └── SectionCache.cs              # File path + header + timestamp keyed
├── Git/
│   ├── IGitService.cs                # Interface for testability
│   ├── GitCliService.cs              # Process-based implementation
│   └── GitException.cs               # Structured error type
└── Oracle/
    ├── OracleVerificationTools.cs    # Tool handlers + sub-agent dispatch
    ├── EvidenceCollector.cs          # Diff, test results, acceptance criteria
    ├── OracleVerdict.cs              # Pass/fail verdict schema
    └── VerificationTracker.cs        # Enforces oracle-before-complete
```

### Execution Flow (Per Iteration)

```
1. WorkflowOrchestrator.RunIterationAsync()
   ├── StateAssessor.AssessCurrentStepAsync()     // Re-entrant assessment
   ├── SpecificationParser.ExtractSection()        // Load relevant context
   ├── ContentHasher.HasDrifted()                  // Check for spec drift
   ├── GuardrailPipeline.EvaluateAsync()           // Back-pressure checks
   ├── PromptBuilder.BuildSystemPrompt()           // Assemble context
   └── CopilotClient.CreateSessionAsync()          // SDK invocation
       └── [LLM tool-calling loop]
           ├── Native tools (file I/O, shell, git)
           ├── Lopen tools (read_spec, update_task_status, ...)
           └── Oracle tools (verify_task_completion → sub-agent)
```

### NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Stateless` | 5.20.1 | State machine execution, guards, diagram export |
| `Markdig` | 0.45.0 | Markdown AST parsing, section extraction |
| `GitHub.Copilot.SDK` | latest | LLM invocation, tool registration |
| `Microsoft.Extensions.AI` | latest | `AIFunctionFactory.Create` for tool definitions |

`System.IO.Hashing` (XxHash128), `System.Text.Json`, `System.Diagnostics.Process` are all in the .NET 8 BCL.

### Key Design Principles

1. **Interfaces everywhere** — `IStateAssessor`, `IGitService`, `IGuardrailPipeline` enable unit testing without real LLM/git/filesystem
2. **DI composition** — `services.AddGuardrailPipeline()`, `services.AddSingleton<IGitService, GitCliService>()` etc.
3. **Async-first** — Every I/O operation (git, SDK, file reads) is `async Task`
4. **Source-generated JSON** — AOT-safe serialization via `[JsonSerializable]` contexts
5. **TimeProvider** — All time-dependent code uses .NET 8's `TimeProvider` for deterministic testing

### Relevance to Lopen

This architecture maps cleanly to the Core spec's separation of concerns: workflow orchestration is isolated from LLM invocation (LLM module), persistence (Storage module), and configuration (Configuration module). Each subsystem (workflow, tasks, back-pressure, documents, git, oracle) is independently testable. The guardrail pipeline is the integration point for all four back-pressure categories. The `StateAssessor` is the single place where re-entrant assessment logic lives.

---

## 9. Orchestration Loop Pattern

**Problem**: All building blocks exist — `WorkflowEngine` (state machine), `ILlmService` (Copilot SDK), tool handlers, `GuardrailPipeline`, `IGitWorkflowService`, `IAutoSaveService`, `IDriftDetector` — but there is no main loop that connects them into a functioning orchestration cycle.

### Key Question: Separate Class vs. Part of WorkflowEngine?

**Recommendation: Separate `WorkflowOrchestrator` class.**

| Concern | WorkflowEngine | WorkflowOrchestrator |
|---|---|---|
| Responsibility | State machine transitions (pure logic) | Drive the assess → invoke → evaluate → save loop |
| Dependencies | `IStateAssessor`, `Stateless` | Everything: engine, LLM, guardrails, git, auto-save, drift |
| Testability | Unit-testable with fake assessor | Integration-testable with fakes for each dependency |
| Lifetime | Scoped per module run | Scoped per module run |
| Reuse | Reusable in CLI, tests, TUI | Entry point — only the CLI host calls it |

`WorkflowEngine` is already clean and focused: it manages state transitions and guards. Embedding the orchestration loop inside it would violate SRP by coupling the state machine to LLM invocation, guardrail evaluation, and persistence concerns. The orchestrator *uses* the engine, it *is not* the engine.

### The Orchestration Loop

The main loop follows an assess → invoke → process → evaluate → save → advance cycle. Each iteration is one LLM session (`SendAndWaitAsync`) which internally handles the tool-calling loop.

```csharp
public sealed class WorkflowOrchestrator
{
    private readonly IWorkflowEngine _engine;
    private readonly ILlmService _llmService;
    private readonly IGuardrailPipeline _guardrails;
    private readonly IDriftDetector _driftDetector;
    private readonly IAutoSaveService _autoSave;
    private readonly IGitWorkflowService _gitWorkflow;
    private readonly IStateAssessor _assessor;
    private readonly IOutputRenderer _renderer;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    // Constructor: inject all dependencies

    public async Task RunAsync(string moduleName, CancellationToken cancellationToken)
    {
        // 1. Initialize: assess reality and set starting step
        await _engine.InitializeAsync(moduleName, cancellationToken);
        await _gitWorkflow.EnsureModuleBranchAsync(moduleName, cancellationToken);

        // 2. Main loop — runs until workflow is complete or cancelled
        while (!_engine.IsComplete && !cancellationToken.IsCancellationRequested)
        {
            await RunIterationAsync(moduleName, cancellationToken);
        }
    }

    private async Task RunIterationAsync(string moduleName, CancellationToken ct)
    {
        var step = _engine.CurrentStep;
        var phase = _engine.CurrentPhase;

        // ── Step 1: Drift detection on re-entry ──
        await AssessDriftAsync(moduleName, ct);

        // ── Step 2: Build guardrail context and evaluate ──
        var guardrailCtx = BuildGuardrailContext(moduleName, step);
        var guardrailResults = await _guardrails.EvaluateAsync(guardrailCtx, ct);

        if (guardrailResults.Any(r => r.Outcome == GuardrailOutcome.Block))
        {
            await _renderer.RenderErrorAsync(
                "Guardrail blocked execution", exception: null, ct);
            return; // Do not invoke LLM — wait for human intervention
        }

        // ── Step 3: Build prompt with corrective instructions ──
        var systemPrompt = BuildSystemPrompt(step, phase, guardrailResults);

        // ── Step 4: Invoke LLM (SDK handles tool-calling loop internally) ──
        var result = await _llmService.InvokeAsync(
            systemPrompt, SelectModel(phase), GetToolsForStep(step), ct);

        // ── Step 5: Post-invocation save ──
        await _autoSave.SaveAsync(
            AutoSaveTrigger.IterationComplete,
            _sessionId, BuildSessionState(), BuildMetrics(result), ct);

        // ── Step 6: Determine trigger and advance state machine ──
        var trigger = DetermineTrigger(step, result);
        if (trigger.HasValue)
        {
            _engine.Fire(trigger.Value);

            // Auto-commit on task completion
            if (trigger == WorkflowTrigger.TaskIterationComplete
                || trigger == WorkflowTrigger.ComponentComplete)
            {
                await _gitWorkflow.CommitTaskCompletionAsync(
                    moduleName, _currentComponent, _currentTask, ct);
            }
        }

        await _renderer.RenderProgressAsync(
            phase.ToString(), step.ToString(), ComputeProgress(), ct);
    }
}
```

### Async Event-Driven Nature of the Copilot SDK

The Copilot SDK's `SendAndWaitAsync` is a **blocking async call** that internally runs the full LLM → tool → handler → result → LLM loop. The orchestrator does *not* need to implement the tool-calling loop — the SDK does this. Each `SendAndWaitAsync` call is one "iteration" from the orchestrator's perspective.

```
┌─────────────────────────────────────────────────┐
│  Orchestrator (our code)                        │
│                                                 │
│  while (!complete)                              │
│    assess → guardrails → build prompt           │
│    ┌─────────────────────────────────────────┐  │
│    │  SDK: SendAndWaitAsync (blocking async)  │  │
│    │    LLM response                         │  │
│    │    → tool call → handler → result       │  │
│    │    → LLM response                       │  │
│    │    → tool call → handler → result       │  │
│    │    → ... (SDK manages internally)       │  │
│    │    → final response (no more tool calls)│  │
│    └─────────────────────────────────────────┘  │
│    save state → advance state machine           │
│  end while                                      │
└─────────────────────────────────────────────────┘
```

**Key implications:**
- Guardrails run *between* SDK invocations, not *within* the tool-calling loop. The SDK's `SessionHooks` (`OnToolCallReceived`, `OnErrorOccurred`) provide the only injection points during an invocation.
- Tool handlers registered via `AIFunctionFactory.Create` execute synchronously within the SDK's loop. Guardrail checks that must run per-tool-call should be embedded in tool handler wrappers.
- The `ToolCallAuditLog` should be populated by tool handler wrappers that intercept calls and log them before delegating to the real handler.

### Error Propagation Through the Loop

Errors fall into three categories with distinct handling:

```csharp
private async Task RunIterationAsync(string moduleName, CancellationToken ct)
{
    try
    {
        // ... iteration body ...
    }
    catch (OperationCanceledException)
    {
        // Category 1: Cancellation — propagate immediately, do not save
        throw;
    }
    catch (LlmException ex) when (ex.IsRetryable)
    {
        // Category 2: Transient LLM failures — log, save state, retry on next iteration
        _logger.LogWarning(ex, "Transient LLM error, will retry");
        await _autoSave.SaveAsync(AutoSaveTrigger.Error, _sessionId, BuildSessionState(), null, ct);
        // Do NOT throw — loop continues on next iteration
    }
    catch (LlmException ex)
    {
        // Category 3: Fatal LLM failures — save state and surface to user
        _logger.LogError(ex, "Fatal LLM error");
        await _autoSave.SaveAsync(AutoSaveTrigger.Error, _sessionId, BuildSessionState(), null, ct);
        await _renderer.RenderErrorAsync(ex.Message, ex, ct);
        throw; // Exits the loop
    }
    catch (Exception ex)
    {
        // Category 4: Unexpected — save what we can, surface to user
        _logger.LogError(ex, "Unexpected error in orchestration loop");
        try { await _autoSave.SaveAsync(AutoSaveTrigger.Error, _sessionId, BuildSessionState(), null, ct); }
        catch { /* fail-open: don't mask the original error */ }
        throw;
    }
}
```

**Principle**: Always attempt to save state before propagating fatal errors. This ensures re-entrant resume works even after crashes. `IAutoSaveService.SaveAsync` already swallows `StorageException` internally, so calling it in error paths is safe.

### Cancellation Token Flow

The `CancellationToken` flows from the CLI host through every async boundary:

```
CLI Host (Ctrl+C handler)
  → WorkflowOrchestrator.RunAsync(ct)
    → RunIterationAsync(ct)
      → IGuardrailPipeline.EvaluateAsync(ctx, ct)
      → ILlmService.InvokeAsync(prompt, model, tools, ct)
        → CopilotClient.CreateSessionAsync(config, ct)
        → session.SendAndWaitAsync(options, timeout, ct)
      → IAutoSaveService.SaveAsync(trigger, ..., ct)
      → IGitWorkflowService.CommitTaskCompletionAsync(..., ct)
```

The SDK's `SendAndWaitAsync` accepts a `CancellationToken` and a `TimeSpan` timeout. On cancellation:
1. The SDK aborts the current tool-calling loop
2. `OperationCanceledException` propagates up to `RunIterationAsync`
3. The orchestrator saves state (if possible) before exiting
4. Re-entry on next run resumes from the last persisted step

### Trigger Determination Logic

After each LLM invocation, the orchestrator must determine which trigger to fire based on the current step and the LLM's output:

```csharp
private WorkflowTrigger? DetermineTrigger(WorkflowStep step, LlmInvocationResult result)
{
    return step switch
    {
        // Requirement Gathering → wait for human approval (not auto-advanced)
        WorkflowStep.DraftSpecification => null,

        // Planning steps → auto-advance when LLM indicates completion
        WorkflowStep.DetermineDependencies => WorkflowTrigger.DependenciesDetermined,
        WorkflowStep.IdentifyComponents => WorkflowTrigger.ComponentsIdentified,
        WorkflowStep.SelectNextComponent => WorkflowTrigger.ComponentSelected,
        WorkflowStep.BreakIntoTasks => WorkflowTrigger.TasksBrokenDown,

        // Building → check if tasks/components remain
        WorkflowStep.IterateThroughTasks => DetermineIterationTrigger(),
        WorkflowStep.Repeat => DetermineRepeatTrigger(),

        _ => null,
    };
}

private WorkflowTrigger DetermineIterationTrigger()
{
    // If all tasks in current component are done → ComponentComplete
    // Otherwise → TaskIterationComplete (re-enters Step 6 for next task)
    return _taskTracker.AllTasksComplete(_currentComponent)
        ? WorkflowTrigger.ComponentComplete
        : WorkflowTrigger.TaskIterationComplete;
}

private WorkflowTrigger DetermineRepeatTrigger()
{
    // If more components → Assess (loops to Step 4)
    // Otherwise → ModuleComplete (but this is fired from Step 4)
    return WorkflowTrigger.Assess;
}
```

---

## 10. DI Registration Gap

**Problem**: `WorkflowEngine`, `IStateAssessor`, and `IPhaseTransitionController` are implemented but not registered in `ServiceCollectionExtensions.AddLopenCore()`. The current registration covers documents, guardrails, git, and rendering — but not the workflow subsystem itself.

### Current State of DI Registration

```csharp
// ✅ Registered in AddLopenCore():
services.AddSingleton<IGitService>(sp => new GitCliService(...));
services.AddSingleton<IGitWorkflowService, GitWorkflowService>();
services.AddSingleton<IRevertService, RevertService>();
services.AddSingleton<IModuleScanner>(sp => new ModuleScanner(...));
services.AddSingleton<IModuleLister, ModuleLister>();
services.AddSingleton<ISpecificationParser, MarkdigSpecificationParser>();
services.AddSingleton<IContentHasher, XxHashContentHasher>();
services.AddSingleton<IDriftDetector, DriftDetector>();
services.AddSingleton<ISectionExtractor, SectionExtractor>();
services.AddSingleton<IGuardrail>(...);  // ToolDisciplineGuardrail
services.AddSingleton<IGuardrail>(...);  // QualityGateGuardrail
services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();
services.TryAddSingleton<IOutputRenderer>(new HeadlessRenderer());

// ❌ NOT registered:
// IStateAssessor / StateAssessor
// IPhaseTransitionController / PhaseTransitionController
// IWorkflowEngine / WorkflowEngine
// WorkflowOrchestrator (does not exist yet)
```

### Recommended Registration Pattern

```csharp
public static IServiceCollection AddLopenCore(this IServiceCollection services, string? projectRoot = null)
{
    // ... existing registrations ...

    // ── Workflow subsystem ──
    services.AddSingleton<IPhaseTransitionController, PhaseTransitionController>();
    services.AddSingleton<IStateAssessor, StateAssessor>();
    services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
    services.AddSingleton<WorkflowOrchestrator>();

    return services;
}
```

### Lifetime Analysis

| Type | Lifetime | Rationale |
|---|---|---|
| `IStateAssessor` | **Singleton** | Stateless service — assesses reality on every call. No mutable per-request state. |
| `IPhaseTransitionController` | **Singleton** | Holds approval state (`IsRequirementGatheringToPlannningApproved`) that persists across the full module run. Single module at a time. |
| `IWorkflowEngine` | **Singleton** | Holds `_currentStep` and Stateless machine. One workflow per process. `InitializeAsync()` resets state for each module. |
| `WorkflowOrchestrator` | **Singleton** | Stateful only during `RunAsync()`. Single module run per process lifetime. |

**Why Singleton over Scoped**: Lopen is a CLI application, not a web app. There is no HTTP request scope. The DI container is built once in `Program.cs` and lives for the process lifetime. `Scoped` in a non-web context requires manually creating scopes, which adds complexity without benefit. All per-module state is initialized via `InitializeAsync()` / `RunAsync()`.

**Alternative — Scoped with manual scope**: If future multi-module parallelism is needed, convert to `Scoped` and create `IServiceScope` per module run. But the current spec is explicitly single-module-at-a-time.

### StateAssessor Dependency Consideration

`StateAssessor` likely depends on `ISessionManager` (from `Lopen.Storage`) to read persisted state. If `StateAssessor` requires `projectRoot`, use the same factory pattern already used for `IGitService`:

```csharp
services.AddSingleton<IStateAssessor>(sp =>
    new StateAssessor(
        sp.GetRequiredService<ISessionManager>(),
        sp.GetRequiredService<IModuleScanner>(),
        sp.GetRequiredService<ILogger<StateAssessor>>(),
        projectRoot));
```

---

## 11. Integration Wiring Patterns

**Problem**: Individual subsystems (guardrails, drift detection, auto-save, git workflow) are implemented but not connected to the orchestration loop. This section describes the specific wiring patterns.

### 11.1 GuardrailPipeline into the Iteration Step

The guardrail pipeline evaluates *between* LLM invocations, not within them. It acts as a pre-invocation gate.

```csharp
// In WorkflowOrchestrator.RunIterationAsync():
var guardrailCtx = new GuardrailContext
{
    Step = _engine.CurrentStep,
    Phase = _engine.CurrentPhase,
    ModuleName = moduleName,
    TaskName = _currentTask,
    ComponentName = _currentComponent,
    ToolCallAuditLog = _auditLog,
    TokensConsumed = _metrics.TotalTokens,
    IterationCount = _iterationCount,
};

var results = await _guardrails.EvaluateAsync(guardrailCtx, ct);

// Partition results by outcome
var blocks = results.Where(r => r.Outcome == GuardrailOutcome.Block).ToList();
var warnings = results.Where(r => r.Outcome == GuardrailOutcome.Warn).ToList();

if (blocks.Count > 0)
{
    // Hard stop — render errors and exit iteration without invoking LLM
    foreach (var block in blocks)
        await _renderer.RenderErrorAsync($"Blocked: {block.Message}", null, ct);
    return;
}

// Warnings become corrective instructions appended to the system prompt
var correctiveInstructions = warnings
    .Select(w => w.CorrectiveInstruction)
    .Where(i => i is not null)
    .ToList();
```

**Per-tool-call guardrails** (within the SDK's internal loop) require tool handler wrappers:

```csharp
// Wrap tool handlers to intercept and audit each tool call
public class AuditingToolWrapper
{
    private readonly Func<string, Task<string>> _innerHandler;
    private readonly ToolCallAuditLog _auditLog;

    public async Task<string> HandleAsync(string input)
    {
        _auditLog.Record(new ToolCallEntry(ToolName, input, DateTimeOffset.UtcNow));
        var result = await _innerHandler(input);
        _auditLog.RecordResult(ToolName, result);
        return result;
    }
}
```

### 11.2 DriftDetector into Re-Entry Assessment

Drift detection runs at the *start* of each iteration (not at the end) to catch spec changes that happened while the orchestrator was paused or between runs.

```csharp
private async Task AssessDriftAsync(string moduleName, CancellationToken ct)
{
    var specPath = _moduleScanner.GetSpecificationPath(moduleName);
    if (specPath is null) return;

    var currentContent = await File.ReadAllTextAsync(specPath, ct);
    var cachedSections = _sectionCache.GetCachedSections(specPath);

    var driftResults = _driftDetector.DetectDrift(specPath, currentContent, cachedSections);

    if (driftResults.Count == 0) return;

    var driftedSections = driftResults
        .Where(d => !d.IsNew && !d.IsRemoved)
        .Select(d => d.Header)
        .ToList();

    if (driftedSections.Count > 0)
    {
        _logger.LogWarning("Spec drift detected in sections: {Sections}",
            string.Join(", ", driftedSections));

        // Determine if drift requires re-assessment of workflow step
        var reEntryStep = DetermineReEntryStep(driftedSections);
        if (reEntryStep.HasValue && reEntryStep.Value != _engine.CurrentStep)
        {
            _logger.LogInformation("Drift requires re-entry at step {Step}", reEntryStep);
            await _engine.InitializeAsync(moduleName, ct); // Re-assess from reality
        }

        // Invalidate cached sections that drifted
        foreach (var drift in driftResults)
            _sectionCache.Invalidate(specPath, drift.Header);
    }
}

/// <summary>
/// Maps drifted spec sections to the earliest workflow step that must be re-evaluated.
/// </summary>
private static WorkflowStep? DetermineReEntryStep(IReadOnlyList<string> driftedSections)
{
    // Acceptance Criteria drift → re-assess from Step 3 (component identification may change)
    if (driftedSections.Any(s => s.Contains("Acceptance Criteria", StringComparison.OrdinalIgnoreCase)))
        return WorkflowStep.IdentifyComponents;

    // Dependencies drift → re-assess from Step 2
    if (driftedSections.Any(s => s.Contains("Dependencies", StringComparison.OrdinalIgnoreCase)))
        return WorkflowStep.DetermineDependencies;

    // Any other drift → flag but don't force re-entry
    return null;
}
```

### 11.3 AutoSaveService into Workflow Events

`IAutoSaveService` (from `Lopen.Storage`) triggers at specific workflow boundaries, not on a timer. The orchestrator calls it at four points:

```
Save Points:
  1. After each LLM invocation completes       → AutoSaveTrigger.IterationComplete
  2. After a state machine transition           → AutoSaveTrigger.StepTransition
  3. On error (before propagating)              → AutoSaveTrigger.Error
  4. On workflow completion                     → AutoSaveTrigger.WorkflowComplete
```

```csharp
// In RunIterationAsync(), after LLM invocation:
await _autoSave.SaveAsync(
    AutoSaveTrigger.IterationComplete,
    _sessionId,
    new SessionState
    {
        ModuleName = moduleName,
        CurrentStep = _engine.CurrentStep,
        CurrentPhase = _engine.CurrentPhase,
        CurrentComponent = _currentComponent,
        CurrentTask = _currentTask,
        TaskHierarchy = _taskHierarchy,
        SectionHashes = _sectionCache.GetAllHashes(),
    },
    new SessionMetrics
    {
        TotalTokens = _metrics.TotalTokens,
        ToolCallCount = _metrics.ToolCallCount,
        IterationCount = _iterationCount,
    },
    ct);

// After state machine transition:
if (trigger.HasValue && _engine.Fire(trigger.Value))
{
    await _autoSave.SaveAsync(
        AutoSaveTrigger.StepTransition,
        _sessionId, BuildSessionState(), BuildMetrics(result), ct);
}
```

**Key design**: `IAutoSaveService.SaveAsync` is fire-and-forget-safe — it catches `StorageException` internally. The orchestrator should still `await` it (for backpressure), but a save failure must never crash the loop.

### 11.4 GitWorkflowService into Task Completion

`IGitWorkflowService` integrates at two points in the orchestration loop:

**1. Module start — branch creation:**
```csharp
public async Task RunAsync(string moduleName, CancellationToken ct)
{
    await _engine.InitializeAsync(moduleName, ct);

    // Create lopen/{moduleName} branch if it doesn't exist
    var branchResult = await _gitWorkflow.EnsureModuleBranchAsync(moduleName, ct);
    if (branchResult is not null)
        _logger.LogInformation("Working on branch: {Branch}", branchResult.Output);

    while (!_engine.IsComplete && !ct.IsCancellationRequested)
        await RunIterationAsync(moduleName, ct);
}
```

**2. Task/component completion — auto-commit:**
```csharp
// In RunIterationAsync(), after state machine advances:
if (trigger == WorkflowTrigger.TaskIterationComplete)
{
    await _gitWorkflow.CommitTaskCompletionAsync(
        moduleName, _currentComponent, _currentTask, ct);
}
else if (trigger == WorkflowTrigger.ComponentComplete)
{
    await _gitWorkflow.CommitTaskCompletionAsync(
        moduleName, _currentComponent, "component-complete", ct);
}
```

The commit message format from `IGitWorkflowService.FormatCommitMessage` follows conventional commits: `feat(moduleName): complete taskName in componentName`.

### 11.5 Complete Wiring Diagram

```
WorkflowOrchestrator.RunAsync(moduleName)
│
├── engine.InitializeAsync()          ← IStateAssessor reads persisted state
├── gitWorkflow.EnsureModuleBranch()  ← IGitWorkflowService creates branch
│
└── while (!complete)
    │
    ├── AssessDriftAsync()            ← IDriftDetector + ISectionCache
    │   └── engine.InitializeAsync()  ← Re-assess if drift requires it
    │
    ├── guardrails.EvaluateAsync()    ← IGuardrailPipeline (all 4 categories)
    │   ├── Block? → render error, skip iteration
    │   └── Warn? → append corrective instructions to prompt
    │
    ├── llmService.InvokeAsync()      ← ILlmService (Copilot SDK)
    │   └── [SDK tool-calling loop]
    │       ├── Tool handlers record to ToolCallAuditLog
    │       └── Oracle tools dispatch sub-agent
    │
    ├── autoSave.SaveAsync()          ← IAutoSaveService (iteration complete)
    │
    ├── DetermineTrigger()
    ├── engine.Fire(trigger)
    │
    ├── gitWorkflow.CommitAsync()     ← IGitWorkflowService (on task/component done)
    └── autoSave.SaveAsync()          ← IAutoSaveService (step transition)
```

### 11.6 Full DI Registration (All Subsystems)

The complete `AddLopenCore` method after wiring all subsystems:

```csharp
public static IServiceCollection AddLopenCore(this IServiceCollection services, string? projectRoot = null)
{
    // ── Git ──
    if (!string.IsNullOrWhiteSpace(projectRoot))
    {
        services.AddSingleton<IGitService>(sp =>
            new GitCliService(
                sp.GetRequiredService<ILogger<GitCliService>>(),
                projectRoot));
        services.AddSingleton<IGitWorkflowService, GitWorkflowService>();
        services.AddSingleton<IRevertService, RevertService>();
        services.AddSingleton<IModuleScanner>(sp =>
            new ModuleScanner(
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<ILogger<ModuleScanner>>(),
                projectRoot));
        services.AddSingleton<IModuleLister, ModuleLister>();
    }

    // ── Documents ──
    services.AddSingleton<ISpecificationParser, MarkdigSpecificationParser>();
    services.AddSingleton<IContentHasher, XxHashContentHasher>();
    services.AddSingleton<IDriftDetector, DriftDetector>();
    services.AddSingleton<ISectionExtractor, SectionExtractor>();

    // ── Guardrails ──
    services.AddSingleton<IGuardrail>(sp => /* ... ToolDisciplineGuardrail ... */);
    services.AddSingleton<IGuardrail>(sp => /* ... QualityGateGuardrail ... */);
    services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();

    // ── Workflow (NEW) ──
    services.AddSingleton<IPhaseTransitionController, PhaseTransitionController>();
    services.AddSingleton<IStateAssessor, StateAssessor>();
    services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
    services.AddSingleton<WorkflowOrchestrator>();

    // ── Output ──
    services.TryAddSingleton<IOutputRenderer>(new HeadlessRenderer());

    return services;
}
```

### Relevance to Lopen

These wiring patterns complete the gap between implemented subsystems and a functioning orchestrator. The `WorkflowOrchestrator` is the single entry point that the CLI host calls — `orchestrator.RunAsync("auth", ct)`. All subsystem integration happens through DI-injected interfaces, maintaining the testability principle established in the existing architecture. The auto-save and error-handling patterns ensure re-entrant resume works even after crashes, which is a core requirement of the spec.

---

## Detailed Research Documents

Each topic has a detailed research document with full code examples:

- [RESEARCH-state-machine.md](RESEARCH-state-machine.md) — Three state machine patterns evaluated with full C# examples
- [RESEARCH-hierarchical-task-data-structures.md](RESEARCH-hierarchical-task-data-structures.md) — Composite pattern, state tracking, JSON serialization, visitor/query
- [RESEARCH-backpressure.md](RESEARCH-backpressure.md) — All four back-pressure categories with complete implementations
- [RESEARCH-markdown-parsing.md](RESEARCH-markdown-parsing.md) — Markdig vs regex, content hashing, drift detection, caching
- [RESEARCH-git-integration.md](RESEARCH-git-integration.md) — LibGit2Sharp vs Process-based git with API examples
- [RESEARCH-oracle-verification.md](../llm/RESEARCH-oracle-verification.md) — Copilot SDK tool handlers, evidence collection, retry loop

## References

- [Core Specification](SPECIFICATION.md) — The specification this research supports
- [LLM Specification](../llm/SPECIFICATION.md) — SDK integration, tool registration, oracle dispatch
- [Storage Specification](../storage/SPECIFICATION.md) — Session persistence, caching, document formats
- [Configuration Specification](../configuration/SPECIFICATION.md) — Budget settings, tool discipline thresholds
