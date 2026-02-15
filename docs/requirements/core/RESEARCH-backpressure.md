# Research: Back-Pressure & Guardrail Implementation Patterns

Research into C# (.NET 8+) patterns for implementing the four categories of back-pressure defined in [Core § Back-Pressure](SPECIFICATION.md#back-pressure).

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Category 1: Budget Tracker (Resource Limits)](#category-1-budget-tracker-resource-limits)
- [Category 2: Churn & Circular Behavior Detection (Progress Integrity)](#category-2-churn--circular-behavior-detection-progress-integrity)
- [Category 3: Quality Gates](#category-3-quality-gates)
- [Category 4: Tool Call Auditor (Tool Discipline)](#category-4-tool-call-auditor-tool-discipline)
- [Guardrail Pipeline (Composition)](#guardrail-pipeline-composition)
- [DI Registration & Integration](#di-registration--integration)
- [Design Decisions](#design-decisions)
- [References](#references)

---

## Architecture Overview

The back-pressure system is composed as a **guardrail pipeline** — an ordered chain of independent checks that run before each orchestration loop iteration. Each guardrail evaluates the current state and returns one of three verdicts: **Pass**, **Warn**, or **Block**.

```
┌─────────────────────────────────────────────────────────┐
│                   Orchestration Loop                     │
│                                                         │
│  ┌──────────────┐   ┌──────────────┐   ┌────────────┐  │
│  │  Budget       │──▶│  Churn       │──▶│  Quality   │  │
│  │  Guardrail    │   │  Guardrail   │   │  Gate      │  │
│  │  (Order=100)  │   │  (Order=200) │   │  (Order=300)│ │
│  └──────────────┘   └──────────────┘   └────────────┘  │
│         │                   │                  │         │
│         ▼                   ▼                  ▼         │
│  ┌──────────────┐   ┌──────────────────────────────┐    │
│  │  Tool         │   │  GuardrailPipelineResult     │    │
│  │  Discipline   │──▶│  (aggregated Pass/Warn/Block)│    │
│  │  (Order=400)  │   └──────────────────────────────┘    │
│  └──────────────┘                                        │
└─────────────────────────────────────────────────────────┘
```

**Key patterns used:**

| Pattern | .NET Reference | Application |
|---|---|---|
| Chain of Responsibility | ASP.NET Middleware pipeline | Ordered guardrail evaluation |
| Pipeline Behaviors | MediatR `IPipelineBehavior<,>` | Pre/post-iteration processing |
| Policy-based Authorization | `IAuthorizationHandler` | Pass/Warn/Block verdict model |
| Decorator/Proxy | `DelegatingHandler` in `HttpClient` | Tool call auditing wrapper |
| Observer | C# `event` / `EventHandler<T>` | Budget threshold notifications |
| Options Pattern | `IOptions<T>` | Configurable thresholds |

---

## Category 1: Budget Tracker (Resource Limits)

Tracks per-module token and premium request consumption against configurable budgets. Implements soft limits: warns at `budget.warning_threshold` (default 80%), pauses for confirmation at `budget.confirmation_threshold` (default 90%), but never hard-blocks.

### Configuration Records

```csharp
namespace Lopen.Core.BackPressure;

public record BudgetThreshold(double Fraction, BudgetSeverity Severity);

public enum BudgetSeverity { Info, Warning, ConfirmationRequired }

public record BudgetSettings
{
    public long TokenBudgetPerModule { get; init; }  // 0 = unlimited
    public int PremiumRequestBudget { get; init; }   // 0 = unlimited
    public double WarningThreshold { get; init; } = 0.8;
    public double ConfirmationThreshold { get; init; } = 0.9;

    public IReadOnlyList<BudgetThreshold> GetOrderedThresholds() =>
    [
        new(WarningThreshold, BudgetSeverity.Warning),
        new(ConfirmationThreshold, BudgetSeverity.ConfirmationRequired),
    ];
}
```

### Thread-Safe Budget Tracker

```csharp
namespace Lopen.Core.BackPressure;

/// <summary>
/// Tracks token and premium request consumption against configurable budgets.
/// Thread-safe via Interlocked. Soft-limit: warns but never hard-blocks.
/// </summary>
public sealed class BudgetTracker
{
    private long _tokensConsumed;
    private int _premiumRequests;
    private int _lastTokenThresholdIndex = -1;
    private int _lastPremiumThresholdIndex = -1;

    public BudgetSettings Settings { get; }
    public string ModuleId { get; }

    public event EventHandler<BudgetThresholdCrossedEventArgs>? ThresholdCrossed;

    public BudgetTracker(string moduleId, BudgetSettings settings)
    {
        ModuleId = moduleId;
        Settings = settings;
    }

    public long TokensConsumed => Interlocked.Read(ref _tokensConsumed);
    public int PremiumRequests => Interlocked.CompareExchange(ref _premiumRequests, 0, 0);

    public BudgetSnapshot GetSnapshot() => new(
        ModuleId, TokensConsumed, Settings.TokenBudgetPerModule,
        PremiumRequests, Settings.PremiumRequestBudget);

    /// <summary>Records token usage. Always succeeds (soft limit).</summary>
    public BudgetCheckResult RecordTokenUsage(long tokens)
    {
        var newTotal = Interlocked.Add(ref _tokensConsumed, tokens);
        return CheckThresholds(newTotal, Settings.TokenBudgetPerModule,
            ref _lastTokenThresholdIndex, BudgetDimension.Tokens);
    }

    public BudgetCheckResult RecordPremiumRequest()
    {
        var newTotal = Interlocked.Increment(ref _premiumRequests);
        return CheckThresholds(newTotal, Settings.PremiumRequestBudget,
            ref _lastPremiumThresholdIndex, BudgetDimension.PremiumRequests);
    }

    private BudgetCheckResult CheckThresholds(
        long currentValue, long budget,
        ref int lastThresholdIndex, BudgetDimension dimension)
    {
        if (budget <= 0)
            return BudgetCheckResult.Ok;

        var fraction = (double)currentValue / budget;
        var thresholds = Settings.GetOrderedThresholds();

        for (int i = thresholds.Count - 1; i >= 0; i--)
        {
            if (fraction < thresholds[i].Fraction)
                continue;

            // CAS prevents duplicate notifications under concurrency
            var previous = Interlocked.CompareExchange(ref lastThresholdIndex, i, i - 1);
            if (previous >= i)
                return new BudgetCheckResult(thresholds[i].Severity, fraction, AlreadyNotified: true);

            ThresholdCrossed?.Invoke(this, new BudgetThresholdCrossedEventArgs(
                ModuleId, dimension, thresholds[i], fraction, currentValue, budget));

            return new BudgetCheckResult(thresholds[i].Severity, fraction, AlreadyNotified: false);
        }

        return BudgetCheckResult.Ok;
    }
}
```

### Supporting Types

```csharp
namespace Lopen.Core.BackPressure;

public enum BudgetDimension { Tokens, PremiumRequests }

public readonly record struct BudgetCheckResult(
    BudgetSeverity Severity, double FractionUsed, bool AlreadyNotified = false)
{
    public static readonly BudgetCheckResult Ok = new(BudgetSeverity.Info, 0);
    public bool RequiresConfirmation =>
        Severity == BudgetSeverity.ConfirmationRequired && !AlreadyNotified;
}

public record BudgetSnapshot(
    string ModuleId, long TokensConsumed, long TokenBudget,
    int PremiumRequestsUsed, int PremiumRequestBudget)
{
    public double TokenFraction => TokenBudget > 0
        ? (double)TokensConsumed / TokenBudget : 0;
    public double PremiumFraction => PremiumRequestBudget > 0
        ? (double)PremiumRequestsUsed / PremiumRequestBudget : 0;
    public string FormatTokenUsage() => TokenBudget > 0
        ? $"{TokensConsumed:N0}/{TokenBudget:N0} ({TokenFraction:P0})"
        : $"{TokensConsumed:N0} (unlimited)";
}

public sealed class BudgetThresholdCrossedEventArgs(
    string moduleId, BudgetDimension dimension, BudgetThreshold threshold,
    double currentFraction, long currentValue, long budget) : EventArgs
{
    public string ModuleId => moduleId;
    public BudgetDimension Dimension => dimension;
    public BudgetThreshold Threshold => threshold;
    public double CurrentFraction => currentFraction;
    public long CurrentValue => currentValue;
    public long Budget => budget;
}
```

### Thread-Safety Notes

- `Interlocked.Add` for 64-bit token counter (atomic on x86 and ARM)
- `Interlocked.Increment` for 32-bit premium request counter
- `Interlocked.CompareExchange` for fire-once threshold tracking — prevents duplicate notifications
- `Interlocked.Read` for safe reads of 64-bit values (necessary on 32-bit platforms)

### Why Events over IObservable

| Concern | `event` | `IObservable<T>` |
|---|---|---|
| Subscriber count | Fixed/small (TUI, logger) ✅ | Overkill |
| Backpressure/filtering | Not needed | Rx operators shine here |
| Lifetime management | Simple `+=`/`-=` | Requires `IDisposable` |
| Async subscribers | Awkward | `SelectMany` handles well |

**Recommendation**: Use C# events. The subscriber set is small (TUI panel + telemetry logger), notifications are rare, and Rx operators aren't needed. Wrap with `Observable.FromEvent` later if needed.

---

## Category 2: Churn & Circular Behavior Detection (Progress Integrity)

### Churn Detector — Per-Key Failure Counter

Tracks consecutive failures per task. When the same task fails N times (configurable, default 3), escalates.

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Concurrent;

public record FailureEvent(string TaskId, string Reason, DateTimeOffset OccurredAt);

public sealed class ChurnDetector
{
    private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();
    private readonly ConcurrentDictionary<string, List<FailureEvent>> _history = new();
    private readonly int _threshold;

    public ChurnDetector(int threshold = 3) => _threshold = threshold;

    public ChurnResult RecordFailure(string taskId, string reason)
    {
        var count = _consecutiveFailures.AddOrUpdate(taskId, 1, (_, prev) => prev + 1);

        _history.AddOrUpdate(
            taskId,
            _ => [new FailureEvent(taskId, reason, DateTimeOffset.UtcNow)],
            (_, list) =>
            {
                lock (list) { list.Add(new(taskId, reason, DateTimeOffset.UtcNow)); }
                return list;
            });

        return count >= _threshold
            ? new ChurnResult.EscalationRequired(taskId, count, GetHistory(taskId))
            : new ChurnResult.Ok(taskId, count);
    }

    public void RecordSuccess(string taskId) =>
        _consecutiveFailures.TryRemove(taskId, out _);

    public IReadOnlyList<FailureEvent> GetHistory(string taskId) =>
        _history.TryGetValue(taskId, out var list) ? list.AsReadOnly() : [];
}

/// <summary>Discriminated union — forces callers to handle escalation.</summary>
public abstract record ChurnResult(string TaskId, int FailureCount)
{
    public sealed record Ok(string TaskId, int FailureCount)
        : ChurnResult(TaskId, FailureCount);
    public sealed record EscalationRequired(
        string TaskId, int FailureCount, IReadOnlyList<FailureEvent> History)
        : ChurnResult(TaskId, FailureCount);
}
```

### Circular Behavior Detector — Frequency Counter Per Resource

Tracks when the same resource is accessed repeatedly. Content hashing distinguishes "read same file 3 times, it hasn't changed" (circular) from "read same file 3 times, it changed each time" (legitimate).

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Concurrent;

public readonly record struct ResourceAction(string ResourceId, string ActionType);

public sealed class CircularBehaviorDetector
{
    private readonly ConcurrentDictionary<ResourceAction, CircularTracker> _trackers = new();
    private readonly int _defaultThreshold;

    public CircularBehaviorDetector(int defaultThreshold = 3) =>
        _defaultThreshold = defaultThreshold;

    public CircularCheckResult Track(ResourceAction action, string? contentHash = null)
    {
        var tracker = _trackers.GetOrAdd(action, _ => new CircularTracker());
        var (count, isRepeatWithoutChange) = tracker.Record(contentHash);

        if (count >= _defaultThreshold && isRepeatWithoutChange)
            return new CircularCheckResult.InterventionNeeded(action, count,
                $"'{action.ResourceId}' has been {action.ActionType}'d {count} times without change");

        if (count >= _defaultThreshold)
            return new CircularCheckResult.Warning(action, count);

        return new CircularCheckResult.Ok(action, count);
    }

    /// <summary>Reset for a new iteration/task context.</summary>
    public void ResetIteration() => _trackers.Clear();
}

internal sealed class CircularTracker
{
    private int _count;
    private string? _lastContentHash;
    private readonly object _lock = new();

    public (int Count, bool IsRepeatWithoutChange) Record(string? contentHash)
    {
        lock (_lock)
        {
            _count++;
            var unchanged = contentHash is not null && contentHash == _lastContentHash;
            _lastContentHash = contentHash;
            return (_count, unchanged || contentHash is null);
        }
    }
}

public abstract record CircularCheckResult(ResourceAction Action, int Count)
{
    public sealed record Ok(ResourceAction Action, int Count)
        : CircularCheckResult(Action, Count);
    public sealed record Warning(ResourceAction Action, int Count)
        : CircularCheckResult(Action, Count);
    public sealed record InterventionNeeded(ResourceAction Action, int Count, string Message)
        : CircularCheckResult(Action, Count);
}
```

### Sliding Window Counter — Time-Windowed Ring Buffer

Enables "3 failures in the last 10 minutes" semantics vs "3 failures ever".

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Concurrent;

/// <summary>
/// Thread-safe sliding window counter. Counts events within a time window.
/// Uses TimeProvider (.NET 8) for testability.
/// </summary>
public sealed class SlidingWindowCounter
{
    private readonly TimeSpan _window;
    private readonly ConcurrentQueue<long> _timestamps = new();
    private readonly TimeProvider _timeProvider;

    public SlidingWindowCounter(TimeSpan window, TimeProvider? timeProvider = null)
    {
        _window = window;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int Record()
    {
        var now = _timeProvider.GetUtcNow().Ticks;
        _timestamps.Enqueue(now);
        Evict(now);
        return Count;
    }

    public int Count
    {
        get
        {
            Evict(_timeProvider.GetUtcNow().Ticks);
            return _timestamps.Count;
        }
    }

    private void Evict(long nowTicks)
    {
        var cutoff = nowTicks - _window.Ticks;
        while (_timestamps.TryPeek(out var oldest) && oldest < cutoff)
            _timestamps.TryDequeue(out _);
    }
}

/// <summary>Keyed variant — one window counter per key (task ID, resource, etc.)</summary>
public sealed class KeyedSlidingWindowCounter<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, SlidingWindowCounter> _counters = new();
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;

    public KeyedSlidingWindowCounter(TimeSpan window, TimeProvider? timeProvider = null)
    {
        _window = window;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int Record(TKey key) =>
        _counters.GetOrAdd(key, _ => new SlidingWindowCounter(_window, _timeProvider))
            .Record();

    public int GetCount(TKey key) =>
        _counters.TryGetValue(key, out var counter) ? counter.Count : 0;
}
```

### Escalation State Machine

Ties churn detection and sliding windows into Normal → Warning → InterventionRequired → Halted state transitions. Inspired by Polly's circuit breaker.

```csharp
namespace Lopen.Core.BackPressure;

public enum BackPressureState { Normal, Warning, InterventionRequired, Halted }

public record BackPressureTransition(
    BackPressureState From, BackPressureState To,
    string Reason, DateTimeOffset OccurredAt);

public sealed class BackPressureStateMachine
{
    private BackPressureState _state = BackPressureState.Normal;
    private readonly object _lock = new();
    private readonly List<BackPressureTransition> _transitions = [];
    private readonly TimeProvider _timeProvider;
    private readonly int _warningThreshold;
    private readonly int _interventionThreshold;
    private readonly int _haltThreshold;

    public BackPressureState CurrentState => _state;
    public IReadOnlyList<BackPressureTransition> Transitions => _transitions.AsReadOnly();

    public BackPressureStateMachine(
        int warningThreshold = 2, int interventionThreshold = 3,
        int haltThreshold = 5, TimeProvider? timeProvider = null)
    {
        _warningThreshold = warningThreshold;
        _interventionThreshold = interventionThreshold;
        _haltThreshold = haltThreshold;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public BackPressureAction Evaluate(int failureCount, string context)
    {
        lock (_lock)
        {
            var previousState = _state;

            _state = failureCount switch
            {
                _ when failureCount >= _haltThreshold => BackPressureState.Halted,
                _ when failureCount >= _interventionThreshold => BackPressureState.InterventionRequired,
                _ when failureCount >= _warningThreshold => BackPressureState.Warning,
                _ => BackPressureState.Normal
            };

            if (_state != previousState)
                _transitions.Add(new(previousState, _state, context, _timeProvider.GetUtcNow()));

            return _state switch
            {
                BackPressureState.Normal => new BackPressureAction.Continue(),
                BackPressureState.Warning => new BackPressureAction.InjectCorrection(
                    $"Warning: {context}. Consider a different approach."),
                BackPressureState.InterventionRequired => new BackPressureAction.InjectCorrection(
                    $"INTERVENTION: {context}. You must change strategy before proceeding."),
                BackPressureState.Halted => new BackPressureAction.Halt(
                    $"Halted: {context}. Human intervention required."),
                _ => throw new InvalidOperationException()
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            var prev = _state;
            _state = BackPressureState.Normal;
            if (prev != BackPressureState.Normal)
                _transitions.Add(new(prev, BackPressureState.Normal,
                    "Manual reset", _timeProvider.GetUtcNow()));
        }
    }
}

public abstract record BackPressureAction
{
    public sealed record Continue() : BackPressureAction;
    public sealed record InjectCorrection(string Message) : BackPressureAction;
    public sealed record Halt(string Message) : BackPressureAction;
}
```

---

## Category 3: Quality Gates

Quality gates are enforced via the guardrail pipeline (see [Guardrail Pipeline](#guardrail-pipeline-composition)). The `OracleVerificationGuardrail` checks that `verify_task_completion` / `verify_component_completion` / `verify_module_completion` were called before allowing completion claims. See the pipeline section for the concrete implementation.

The `RequiredToolVerifier` enforces that mandatory tools (from Skills & Hooks) were called:

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Immutable;

public sealed class RequiredToolVerifier
{
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _requirements;

    public RequiredToolVerifier(
        ImmutableDictionary<string, ImmutableHashSet<string>>? requirements = null)
    {
        _requirements = requirements ??
            ImmutableDictionary<string, ImmutableHashSet<string>>.Empty
                .Add("task_completion",
                    ["verify_tests", "verify_task_completion"].ToImmutableHashSet())
                .Add("component_completion",
                    ["verify_tests", "verify_lint", "verify_component_completion"].ToImmutableHashSet())
                .Add("module_completion",
                    ["verify_build", "verify_tests", "verify_lint", "verify_module_completion"]
                        .ToImmutableHashSet());
    }

    public VerificationResult Verify(
        string completionScope, ImmutableList<ToolCallRecord> iterationCalls)
    {
        if (!_requirements.TryGetValue(completionScope, out var required))
            return VerificationResult.Pass(completionScope);

        var calledTools = iterationCalls
            .Where(c => c.Outcome == ToolCallOutcome.Success)
            .Select(c => c.ToolName)
            .ToImmutableHashSet();

        var missing = required.Except(calledTools);

        return missing.IsEmpty
            ? VerificationResult.Pass(completionScope)
            : VerificationResult.Fail(completionScope, missing);
    }
}

public sealed record VerificationResult(
    string Scope, bool Passed, ImmutableHashSet<string> MissingTools)
{
    public static VerificationResult Pass(string scope) =>
        new(scope, true, ImmutableHashSet<string>.Empty);

    public static VerificationResult Fail(string scope, ImmutableHashSet<string> missing) =>
        new(scope, false, missing);

    public string? ToRejectionMessage() => Passed ? null :
        $"Cannot mark {Scope} as complete. Required tools not called: " +
        $"[{string.Join(", ", MissingTools)}]. Run these tools first, then retry.";
}
```

---

## Category 4: Tool Call Auditor (Tool Discipline)

### Audit Trail Data Model

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Immutable;

public sealed record ToolCallRecord(
    Guid Id,
    string IterationId,
    string ToolName,
    ImmutableDictionary<string, string> Arguments,
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    ToolCallOutcome Outcome,
    string? ErrorMessage = null);

public enum ToolCallOutcome { Success, Failure, Timeout }
```

### Thread-Safe Append-Only Audit Log

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Immutable;

public sealed class ToolCallAuditLog
{
    private ImmutableList<ToolCallRecord> _records = ImmutableList<ToolCallRecord>.Empty;

    public ImmutableList<ToolCallRecord> Records => _records;

    public void Append(ToolCallRecord record)
    {
        // Lock-free CAS append — safe for concurrent tool calls
        ImmutableList<ToolCallRecord> initial, updated;
        do
        {
            initial = _records;
            updated = initial.Add(record);
        } while (Interlocked.CompareExchange(ref _records, updated, initial) != initial);
    }

    public ImmutableList<ToolCallRecord> GetByIteration(string iterationId) =>
        _records.Where(r => r.IterationId == iterationId).ToImmutableList();

    public ImmutableList<ToolCallRecord> GetByToolName(string toolName) =>
        _records.Where(r => r.ToolName == toolName).ToImmutableList();
}
```

### Auditing Decorator

Wraps any `IToolHandler` transparently. Follows the decorator pattern — adds auditing without modifying tool handlers.

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Immutable;
using System.Diagnostics;

public interface IToolHandler
{
    string ToolName { get; }
    Task<ToolResult> ExecuteAsync(
        ImmutableDictionary<string, string> arguments, CancellationToken ct);
}

public record ToolResult(bool Success, string Output, string? Error = null);

public sealed class AuditingToolHandler(
    IToolHandler inner, ToolCallAuditLog auditLog,
    string iterationId, TimeProvider? clock = null) : IToolHandler
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string ToolName => inner.ToolName;

    public async Task<ToolResult> ExecuteAsync(
        ImmutableDictionary<string, string> arguments, CancellationToken ct)
    {
        var timestamp = _clock.GetUtcNow();
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await inner.ExecuteAsync(arguments, ct);
            sw.Stop();

            auditLog.Append(new ToolCallRecord(
                Guid.NewGuid(), iterationId, inner.ToolName, arguments,
                timestamp, sw.Elapsed,
                result.Success ? ToolCallOutcome.Success : ToolCallOutcome.Failure,
                result.Error));

            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            auditLog.Append(new ToolCallRecord(
                Guid.NewGuid(), iterationId, inner.ToolName, arguments,
                timestamp, sw.Elapsed, ToolCallOutcome.Timeout));
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            auditLog.Append(new ToolCallRecord(
                Guid.NewGuid(), iterationId, inner.ToolName, arguments,
                timestamp, sw.Elapsed, ToolCallOutcome.Failure, ex.Message));
            throw;
        }
    }
}
```

### Pattern Detection — LINQ-Based Analysis

Analyzes the audit trail to detect wasteful patterns per iteration.

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Immutable;

public abstract record DetectedPattern(string Description, PatternSeverity Severity);

public record RepeatedFileRead(string FilePath, int Count)
    : DetectedPattern($"File '{FilePath}' read {Count} times", PatternSeverity.Warning);

public record RepeatedCommandFailure(string Command, int Count)
    : DetectedPattern($"Command '{Command}' failed {Count} times", PatternSeverity.Escalation);

public record ShotgunDebugging(int UniqueFilesEdited, TimeSpan Window)
    : DetectedPattern(
        $"{UniqueFilesEdited} files edited in {Window.TotalMinutes:F0}min without tests",
        PatternSeverity.Warning);

public enum PatternSeverity { Info, Warning, Escalation }

public sealed class PatternDetector
{
    public int MaxSameFileReads { get; init; } = 3;
    public int MaxConsecutiveFailures { get; init; } = 3;
    public int ShotgunFileThreshold { get; init; } = 5;

    public ImmutableList<DetectedPattern> Analyze(ImmutableList<ToolCallRecord> iterationCalls)
    {
        var patterns = ImmutableList.CreateBuilder<DetectedPattern>();

        // Same file read multiple times
        var repeatedReads = iterationCalls
            .Where(c => c.ToolName is "read_file" or "view_file" or "read_spec")
            .Where(c => c.Arguments.ContainsKey("path"))
            .GroupBy(c => c.Arguments["path"])
            .Where(g => g.Count() >= MaxSameFileReads);

        foreach (var group in repeatedReads)
            patterns.Add(new RepeatedFileRead(group.Key, group.Count()));

        // Same command failing repeatedly
        var commandFailures = iterationCalls
            .Where(c => c.ToolName is "bash" or "run_command")
            .Where(c => c.Outcome == ToolCallOutcome.Failure)
            .Where(c => c.Arguments.ContainsKey("command"))
            .GroupBy(c => c.Arguments["command"])
            .Where(g => g.Count() >= MaxConsecutiveFailures);

        foreach (var group in commandFailures)
            patterns.Add(new RepeatedCommandFailure(group.Key, group.Count()));

        // Many edits without running tests
        var editCalls = iterationCalls
            .Where(c => c.ToolName is "write_file" or "edit_file").ToList();
        var testCalls = iterationCalls
            .Where(c => c.ToolName is "verify_tests" or "run_tests").ToList();

        if (editCalls.Count >= ShotgunFileThreshold && testCalls.Count == 0)
        {
            var window = editCalls[^1].Timestamp - editCalls[0].Timestamp;
            patterns.Add(new ShotgunDebugging(editCalls.Count, window));
        }

        return patterns.ToImmutable();
    }
}
```

### Corrective Instruction Generator

Transforms detected patterns into LLM prompt instructions. Implements "injects a corrective instruction into the next prompt" from Core spec.

```csharp
namespace Lopen.Core.BackPressure;

using System.Collections.Immutable;
using System.Text;

public sealed class CorrectiveInstructionGenerator
{
    public string? GenerateCorrection(ImmutableList<DetectedPattern> patterns)
    {
        if (patterns.IsEmpty) return null;

        var builder = new StringBuilder();
        builder.AppendLine("⚠️ TOOL USAGE CORRECTION (from orchestrator):");
        builder.AppendLine();

        foreach (var pattern in patterns)
        {
            var instruction = pattern switch
            {
                RepeatedFileRead r =>
                    $"You have read '{r.FilePath}' {r.Count} times this iteration. " +
                    "Store the content in working memory instead of re-reading.",
                RepeatedCommandFailure r =>
                    $"The command '{r.Command}' has failed {r.Count} times. " +
                    "STOP retrying. Analyze the error, change your approach.",
                ShotgunDebugging r =>
                    $"You edited {r.UniqueFilesEdited} files without running tests. " +
                    "Run `verify_tests` before making more changes.",
                _ => pattern.Description
            };
            builder.AppendLine($"- {instruction}");
        }

        builder.AppendLine();
        builder.AppendLine("These are soft corrections — proceed if you have a " +
                          "legitimate reason, but explain your reasoning.");

        return builder.ToString();
    }
}
```

---

## Guardrail Pipeline (Composition)

### Result Types

```csharp
namespace Lopen.Core.BackPressure;

using System.Text;

public enum GuardrailVerdict { Pass, Warn, Block }

public sealed record GuardrailResult
{
    public required GuardrailVerdict Verdict { get; init; }
    public required string GuardrailName { get; init; }
    public string? Message { get; init; }
    public bool RequiresUserConfirmation { get; init; }

    public static GuardrailResult Passed(string name) =>
        new() { Verdict = GuardrailVerdict.Pass, GuardrailName = name };

    public static GuardrailResult Warning(string name, string message) =>
        new() { Verdict = GuardrailVerdict.Warn, GuardrailName = name, Message = message };

    public static GuardrailResult Blocked(string name, string message,
        bool requiresConfirmation = false) =>
        new() { Verdict = GuardrailVerdict.Block, GuardrailName = name,
                Message = message, RequiresUserConfirmation = requiresConfirmation };
}

public sealed class GuardrailPipelineResult
{
    private readonly List<GuardrailResult> _results = [];

    public IReadOnlyList<GuardrailResult> Results => _results;
    public bool IsBlocked => _results.Any(r => r.Verdict == GuardrailVerdict.Block);
    public bool HasWarnings => _results.Any(r => r.Verdict == GuardrailVerdict.Warn);
    public bool RequiresUserConfirmation => _results.Any(r => r.RequiresUserConfirmation);

    public IEnumerable<GuardrailResult> Blocks =>
        _results.Where(r => r.Verdict == GuardrailVerdict.Block);
    public IEnumerable<GuardrailResult> Warnings =>
        _results.Where(r => r.Verdict == GuardrailVerdict.Warn);

    internal void Add(GuardrailResult result) => _results.Add(result);

    /// <summary>Builds corrective instructions for the next LLM prompt.</summary>
    public string BuildCorrectiveInstructions()
    {
        var sb = new StringBuilder();
        foreach (var warning in Warnings)
            sb.AppendLine($"⚠️ {warning.GuardrailName}: {warning.Message}");
        foreach (var block in Blocks)
            sb.AppendLine($"🛑 {block.GuardrailName}: {block.Message}");
        return sb.ToString();
    }
}
```

### Guardrail Interface

```csharp
namespace Lopen.Core.BackPressure;

public enum BackPressureCategory
{
    ResourceLimits,      // Category 1
    ProgressIntegrity,   // Category 2
    QualityGates,        // Category 3
    ToolDiscipline       // Category 4
}

/// <summary>
/// A single guardrail check. Ordered by the Order property.
/// Short-circuit semantics: if ShortCircuitOnBlock is true, a Block
/// verdict stops the pipeline immediately (budget exhaustion). If false,
/// the pipeline continues collecting results (tool discipline warnings).
/// </summary>
public interface IGuardrail
{
    int Order { get; }
    bool ShortCircuitOnBlock { get; }
    BackPressureCategory Category { get; }
    Task<GuardrailResult> EvaluateAsync(IterationContext context, CancellationToken ct = default);
}
```

### Pipeline Executor

```csharp
namespace Lopen.Core.BackPressure;

using Microsoft.Extensions.Logging;

public interface IGuardrailPipeline
{
    Task<GuardrailPipelineResult> EvaluateAsync(
        IterationContext context, CancellationToken ct = default);
}

/// <summary>
/// Executes guardrails in Order, aggregating results. Hard-limit guardrails
/// (ShortCircuitOnBlock=true) stop the pipeline on Block; soft-limit
/// guardrails accumulate warnings. Guardrail exceptions are caught and
/// converted to warnings (fail-open).
/// </summary>
public sealed class GuardrailPipeline(
    IEnumerable<IGuardrail> guardrails, ILogger<GuardrailPipeline> logger)
    : IGuardrailPipeline
{
    private readonly IReadOnlyList<IGuardrail> _guardrails =
        guardrails.OrderBy(g => g.Order).ToList();

    public async Task<GuardrailPipelineResult> EvaluateAsync(
        IterationContext context, CancellationToken ct = default)
    {
        var result = new GuardrailPipelineResult();

        foreach (var guardrail in _guardrails)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var verdict = await guardrail.EvaluateAsync(context, ct);
                result.Add(verdict);

                logger.LogDebug("Guardrail {Name} returned {Verdict}: {Message}",
                    verdict.GuardrailName, verdict.Verdict, verdict.Message);

                if (verdict.Verdict == GuardrailVerdict.Block && guardrail.ShortCircuitOnBlock)
                {
                    logger.LogWarning("Pipeline short-circuited by {Name}: {Message}",
                        verdict.GuardrailName, verdict.Message);
                    break;
                }
            }
            catch (Exception ex)
            {
                // Fail-open: guardrail errors become warnings
                logger.LogError(ex, "Guardrail {Type} threw", guardrail.GetType().Name);
                result.Add(GuardrailResult.Warning(
                    guardrail.GetType().Name,
                    $"Guardrail evaluation failed: {ex.Message}"));
            }
        }

        return result;
    }
}
```

### Concrete Guardrail Implementations

```csharp
namespace Lopen.Core.BackPressure;

using Microsoft.Extensions.Options;

// ── Category 1: Resource Limits (Order=100, ShortCircuit=true) ──

public sealed class BudgetGuardrail(BudgetTracker budgetTracker) : IGuardrail
{
    public int Order => 100;
    public bool ShortCircuitOnBlock => true;
    public BackPressureCategory Category => BackPressureCategory.ResourceLimits;

    public Task<GuardrailResult> EvaluateAsync(IterationContext context, CancellationToken ct)
    {
        var snapshot = budgetTracker.GetSnapshot();
        var ratio = snapshot.TokenFraction;

        var result = ratio switch
        {
            >= 0.9 => GuardrailResult.Blocked("BudgetGuardrail",
                $"Module '{context.ModuleName}' at {ratio:P0} of token budget " +
                $"({snapshot.FormatTokenUsage()})",
                requiresConfirmation: true),
            >= 0.8 => GuardrailResult.Warning("BudgetGuardrail",
                $"Module '{context.ModuleName}' at {ratio:P0} of token budget " +
                $"({snapshot.FormatTokenUsage()})"),
            _ => GuardrailResult.Passed("BudgetGuardrail")
        };

        return Task.FromResult(result);
    }
}

// ── Category 2: Churn Detection (Order=200, ShortCircuit=true) ──

public sealed class ChurnDetectionGuardrail(
    ChurnDetector churnDetector, IOptions<BackPressureOptions> options) : IGuardrail
{
    public int Order => 200;
    public bool ShortCircuitOnBlock => true;
    public BackPressureCategory Category => BackPressureCategory.ProgressIntegrity;

    public Task<GuardrailResult> EvaluateAsync(IterationContext context, CancellationToken ct)
    {
        if (context.TaskId is null)
            return Task.FromResult(GuardrailResult.Passed("ChurnDetection"));

        var history = churnDetector.GetHistory(context.TaskId);
        var count = history.Count;
        var threshold = options.Value.ChurnThreshold;

        var result = count >= threshold
            ? GuardrailResult.Blocked("ChurnDetection",
                $"Task '{context.TaskId}' failed {count} consecutive times " +
                $"(threshold: {threshold}). Consider manual intervention.",
                requiresConfirmation: true)
            : GuardrailResult.Passed("ChurnDetection");

        return Task.FromResult(result);
    }
}

// ── Category 2: Circular Behavior (Order=210, ShortCircuit=false) ──

public sealed class CircularBehaviorGuardrail(
    CircularBehaviorDetector detector) : IGuardrail
{
    public int Order => 210;
    public bool ShortCircuitOnBlock => false;
    public BackPressureCategory Category => BackPressureCategory.ProgressIntegrity;

    public Task<GuardrailResult> EvaluateAsync(IterationContext context, CancellationToken ct)
    {
        // Analysis is done reactively via the detector during tool calls.
        // The guardrail checks the accumulated state.
        return Task.FromResult(GuardrailResult.Passed("CircularBehavior"));
    }
}

// ── Category 3: Oracle Verification (Order=300, ShortCircuit=true) ──

public sealed class OracleVerificationGuardrail(
    ToolCallAuditLog auditLog, RequiredToolVerifier verifier) : IGuardrail
{
    public int Order => 300;
    public bool ShortCircuitOnBlock => true;
    public BackPressureCategory Category => BackPressureCategory.QualityGates;

    public Task<GuardrailResult> EvaluateAsync(IterationContext context, CancellationToken ct)
    {
        if (context.TaskId is null || !context.IsCompletionClaim)
            return Task.FromResult(GuardrailResult.Passed("OracleVerification"));

        var iterationCalls = auditLog.GetByIteration(context.IterationId);
        var verification = verifier.Verify("task_completion", iterationCalls);

        var result = verification.Passed
            ? GuardrailResult.Passed("OracleVerification")
            : GuardrailResult.Blocked("OracleVerification",
                verification.ToRejectionMessage() ?? "Verification failed");

        return Task.FromResult(result);
    }
}

// ── Category 4: Tool Discipline (Order=400, ShortCircuit=false) ──

public sealed class ToolDisciplineGuardrail(
    ToolCallAuditLog auditLog, PatternDetector detector,
    CorrectiveInstructionGenerator correctionGenerator) : IGuardrail
{
    public int Order => 400;
    public bool ShortCircuitOnBlock => false; // Per spec: "No hard blocks"
    public BackPressureCategory Category => BackPressureCategory.ToolDiscipline;

    public Task<GuardrailResult> EvaluateAsync(IterationContext context, CancellationToken ct)
    {
        var iterationCalls = auditLog.GetByIteration(context.IterationId);
        var patterns = detector.Analyze(iterationCalls);

        if (patterns.IsEmpty)
            return Task.FromResult(GuardrailResult.Passed("ToolDiscipline"));

        var correction = correctionGenerator.GenerateCorrection(patterns);
        return Task.FromResult(GuardrailResult.Warning("ToolDiscipline",
            correction ?? "Wasteful tool patterns detected."));
    }
}
```

### Iteration Context

```csharp
namespace Lopen.Core.BackPressure;

public sealed class IterationContext
{
    public required string ModuleName { get; init; }
    public required string IterationId { get; init; }
    public string? ComponentName { get; init; }
    public string? TaskId { get; init; }
    public required int IterationNumber { get; init; }
    public bool IsCompletionClaim { get; init; }
}
```

---

## DI Registration & Integration

### Service Registration

```csharp
namespace Lopen.Core.BackPressure;

using Microsoft.Extensions.DependencyInjection;

public sealed class BackPressureOptions
{
    public double BudgetWarningThreshold { get; set; } = 0.8;
    public double BudgetConfirmationThreshold { get; set; } = 0.9;
    public int ChurnThreshold { get; set; } = 3;
    public int MaxFileReadsPerIteration { get; set; } = 3;
    public int MaxRepeatedCommands { get; set; } = 3;
}

public static class GuardrailServiceCollectionExtensions
{
    public static IServiceCollection AddGuardrailPipeline(
        this IServiceCollection services,
        Action<BackPressureOptions>? configure = null)
    {
        services.AddOptions<BackPressureOptions>()
            .BindConfiguration("BackPressure");

        if (configure is not null)
            services.Configure(configure);

        // Core services
        services.AddSingleton<ToolCallAuditLog>();
        services.AddSingleton<PatternDetector>();
        services.AddSingleton<CorrectiveInstructionGenerator>();
        services.AddSingleton<RequiredToolVerifier>();
        services.AddSingleton<ChurnDetector>();
        services.AddSingleton<CircularBehaviorDetector>();

        // Pipeline
        services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();

        // Guardrails (order determined by Order property, not registration)
        services.AddSingleton<IGuardrail, BudgetGuardrail>();
        services.AddSingleton<IGuardrail, ChurnDetectionGuardrail>();
        services.AddSingleton<IGuardrail, CircularBehaviorGuardrail>();
        services.AddSingleton<IGuardrail, OracleVerificationGuardrail>();
        services.AddSingleton<IGuardrail, ToolDisciplineGuardrail>();

        return services;
    }
}
```

### Orchestration Loop Integration

```csharp
namespace Lopen.Core;

using Lopen.Core.BackPressure;
using Microsoft.Extensions.Logging;

public sealed class OrchestrationLoop(
    IGuardrailPipeline guardrails,
    BudgetTracker budget,
    ILogger<OrchestrationLoop> logger)
{
    public async Task RunIterationAsync(IterationContext ctx, CancellationToken ct)
    {
        // 1. Pre-iteration guardrail evaluation
        var pipelineResult = await guardrails.EvaluateAsync(ctx, ct);

        // 2. Surface warnings
        foreach (var warning in pipelineResult.Warnings)
            logger.LogWarning("⚠️ {Name}: {Message}", warning.GuardrailName, warning.Message);

        // 3. Handle blocks
        if (pipelineResult.IsBlocked)
        {
            foreach (var block in pipelineResult.Blocks)
                logger.LogError("🛑 {Name}: {Message}", block.GuardrailName, block.Message);

            if (pipelineResult.RequiresUserConfirmation)
            {
                // Pause for user confirmation before next iteration
                // (handled by the caller / TUI layer)
                return;
            }
        }

        // 4. Inject corrective instructions into prompt
        var corrections = pipelineResult.BuildCorrectiveInstructions();
        // corrections are appended to the system prompt for the SDK invocation

        // 5. Invoke SDK, record usage
        // var response = await sdk.InvokeAsync(prompt + corrections, ct);
        // budget.RecordTokenUsage(response.TokensUsed);
    }
}
```

---

## Design Decisions

| Decision | Rationale |
|---|---|
| **Events over IObservable** for budget notifications | Small subscriber count (TUI + telemetry); Rx is overkill |
| **Discriminated unions via abstract records** for results | Forces callers to handle all cases; pattern matching in C# |
| **`Interlocked` over `lock`** for counters | Lower contention for simple atomic operations |
| **`TimeProvider`** (.NET 8) for all time-dependent code | Enables deterministic testing with `FakeTimeProvider` |
| **`ImmutableList`/`ImmutableDictionary`** for audit trail | Append-only by nature; thread-safe; structural sharing |
| **CAS loop** for audit log append | Lock-free concurrent appends; matches immutable data ethos |
| **`IGuardrail.Order`** property for ordering | Decouples evaluation order from DI registration order |
| **`ShortCircuitOnBlock`** per guardrail | Budget = hard stop; Tool discipline = soft warning |
| **Fail-open for guardrail errors** | Guardrail infrastructure must never crash the orchestration loop |
| **LINQ for pattern analysis** | Declarative queries over immutable data; pure functions, easy to test |

---

## References

- [Core § Back-Pressure](SPECIFICATION.md#back-pressure) — The four back-pressure categories this implements
- [Configuration § Budget Settings](../configuration/SPECIFICATION.md#budget-settings) — Configurable thresholds
- [Configuration § Tool Discipline Settings](../configuration/SPECIFICATION.md#tool-discipline-settings) — Tool discipline thresholds
- [LLM § Oracle Verification Tools](../llm/SPECIFICATION.md#oracle-verification-tools) — Oracle dispatch mechanism
- [Polly Circuit Breaker](https://github.com/App-vNext/Polly) — State machine inspiration (Open/HalfOpen/Closed)
- [`System.Threading.RateLimiting`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting) — .NET rate limiting patterns
- [`TimeProvider`](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider) — .NET 8 time abstraction for testability
- [MediatR Pipeline Behaviors](https://github.com/jbogard/MediatR) — Composable pre/post processing pattern
- [`Microsoft.Extensions.Diagnostics.ResourceMonitoring`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.diagnostics.resourcemonitoring) — Snapshot/polling pattern reference
