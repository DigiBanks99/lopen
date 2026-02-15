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
