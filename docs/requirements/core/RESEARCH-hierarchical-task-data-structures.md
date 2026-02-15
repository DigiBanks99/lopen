# Research: Hierarchical Task Data Structures in C#

> Data structure patterns for implementing the Module → Component → Task → Subtask hierarchy defined in the [Core Specification](SPECIFICATION.md#task-hierarchy).

---

## Table of Contents

- [1. Composite Pattern for the Hierarchy](#1-composite-pattern-for-the-hierarchy)
- [2. State Tracking with Enum + Transition Validation](#2-state-tracking-with-enum--transition-validation)
- [3. Aggregate State Computation](#3-aggregate-state-computation)
- [4. JSON Serialization with System.Text.Json](#4-json-serialization-with-systemtextjson)
- [5. Immutable vs Mutable State Patterns](#5-immutable-vs-mutable-state-patterns)
- [6. Visitor and Query Patterns](#6-visitor-and-query-patterns)
- [7. Recommendation](#7-recommendation)

---

## 1. Composite Pattern for the Hierarchy

### Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Base type | `abstract class WorkNode<TChild>` | Shares add/remove/children logic; generic param enforces valid nesting |
| Leaf node | Implements `IWorkNode` directly | No phantom `TChild` needed; `Children` returns `[]` |
| Parent setter | `internal interface IChildNode` | Keeps `Parent` publicly read-only while letting `Add/Remove` maintain it |
| `required init` | On `Id`, `Name` | Forces callers to supply identity at construction; immutable after |
| Children exposure | `IReadOnlyList<T>` | Prevents external mutation; backed by `List<T>` internally |
| Records | Only for snapshot DTOs | Tree nodes have identity semantics; records for value-semantic projections |

### Why Typed Hierarchy Over Homogeneous Tree

A generic `WorkNode<TChild>` gives a **typed hierarchy** (Module → Component → TaskItem → Subtask) enforced at compile time, while an `IWorkNode` interface provides a **homogeneous view** for traversal algorithms that don't care about level. This prevents invalid nesting (e.g., a Subtask containing a Module) without runtime checks.

### Code: Hierarchy Data Model

```csharp
// ─── Interface for homogeneous traversal ───
public interface IWorkNode
{
    Guid Id { get; }
    string Name { get; }
    WorkNodeState State { get; }
    IWorkNode? Parent { get; }
    IReadOnlyList<IWorkNode> Children { get; }
    int Depth { get; }
}

// Internal interface for parent-setting without exposing public setter
internal interface IChildNode
{
    void SetParent(IWorkNode? parent);
}

// ─── Abstract base with typed children ───
public abstract class WorkNode<TChild> : IWorkNode
    where TChild : IWorkNode
{
    private readonly List<TChild> _children = [];

    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public WorkNodeState State { get; private set; } = WorkNodeState.Pending;
    public IWorkNode? Parent { get; internal set; }

    public IReadOnlyList<TChild> Children => _children.AsReadOnly();
    IReadOnlyList<IWorkNode> IWorkNode.Children =>
        _children.Cast<IWorkNode>().ToList().AsReadOnly();

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    public TChild Add(TChild child)
    {
        if (child is IChildNode c)
            c.SetParent(this);
        _children.Add(child);
        return child;
    }

    public bool Remove(TChild child)
    {
        if (!_children.Remove(child)) return false;
        if (child is IChildNode c) c.SetParent(null);
        return true;
    }

    public void TransitionTo(WorkNodeState newState)
    {
        State = State.TransitionTo(newState);
    }

    // Bypass validation when restoring from persisted state
    internal void RestoreState(WorkNodeState state) => State = state;
}

// ─── Concrete types ───

public sealed class Module : WorkNode<Component>
{
    public string? Description { get; init; }
}

public sealed class Component : WorkNode<TaskItem>, IChildNode
{
    public string? Area { get; init; }
    void IChildNode.SetParent(IWorkNode? parent) => Parent = parent;
}

public sealed class TaskItem : WorkNode<Subtask>, IChildNode
{
    public int Priority { get; init; }
    void IChildNode.SetParent(IWorkNode? parent) => Parent = parent;
}

// Leaf node — no children
public sealed class Subtask : IWorkNode, IChildNode
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public WorkNodeState State { get; private set; } = WorkNodeState.Pending;
    public IWorkNode? Parent { get; private set; }
    public IReadOnlyList<IWorkNode> Children => [];
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;
    public TimeSpan? Estimate { get; init; }

    public void TransitionTo(WorkNodeState newState)
    {
        State = State.TransitionTo(newState);
    }

    internal void RestoreState(WorkNodeState state) => State = state;
    void IChildNode.SetParent(IWorkNode? parent) => Parent = parent;
}
```

### Construction Example

```csharp
var module = new Module { Id = Guid.NewGuid(), Name = "Authentication" };

var comp = module.Add(new Component
{
    Id = Guid.NewGuid(), Name = "JWT Validation", Area = "Security"
});

var task = comp.Add(new TaskItem
{
    Id = Guid.NewGuid(), Name = "Implement token refresh", Priority = 1
});

task.Add(new Subtask
{
    Id = Guid.NewGuid(), Name = "Parse token from header",
    Estimate = TimeSpan.FromHours(2)
});

// Parent back-references are maintained automatically
Debug.Assert(task.Children[0].Parent == task);
Debug.Assert(task.Children[0].Depth == 3);
```

---

## 2. State Tracking with Enum + Transition Validation

### State Enum

```csharp
public enum WorkNodeState
{
    Pending,
    InProgress,
    Complete,
    Failed
}
```

### Transition Validation

Valid transitions match the spec: `Pending → InProgress → Complete/Failed`, plus retry `Failed → InProgress`.

```csharp
public static class WorkNodeStateExtensions
{
    public static bool CanTransitionTo(this WorkNodeState current, WorkNodeState target) =>
        (current, target) switch
        {
            (WorkNodeState.Pending,    WorkNodeState.InProgress) => true,
            (WorkNodeState.InProgress, WorkNodeState.Complete)   => true,
            (WorkNodeState.InProgress, WorkNodeState.Failed)     => true,
            (WorkNodeState.Failed,     WorkNodeState.InProgress) => true,
            _ => false
        };

    public static WorkNodeState TransitionTo(this WorkNodeState current, WorkNodeState target) =>
        current.CanTransitionTo(target)
            ? target
            : throw new InvalidOperationException(
                $"Invalid state transition: {current} → {target}");

    public static string ToIcon(this WorkNodeState state) => state switch
    {
        WorkNodeState.Pending    => "○",
        WorkNodeState.InProgress => "▶",
        WorkNodeState.Complete   => "✓",
        WorkNodeState.Failed     => "✗",
        _ => throw new UnreachableException()
    };
}
```

**Design notes:**

- **Tuple switch expression** `(current, target)` is the idiomatic .NET 8 transition table — compact, exhaustive, no class-per-state overhead.
- **Extension methods** on the enum keep the enum clean while making usage ergonomic: `state.TransitionTo(WorkNodeState.Complete)`.
- **`TransitionTo` returns a new value** — it doesn't mutate anything. The caller (`WorkNode.TransitionTo`) assigns the result.
- **`UnreachableException`** (.NET 7+) signals exhaustive matching to both compiler and readers.

---

## 3. Aggregate State Computation

The parent's state is derived from its children. Rules from the spec:

| Children | Parent State |
|---|---|
| All Pending | Pending |
| Any InProgress, or mix of Complete + Pending | InProgress |
| All Complete | Complete |
| Any Failed | Failed (highest priority) |

```csharp
public static class AggregateStateComputation
{
    public static WorkNodeState ComputeAggregateState(
        IReadOnlyList<WorkNodeState> childStates)
    {
        if (childStates is { Count: 0 })
            return WorkNodeState.Pending;

        // Failed takes priority — one failure poisons the parent
        if (childStates.Any(s => s is WorkNodeState.Failed))
            return WorkNodeState.Failed;

        if (childStates.All(s => s is WorkNodeState.Complete))
            return WorkNodeState.Complete;

        if (childStates.All(s => s is WorkNodeState.Pending))
            return WorkNodeState.Pending;

        // Any InProgress, or mix of Complete + Pending
        return WorkNodeState.InProgress;
    }
}
```

### Integration with IWorkNode

```csharp
public static class WorkNodeAggregateExtensions
{
    public static WorkNodeState ComputeAggregateState(this IWorkNode node)
    {
        if (node.Children.Count == 0)
            return node.State;

        var childStates = node.Children
            .Select(c => c.ComputeAggregateState())
            .ToList();

        return AggregateStateComputation.ComputeAggregateState(childStates);
    }
}
```

This recursively computes aggregate state bottom-up: leaf nodes return their own state, parent nodes aggregate children. The recursion is safe for a 4-level tree.

---

## 4. JSON Serialization with System.Text.Json

### Polymorphic Serialization

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ModuleDto),    "module")]
[JsonDerivedType(typeof(ComponentDto), "component")]
[JsonDerivedType(typeof(TaskItemDto),  "task")]
[JsonDerivedType(typeof(SubtaskDto),   "subtask")]
public abstract record WorkNodeDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public WorkNodeState State { get; init; } = WorkNodeState.Pending;
    public IReadOnlyList<WorkNodeDto> Children { get; init; } = [];
}

public record ModuleDto : WorkNodeDto
{
    public string? Description { get; init; }
}

public record ComponentDto : WorkNodeDto
{
    public string? Area { get; init; }
}

public record TaskItemDto : WorkNodeDto
{
    public int Priority { get; init; }
}

public record SubtaskDto : WorkNodeDto
{
    public TimeSpan? Estimate { get; init; }
}
```

### Source Generator Context (AOT-safe)

```csharp
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(WorkNodeDto))]
[JsonSerializable(typeof(ModuleDto))]
[JsonSerializable(typeof(ComponentDto))]
[JsonSerializable(typeof(TaskItemDto))]
[JsonSerializable(typeof(SubtaskDto))]
public partial class WorkNodeJsonContext : JsonSerializerContext;
```

### Handling Parent References

Parent back-references create circular references. Two approaches:

1. **`[JsonIgnore]` on `Parent`** (recommended) — skip during serialization, reconstruct after deserialization with `WireParents()`.
2. **`ReferenceHandler.IgnoreCycles`** — writes `null` for cycles. Less predictable.

```csharp
public static class WorkNodeDtoExtensions
{
    // Reconstruct parent refs after deserialization — not needed for DTOs
    // but useful if deserializing back to the live model
    public static T WireParents<T>(this T node) where T : IWorkNode
    {
        foreach (var child in node.Children)
        {
            if (child is IChildNode c) c.SetParent(node);
            if (child is IWorkNode wn) wn.WireParents();
        }
        return node;
    }
}
```

### Serialize / Deserialize Round-Trip

```csharp
public static class SessionPersistence
{
    public static string Serialize(WorkNodeDto node) =>
        JsonSerializer.Serialize(node, WorkNodeJsonContext.Default.WorkNodeDto);

    public static WorkNodeDto? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, WorkNodeJsonContext.Default.WorkNodeDto);
}
```

### Example JSON Output

```json
{
  "$type": "module",
  "description": "Authentication & authorization",
  "id": "a1b2c3d4-...",
  "name": "Auth Module",
  "state": "pending",
  "children": [
    {
      "$type": "component",
      "area": "Security",
      "id": "e5f6a7b8-...",
      "name": "JWT Validation",
      "state": "inProgress",
      "children": [
        {
          "$type": "task",
          "priority": 1,
          "id": "c9d0e1f2-...",
          "name": "Implement token refresh",
          "state": "inProgress",
          "children": [
            {
              "$type": "subtask",
              "estimate": "02:00:00",
              "id": "a3b4c5d6-...",
              "name": "Parse token from header",
              "state": "complete",
              "children": []
            }
          ]
        }
      ]
    }
  ]
}
```

**Key serialization decisions:**

| Concern | Approach |
|---|---|
| Polymorphism | `[JsonPolymorphic]` + `[JsonDerivedType]` — emits `$type` discriminator |
| Circular refs | `[JsonIgnore]` on `Parent` + post-deser `WireParents()` |
| Enums as strings | `UseStringEnumConverter = true` in source-gen options |
| AOT | `[JsonSerializable]` — register every concrete type |
| `IReadOnlyList` | Works natively — STJ deserializes into `List<T>` which implements `IReadOnlyList<T>` |
| Records + `init` | Fully supported in .NET 8+; STJ uses the primary constructor |

---

## 5. Immutable vs Mutable State Patterns

### Analysis

| Pattern | Pros | Cons | Verdict |
|---|---|---|---|
| **Full immutability** (records + `with`) | Audit trail, no accidental mutation | Path copying is unreadable for 4-level trees; every state change rebuilds ancestor chain | ❌ Impractical |
| **Mutable classes + validated transitions** | Direct mutation, simple, serializes cleanly | Must enforce invariants manually | ✅ Recommended |
| **Hybrid** (mutable live model + record DTOs) | Clean separation | Mapping boilerplate for no benefit when class serializes fine | ❌ Unnecessary |
| **Event sourcing lite** (store transitions) | Full audit trail, replay/debug | Adds complexity; Lopen already uses Git for rollback | ❌ Overkill for v1 |

### Why Full Immutability Fails Here

Updating a deeply nested node with immutable records requires path copying:

```csharp
// Updating a subtask's state in an immutable tree — 4 levels of `with`
var updated = root with {
    Children = root.Children.SetItem(moduleIdx,
        root.Children[moduleIdx] with {
            Children = root.Children[moduleIdx].Children.SetItem(compIdx,
                root.Children[moduleIdx].Children[compIdx] with {
                    Children = root.Children[moduleIdx].Children[compIdx]
                        .Children.SetItem(taskIdx,
                            targetTask with { State = TaskState.Complete })})})
};
```

This is unreadable, error-prone, and offers no benefit since Lopen doesn't need previous tree snapshots (it uses Git for rollback).

### Recommended Pattern: Mutable Classes with Guards

```csharp
public class WorkNode
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public WorkNodeState State { get; private set; } = WorkNodeState.Pending;
    public List<WorkNode> Children { get; init; } = [];

    // Controlled mutation with validation
    public void TransitionTo(WorkNodeState newState)
    {
        State = State.TransitionTo(newState); // throws on invalid transition
    }

    // Bypass validation when restoring persisted state
    internal void RestoreState(WorkNodeState state) => State = state;
}
```

**Why this fits Lopen:**

- `private set` + `TransitionTo()` — impossible to set invalid states from outside.
- `RestoreState()` is `internal` — trusted during deserialization, but bypasses validation (trust the file, validate at runtime).
- `System.Text.Json` serializes cleanly with `[JsonInclude]` on the `State` property.
- The tree is small (~100 nodes max). O(n) traversal via `FindById()` is fine.

### Lifecycle Mapping

| Phase | Pattern | Rationale |
|---|---|---|
| Planning (build tree) | `Children.Add()` | Tree assembled incrementally |
| Building (mutate states) | `TransitionTo()` with guards | State machine enforcement, oracle gates |
| Persist (serialize) | `System.Text.Json` source gen on class directly | No DTO mapping needed |
| Resume (deserialize) | `RestoreState()` bypassing validation | Trust persisted state, then re-assess |

---

## 6. Visitor and Query Patterns

### Core: `Descendants()` Extension Method

This is the foundation. It turns a tree into a flat `IEnumerable`, unlocking LINQ.

```csharp
public static class WorkNodeQueryExtensions
{
    // Recursive — clear, fine for shallow trees (≤ 5 levels)
    public static IEnumerable<IWorkNode> DescendantsAndSelf(this IWorkNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    // Iterative with explicit stack — O(n) without nested iterator overhead
    public static IEnumerable<IWorkNode> DescendantsIterative(this IWorkNode node)
    {
        var stack = new Stack<IWorkNode>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var i = current.Children.Count - 1; i >= 0; i--)
                stack.Push(current.Children[i]);
        }
    }

    // Typed descendant filtering
    public static IEnumerable<T> Descendants<T>(this IWorkNode node) where T : IWorkNode
        => node.DescendantsAndSelf().OfType<T>();
}
```

**Recursive vs Iterative:** The recursive version creates O(depth) iterator objects per node, giving O(n·d) total overhead. The iterative version is O(n). For a 4-level tree with ~100 nodes, the recursive version is perfectly fine.

### Finding "Next Pending Task"

```csharp
public static class PendingTaskFinder
{
    public static IWorkNode? FindNextPending(this IWorkNode root) =>
        root.DescendantsAndSelf().FirstOrDefault(node => node switch
        {
            Subtask { State: WorkNodeState.Pending } => true,
            TaskItem { State: WorkNodeState.Pending, Children.Count: 0 } => true,
            _ => false
        });
}
```

Lazy evaluation means it stops at the first match. Pattern matching on type + property destructuring is the idiomatic .NET 8 approach.

### LINQ Queries on Descendants

```csharp
// All pending subtasks
var pendingSubtasks = root.Descendants<Subtask>()
    .Where(s => s.State is WorkNodeState.Pending);

