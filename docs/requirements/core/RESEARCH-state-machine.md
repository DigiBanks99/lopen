# Research: Re-entrant State Machine Patterns for Workflow Orchestrator

## Context

Lopen's core workflow is a structured 7-step process across 3 phases. The orchestrator must be **re-entrant** — it assesses actual codebase state each iteration rather than trusting stale session data — and must support "entering at the correct step" based on assessment. This research evaluates three C# patterns for implementing this state machine targeting .NET 8+.

### Workflow Summary

```
Phase 1 — Requirement Gathering
  Step 1: Draft Specification

Phase 2 — Planning
  Step 2: Determine Dependencies
  Step 3: Identify Components
  Step 4: Select Next Component
  Step 5: Break Into Tasks

Phase 3 — Building
  Step 6: Iterate Through Tasks
  Step 7: Repeat (→ back to Step 4)
```

### Key Constraints

- **Re-entrant**: Each iteration assesses actual state; no blind trust of prior session data
- **Human gate**: Requirement Gathering → Planning requires user confirmation
- **Automated gates**: Planning → Building and Building → Complete are condition-based
- **Dynamic entry**: Must enter at any step based on assessment result
- **Step 7 loops back**: Building cycles Step 4–7 until all components are complete
- **Back-pressure**: External guardrails (churn detection, budget limits) can interrupt the machine

---

## Pattern 1: Stateless Library (Statecharts)

### Description