// Completion percentage
var allLeaves = root.DescendantsAndSelf()
    .Where(n => n.Children.Count == 0)
    .ToList();
var completionPct = allLeaves.Count == 0 ? 100.0
    : 100.0 * allLeaves.Count(n => n.State is WorkNodeState.Complete) / allLeaves.Count;

// Group by state
var byState = root.DescendantsAndSelf()
    .GroupBy(n => n.State)
    .Select(g => new { State = g.Key, Count = g.Count() });
```

### Visitor Pattern (for Multiple Distinct Operations)

Use when you need different operations over the same tree without modifying node types:

```csharp
public interface IWorkNodeVisitor<out TResult>
{
    TResult Visit(Module module);
    TResult Visit(Component component);
    TResult Visit(TaskItem task);
    TResult Visit(Subtask subtask);
}
```

### When to Use Which

| Need | Approach |
|---|---|
| Ad-hoc queries (find, filter, count) | `Descendants()` + LINQ |
| Multiple distinct operations over same tree | Visitor pattern |
| Simple full traversal with per-node action | `foreach` over `Descendants()` + pattern matching |
| Need ancestor path during traversal | Visitor with `Stack<string>` path tracking |

**Default to LINQ + Descendants().** Reserve the visitor pattern for when you have 3+ distinct operations that would otherwise require 3+ separate traversal methods with duplicated logic.

---

## 7. Recommendation

For Lopen's task hierarchy implementation:

1. **Use mutable classes** with `WorkNode<TChild>` generic base for the live model. Tree nodes have identity semantics — records are wrong here.
2. **Use `private set` + `TransitionTo()`** for state mutations with validation. `internal RestoreState()` for deserialization.
3. **Compute aggregate state recursively** bottom-up via extension method. Failed takes priority.
4. **Serialize with `System.Text.Json` source generators** using record DTOs with `[JsonPolymorphic]` / `[JsonDerivedType]`. `[JsonIgnore]` on parent references, `WireParents()` after deserialization.
5. **Use `Descendants()` + LINQ** as the default query pattern. Add a visitor only if 3+ distinct tree operations emerge.
6. **Skip immutable patterns** — path copying for 4-level trees is unreadable and Lopen uses Git for rollback, not tree snapshots.

---

## References

- [Core Specification — Task Hierarchy](SPECIFICATION.md#task-hierarchy)
- [Core Specification — Task States](SPECIFICATION.md#task-states)
- [Storage Specification](../storage/SPECIFICATION.md) — Session persistence format
- [System.Text.Json Polymorphism](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism) — .NET 8 polymorphic serialization docs
- [System.Text.Json Source Generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) — AOT-compatible serialization