[Stateless](https://github.com/dotnet-state-machine/stateless) by Nicholas Blumhardt is a mature, lightweight library for building state machines in .NET. It models states and triggers with a fluent configuration API, supports guard clauses, hierarchical states, async actions, reentrant states, parameterized triggers, and external state storage. Zero dependencies.

**NuGet**: `Stateless` v5.20.1 — targets .NET Standard 2.0, .NET 8, 9, 10. ~15M total downloads. Used by GitHub (VisualStudio extension), Microsoft (ailab), Azure IoT Edge.

### Code Example

```csharp
using Stateless;

public enum WorkflowStep
{
    DraftSpecification,       // Phase 1
    DetermineDependencies,    // Phase 2
    IdentifyComponents,
    SelectNextComponent,
    BreakIntoTasks,
    IterateThroughTasks,      // Phase 3
    Repeat,
    Complete
}

public enum WorkflowTrigger
{
    SpecApproved,             // Human-gated
    DependenciesResolved,
    ComponentsIdentified,
    ComponentSelected,
    TasksBroken,
    TasksCompleted,
    MoreComponents,
    AllComponentsDone,
    Assess                    // Re-entrant: triggers assessment
}

public class WorkflowOrchestrator
{
    private readonly StateMachine<WorkflowStep, WorkflowTrigger> _machine;
    private readonly IStateAssessor _assessor;

    public WorkflowOrchestrator(IStateAssessor assessor)
    {
        _assessor = assessor;

        // External state storage enables re-entrant assessment.
        // The accessor reads assessed state; the mutator persists transitions.
        _machine = new StateMachine<WorkflowStep, WorkflowTrigger>(
            () => _assessor.GetCurrentStep(),
            step => _assessor.PersistStep(step));

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // Phase 1 — Requirement Gathering
        _machine.Configure(WorkflowStep.DraftSpecification)
            .OnEntryAsync(async () => await _assessor.ExecuteDraftSpec())
            .Permit(WorkflowTrigger.SpecApproved, WorkflowStep.DetermineDependencies)
            .PermitReentryIf(WorkflowTrigger.Assess, () => !_assessor.IsSpecReady());

        // Phase 2 — Planning
        _machine.Configure(WorkflowStep.DetermineDependencies)
            .OnEntryAsync(async () => await _assessor.ExecuteDependencyAnalysis())
            .Permit(WorkflowTrigger.DependenciesResolved, WorkflowStep.IdentifyComponents);

        _machine.Configure(WorkflowStep.IdentifyComponents)
            .OnEntryAsync(async () => await _assessor.ExecuteComponentIdentification())
            .Permit(WorkflowTrigger.ComponentsIdentified, WorkflowStep.SelectNextComponent);

        _machine.Configure(WorkflowStep.SelectNextComponent)
            .OnEntryAsync(async () => await _assessor.ExecuteComponentSelection())
            .Permit(WorkflowTrigger.ComponentSelected, WorkflowStep.BreakIntoTasks)
            .Permit(WorkflowTrigger.AllComponentsDone, WorkflowStep.Complete);

        _machine.Configure(WorkflowStep.BreakIntoTasks)
            .OnEntryAsync(async () => await _assessor.ExecuteTaskBreakdown())
            .Permit(WorkflowTrigger.TasksBroken, WorkflowStep.IterateThroughTasks);

        // Phase 3 — Building
        _machine.Configure(WorkflowStep.IterateThroughTasks)
            .OnEntryAsync(async () => await _assessor.ExecuteTaskIteration())
            .Permit(WorkflowTrigger.TasksCompleted, WorkflowStep.Repeat);

        _machine.Configure(WorkflowStep.Repeat)
            .PermitDynamic(WorkflowTrigger.Assess, () =>
                _assessor.HasMoreComponents()
                    ? WorkflowStep.SelectNextComponent
                    : WorkflowStep.Complete);

        // Global transition handler for observability
        _machine.OnTransitionedAsync(t =>
        {
            Console.WriteLine($"[Workflow] {t.Source} → {t.Destination} via {t.Trigger}");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Re-entrant entry point. Assesses actual state and enters at correct step.
    /// </summary>
    public async Task RunIterationAsync()
    {
        // The external state accessor (_assessor.GetCurrentStep()) is called
        // each time _machine.State is read — this IS the re-entrant assessment.
        // The assessor reads the codebase/session and returns the true current step.
        var currentStep = _machine.State;

        var permittedTriggers = _machine.GetPermittedTriggers();
        // Execute step logic and determine which trigger to fire based on results...
    }

    public string ExportDiagram() => MermaidGraph.Format(_machine.GetInfo());
}
```

### Pros

| Advantage | Detail |
|---|---|
| **External state storage** | Constructor accepts `() => state` / `(s) => state = s` delegates — ideal for re-entrant assessment where state is derived from codebase inspection each time |
| **Guard clauses** | `PermitIf` / `PermitReentryIf` directly model the human-gate (spec approval) and conditional transitions |
| **PermitDynamic** | Step 7 (Repeat) can dynamically route to Step 4 or Complete based on runtime assessment |
| **Async-first** | `OnEntryAsync`, `FireAsync`, `PermitIfAsync` — all async by default |
| **Introspection** | `GetPermittedTriggers()`, `GetInfo()` — useful for TUI to show available transitions |
| **Diagram export** | Built-in Mermaid and DOT export — matches Lopen's observability needs |
| **Battle-tested** | Used by GitHub, Microsoft, Azure. 15M+ downloads. Minimal API surface. |
| **Zero dependencies** | No transitive dependency burden |

### Cons

| Disadvantage | Detail |
|---|---|
| **Not thread-safe** | Single-threaded only — fine for Lopen (sequential orchestration) but won't parallelize steps |
| **Trigger-driven, not assessment-driven** | The library expects you to _fire triggers_; re-entrant "assess and jump to step" requires the external state storage pattern (accessor delegate) to work around this |
| **No built-in persistence** | State serialization is your responsibility — but the delegate pattern makes this trivial |
| **Configuration is imperative** | Transitions defined in code, not data — harder to modify at runtime |

### Verdict for Lopen

**Strong fit.** External state storage is the exact pattern needed for re-entrant assessment. The accessor delegate means "where are we?" is always answered by inspecting reality, not trusting cached state. Guard clauses naturally express the human gate. `PermitDynamic` handles the Step 7 → Step 4 loop.

---

## Pattern 2: Manual Enum-Based State Machine (Switch/Pattern Matching)

### Description

A hand-rolled state machine using C# enums for states and `switch` expressions / pattern matching for transitions. No external dependencies. Maximum control over assessment logic and transition rules. This is the simplest pattern and often sufficient for workflow orchestrators with a small, well-defined state space.

**NuGet**: None required.

### Code Example

```csharp
public enum WorkflowStep
{
    DraftSpecification,
    DetermineDependencies,
    IdentifyComponents,
    SelectNextComponent,
    BreakIntoTasks,
    IterateThroughTasks,
    Repeat,
    Complete
}

public enum Phase { RequirementGathering, Planning, Building }

public record WorkflowState(WorkflowStep Step, Phase Phase, bool SpecApproved = false);

public class WorkflowOrchestrator(IStateAssessor assessor)
{
    /// <summary>
    /// Core re-entrant loop. Each call assesses reality and enters at the correct step.
    /// </summary>
    public async Task<WorkflowState> RunIterationAsync(CancellationToken ct = default)
    {
        // Re-entrant: assess actual codebase state — never trust prior session
        var state = await assessor.AssessCurrentStateAsync(ct);

        while (state.Step != WorkflowStep.Complete)
        {
            ct.ThrowIfCancellationRequested();

            state = state.Step switch
            {
                WorkflowStep.DraftSpecification => await HandleDraftSpec(state, ct),
                WorkflowStep.DetermineDependencies => await HandleDependencies(state, ct),
                WorkflowStep.IdentifyComponents => await HandleComponents(state, ct),
                WorkflowStep.SelectNextComponent => await HandleSelectComponent(state, ct),
                WorkflowStep.BreakIntoTasks => await HandleTaskBreakdown(state, ct),
                WorkflowStep.IterateThroughTasks => await HandleTaskIteration(state, ct),
                WorkflowStep.Repeat => HandleRepeat(state),
                WorkflowStep.Complete => state,
                _ => throw new InvalidOperationException($"Unknown step: {state.Step}")
            };
        }

        return state;
    }

    private async Task<WorkflowState> HandleDraftSpec(WorkflowState state, CancellationToken ct)
    {
        await assessor.ExecuteDraftSpec(ct);

        // Human gate — cannot proceed without explicit approval
        if (!state.SpecApproved)
        {
            return state; // Remain in DraftSpecification — caller must re-enter with approval
        }

        return state with { Step = WorkflowStep.DetermineDependencies, Phase = Phase.Planning };
    }

    private async Task<WorkflowState> HandleDependencies(WorkflowState state, CancellationToken ct)
    {
        await assessor.ExecuteDependencyAnalysis(ct);
        return state with { Step = WorkflowStep.IdentifyComponents };
    }

    private async Task<WorkflowState> HandleComponents(WorkflowState state, CancellationToken ct)
    {
        await assessor.ExecuteComponentIdentification(ct);
        return state with { Step = WorkflowStep.SelectNextComponent };
    }

    private async Task<WorkflowState> HandleSelectComponent(WorkflowState state, CancellationToken ct)
    {
        var hasMore = await assessor.ExecuteComponentSelection(ct);
        return hasMore
            ? state with { Step = WorkflowStep.BreakIntoTasks }
            : state with { Step = WorkflowStep.Complete };
    }

    private async Task<WorkflowState> HandleTaskBreakdown(WorkflowState state, CancellationToken ct)
    {
        await assessor.ExecuteTaskBreakdown(ct);
        return state with { Step = WorkflowStep.IterateThroughTasks, Phase = Phase.Building };
    }

    private async Task<WorkflowState> HandleTaskIteration(WorkflowState state, CancellationToken ct)
    {
        await assessor.ExecuteTaskIteration(ct);
        return state with { Step = WorkflowStep.Repeat };
    }

    private WorkflowState HandleRepeat(WorkflowState state)
    {
        return assessor.HasMoreComponents()
            ? state with { Step = WorkflowStep.SelectNextComponent, Phase = Phase.Planning }
            : state with { Step = WorkflowStep.Complete };
    }
}
```

### Pros

| Advantage | Detail |
|---|---|
| **Full control** | Re-entrant assessment is first-class — `AssessCurrentStateAsync()` runs at the top of every iteration |
| **No dependencies** | Zero NuGet packages, zero abstraction leakage |
| **Transparent** | Every transition is explicit, debuggable, and visible in a single file |
| **Pattern matching** | C# 12+ switch expressions are concise and exhaustive (compiler warns on missing cases) |
| **Immutable state** | `record` with `with` expressions gives safe state transitions |
| **Easy to test** | Pure functions: given state → returned state. No framework mocking needed |
| **Dynamic entry** | `AssessCurrentStateAsync` can return ANY step — no need to "walk through" prior states |

### Cons

| Disadvantage | Detail |
|---|---|
| **Manual boilerplate** | Every transition is hand-coded — grows linearly with states |
| **No guard clause abstraction** | Human gate logic is mixed into handler methods |
| **No introspection** | Can't ask "what transitions are available from here?" without building it yourself |
| **No diagram export** | Must build visualization manually |
| **Transition validation** | No compile-time or runtime validation that the state graph is well-formed |
| **Scales poorly** | Works great for 7–10 states; painful beyond ~20 states |

### Verdict for Lopen

**Good fit for simplicity.** The 7-step workflow is small enough that manual control is practical. The re-entrant pattern is maximally explicit — assess, enter, execute. The downside is losing Stateless's introspection and diagram export, which are valuable for Lopen's TUI and observability goals.

---

## Pattern 3: Workflow-as-Data (Declarative Transitions)

### Description

Define the state machine as a data structure — transitions are records in a collection, not imperative code. The engine reads the transition table and executes accordingly. This separates the _definition_ of the workflow from its _execution_, making it serializable, inspectable, and modifiable at runtime.

This pattern is used by workflow engines (Elsa Workflows, Azure Durable Functions) and is natural for orchestrators where the workflow shape might evolve.

**NuGet**: None required for the pattern. [Elsa Workflows](https://www.nuget.org/packages/Elsa) (`Elsa` v3.x) is a full framework option but is heavyweight for this use case.

### Code Example

```csharp
public enum WorkflowStep
{
    DraftSpecification,
    DetermineDependencies,
    IdentifyComponents,
    SelectNextComponent,
    BreakIntoTasks,
    IterateThroughTasks,
    Repeat,
    Complete
}

public enum Phase { RequirementGathering, Planning, Building }

public enum GateType { None, HumanApproval, Condition }

public record Transition(
    WorkflowStep From,
    WorkflowStep To,
    GateType Gate = GateType.None,
    string? GateKey = null);

public record StepDefinition(
    WorkflowStep Step,
    Phase Phase,
    Func<IStepContext, CancellationToken, Task<StepResult>> Execute);

public record StepResult(bool Success, string? NextGateKey = null);

public class WorkflowDefinition
{
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
    public required IReadOnlyList<Transition> Transitions { get; init; }

    /// <summary>
    /// Lopen's 7-step workflow defined as pure data.
    /// </summary>
    public static WorkflowDefinition CreateDefault(IStepExecutor executor) => new()
    {
        Steps =
        [
            new(WorkflowStep.DraftSpecification, Phase.RequirementGathering, executor.DraftSpec),
            new(WorkflowStep.DetermineDependencies, Phase.Planning, executor.DetermineDeps),
            new(WorkflowStep.IdentifyComponents, Phase.Planning, executor.IdentifyComponents),
            new(WorkflowStep.SelectNextComponent, Phase.Planning, executor.SelectComponent),
            new(WorkflowStep.BreakIntoTasks, Phase.Planning, executor.BreakIntoTasks),
            new(WorkflowStep.IterateThroughTasks, Phase.Building, executor.IterateTasks),
            new(WorkflowStep.Repeat, Phase.Building, executor.EvaluateRepeat),
        ],
        Transitions =
        [
            // Phase 1 → Phase 2 (human-gated)
            new(WorkflowStep.DraftSpecification, WorkflowStep.DetermineDependencies,
                GateType.HumanApproval, "spec-approved"),

            // Phase 2 linear
            new(WorkflowStep.DetermineDependencies, WorkflowStep.IdentifyComponents),
            new(WorkflowStep.IdentifyComponents, WorkflowStep.SelectNextComponent),

            // Select → Tasks or Complete (conditional)
            new(WorkflowStep.SelectNextComponent, WorkflowStep.BreakIntoTasks,
                GateType.Condition, "has-more-components"),
            new(WorkflowStep.SelectNextComponent, WorkflowStep.Complete,
                GateType.Condition, "all-components-done"),

            new(WorkflowStep.BreakIntoTasks, WorkflowStep.IterateThroughTasks),
            new(WorkflowStep.IterateThroughTasks, WorkflowStep.Repeat),

            // Repeat → back to Select or Complete (conditional)
            new(WorkflowStep.Repeat, WorkflowStep.SelectNextComponent,
                GateType.Condition, "has-more-components"),
            new(WorkflowStep.Repeat, WorkflowStep.Complete,
                GateType.Condition, "all-components-done"),
        ]
    };
}

public class WorkflowEngine(
    WorkflowDefinition definition,
    IStateAssessor assessor,
    IGateEvaluator gates)
{
    private readonly Dictionary<WorkflowStep, StepDefinition> _steps =
        definition.Steps.ToDictionary(s => s.Step);

    private readonly ILookup<WorkflowStep, Transition> _transitions =
        definition.Transitions.ToLookup(t => t.From);

    /// <summary>
    /// Re-entrant: assess reality, resolve correct step, execute from there.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        var currentStep = await assessor.AssessCurrentStepAsync(ct);

        while (currentStep != WorkflowStep.Complete)
        {
            ct.ThrowIfCancellationRequested();

            if (!_steps.TryGetValue(currentStep, out var stepDef))
                throw new InvalidOperationException($"No definition for step: {currentStep}");

            var context = await assessor.BuildContextAsync(currentStep, ct);
            var result = await stepDef.Execute(context, ct);

            currentStep = await ResolveNextStepAsync(currentStep, result, ct);
            await assessor.PersistStepAsync(currentStep, ct);
        }
    }

    private async Task<WorkflowStep> ResolveNextStepAsync(
        WorkflowStep from, StepResult result, CancellationToken ct)
    {
        foreach (var transition in _transitions[from])
        {
            var canTransition = transition.Gate switch
            {
                GateType.None => true,
                GateType.HumanApproval => await gates.CheckHumanApprovalAsync(transition.GateKey!, ct),
                GateType.Condition => await gates.EvaluateConditionAsync(transition.GateKey!, ct),
                _ => false
            };

            if (canTransition) return transition.To;
        }

        throw new InvalidOperationException($"No valid transition from {from}");
    }

    /// <summary>
    /// Introspection: return available transitions from a given step.
    /// </summary>
    public IEnumerable<Transition> GetTransitionsFrom(WorkflowStep step) => _transitions[step];

    /// <summary>
    /// Validate the workflow graph for completeness.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        foreach (var step in _steps.Keys)
        {
            if (!_transitions[step].Any() && step != WorkflowStep.Complete)
                errors.Add($"Step {step} has no outgoing transitions");
        }

        var reachable = new HashSet<WorkflowStep>();
        Traverse(definition.Steps[0].Step, reachable);
        foreach (var step in _steps.Keys.Where(s => !reachable.Contains(s)))
            errors.Add($"Step {step} is unreachable");

        return errors;
    }

    private void Traverse(WorkflowStep step, HashSet<WorkflowStep> visited)
    {
        if (!visited.Add(step)) return;
        foreach (var t in _transitions[step])
            Traverse(t.To, visited);
    }
}
```

### Pros

| Advantage | Detail |
|---|---|
| **Serializable** | Workflow shape can be persisted as JSON, loaded from config, or modified at runtime |
| **Introspectable** | Transition table is queryable — "what can happen from here?" is a LINQ query |
| **Testable** | Workflow definition and engine are fully decoupled — test either independently |
| **Declarative gates** | Human gates, conditions, and automatic transitions are expressed uniformly as data |
| **Validation** | Can validate the graph at startup (reachability, dead ends, missing transitions) |
| **Extensible** | Adding a step = adding a record. No imperative code changes to the engine. |
| **Visualization** | Transition list trivially maps to Mermaid/DOT diagram generation |

### Cons

| Disadvantage | Detail |
|---|---|
| **More upfront design** | Requires the engine, gate evaluator, and context builder abstractions |
| **Transition ordering matters** | First matching transition wins — implicit priority can cause subtle bugs |
| **No ecosystem** | Hand-built; no community validation or battle-testing |
| **Indirection** | Debugging requires tracing through the engine rather than reading sequential code |
| **Gate evaluation coupling** | `IGateEvaluator` must map string keys to runtime conditions — another layer to maintain |

### Verdict for Lopen

**Strong fit for evolvability.** If the workflow shape is expected to change (new steps, reordered phases, different gating strategies), data-driven definitions are ideal. The declarative transition table also aligns well with Lopen's specification-driven philosophy — the workflow _is_ a spec. The cost is more upfront abstraction.

---

## Comparison Matrix

| Criterion | Stateless Library | Manual Enum/Switch | Workflow-as-Data |
|---|---|---|---|
| **Re-entrant assessment** | ✅ External state storage delegates | ✅ Explicit assess-at-top-of-loop | ✅ Explicit assess-at-top-of-loop |
| **Dynamic step entry** | ⚠️ Via state accessor (indirect) | ✅ Direct — assessor returns any step | ✅ Direct — assessor returns any step |
| **Human gate** | ✅ Guard clauses | ✅ Manual conditional | ✅ Declarative gate type |
| **Back-pressure integration** | ⚠️ Must wrap in OnEntry handlers | ✅ Natural — checks in handler code | ✅ Natural — checks in engine loop |
| **Introspection / TUI** | ✅ `GetPermittedTriggers()`, `GetInfo()` | ❌ Must build manually | ✅ Query transition table |
| **Diagram export** | ✅ Built-in Mermaid + DOT | ❌ Must build manually | ⚠️ Easy to build from data |
| **Testability** | ⚠️ Requires mocking triggers | ✅ Pure functions | ✅ Decoupled definition/engine |
| **Dependencies** | 1 NuGet (zero transitive) | None | None |
| **Lines of code (approx.)** | ~80 config | ~120 handlers | ~150 engine + definition |
| **Learning curve** | Medium (Stateless API) | Low | Medium (abstractions) |
| **Scales beyond 7 steps** | ✅ Well | ❌ Gets unwieldy | ✅ Well |

---

## Recommendation

**Hybrid: Workflow-as-Data definition + Stateless execution engine.**

The strongest approach for Lopen combines patterns:

1. **Define the workflow declaratively** (Pattern 3) — steps, phases, transitions, and gate types as data. This aligns with Lopen's spec-driven philosophy and enables introspection, validation, and visualization.

2. **Use Stateless as the execution engine** (Pattern 1) — configure the `StateMachine` from the data definition at startup. This gives you battle-tested transition mechanics, guard clauses, async support, and Mermaid export for free.

3. **Keep the re-entrant assessor as the state accessor** — Stateless's external state storage pattern (`() => assessor.GetCurrentStep()`) ensures every state read is an assessment of reality.

If the project wants zero dependencies, Pattern 2 (manual enum/switch) is sufficient for 7 steps and maximally transparent — but loses introspection and diagramming that Lopen's TUI and observability requirements call for.

### Recommended NuGet

| Package | Version | Purpose |
|---|---|---|
| `Stateless` | 5.20.1 | State machine execution, transitions, guards, diagram export |

---

## Additional Library Noted

**NxGraph** (`NuGet: NxGraph`) — A zero-allocation, high-performance FSM for .NET 8+ with a fluent DSL, OpenTelemetry tracing, Mermaid export, and replay support. Interesting for hot-path scenarios but designed for _forward-only linear/branching execution_, not re-entrant assessment. It lacks external state storage and dynamic entry, making it unsuitable for Lopen's re-entrant requirement. Worth monitoring for sub-task execution engines where performance matters.

## References

- [Stateless GitHub](https://github.com/dotnet-state-machine/stateless) — Source, docs, examples
- [Stateless NuGet](https://www.nuget.org/packages/Stateless) — v5.20.1, .NET 8/9/10
- [NxGraph GitHub](https://github.com/Enzx/NxGraph) — Zero-allocation FSM for .NET 8+
- [Core Specification](./SPECIFICATION.md) — Lopen's 7-step workflow definition
