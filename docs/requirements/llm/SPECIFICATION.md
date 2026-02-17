---
name: llm
description: The LLM integration requirements of Lopen using the GitHub Copilot SDK
---

# LLM Specification

## Overview

Lopen uses the **GitHub Copilot SDK** as its sole LLM backend. This module defines how Lopen integrates with the SDK, manages model selection, registers tools, constructs prompts, executes the orchestration loop, and tracks token/cost metrics.

Lopen is an **orchestrator**, not an agent itself. It drives the Copilot SDK in a loop, providing context and instructions each iteration, and interpreting the results to advance the [Core Workflow](../core/SPECIFICATION.md#core-development-workflow).

### Design Principles

1. **Copilot SDK Only** — No abstraction layer for other providers; GitHub Copilot models exclusively
2. **Hybrid Tool Strategy** — Lopen registers domain-specific tools for orchestration; the SDK uses native tools for implementation
3. **Per-Step Model Selection** — Users can assign different models to different workflow phases
4. **Token Conscious** — Token usage and premium API requests are first-class metrics
5. **Loop-Driven** — Each workflow phase and task iteration is a discrete SDK invocation with fresh context

---

## Copilot SDK Integration

### Authentication

- Lopen authenticates via the user's GitHub credentials (same as Copilot CLI)
- Authentication is handled by the Copilot SDK — Lopen does not manage tokens or OAuth flows directly
- If authentication fails, Lopen surfaces a clear error and blocks (this is a critical system error per [Core § Failure Handling](../core/SPECIFICATION.md#failure-handling--self-correction))

### SDK Invocation

Each loop iteration follows this pattern:

1. **Lopen prepares context** — Reads session state, specs, and task progress; determines the current workflow step (see [Core § Re-entrant Assessment](../core/SPECIFICATION.md#re-entrant-assessment))
2. **Lopen constructs the prompt** — Assembles a system prompt with workflow instructions, current step context, and relevant document sections
3. **Lopen invokes the SDK** — Calls the Copilot SDK with the constructed prompt, selected model, and registered tools
4. **SDK executes** — The LLM reasons, calls tools (both Lopen tools and native tools), and produces output
5. **Lopen interprets results** — Parses the LLM's output, updates session state, and determines whether to advance the workflow or loop again

### Loop Execution

The orchestration loop replaces the current `scripts/lopen.sh` bash loop:

- **Loop condition**: Continue until the current module's workflow is complete (all components built) or the user intervenes
- **Termination signals**: Module complete, user interrupt, critical error, or maximum iteration limit (configurable via [Configuration](../configuration/SPECIFICATION.md))

### Context Window Strategy

Each SDK invocation receives a **fresh context window** — no conversation history is carried forward between invocations. Session state serves as memory. Fresh context boundaries are:

| Boundary                                  | SDK Invocation                            | Context Scope                                                       |
| ----------------------------------------- | ----------------------------------------- | ------------------------------------------------------------------- |
| **Requirement Gathering** (Step 1)        | Single invocation for the whole phase     | Spec draft, user conversation, research                             |
| **Planning** (Steps 2–5)                  | Single invocation for the whole phase     | Dependencies, components, next component selection, task breakdown  |
| **Task Execution** (Step 6, per task)     | One invocation per task                   | Current task, relevant spec sections, component context             |
| **Component Repeat** (Step 7 → Steps 4–5) | One invocation to plan the next component | Updated component status, remaining components, next task breakdown |

**Key implications**:

- Planning runs steps 2–5 in a single context window — the LLM sees dependencies, components, and task breakdown together
- Each task in step 6 gets its own fresh context; the oracle verification loop (see [Oracle Verification Tools](#oracle-verification-tools)) runs **within** the same SDK invocation as the task, not as a separate call
- When step 7 repeats to step 4, Lopen creates a fresh context for the Planning phase (steps 4–5 only, since steps 2–3 are already complete), then returns to per-task fresh contexts for step 6

---

## Model Selection

### Per-Step Configuration

Users can assign different Copilot models to different workflow phases via [Configuration](../configuration/SPECIFICATION.md):

| Phase                 | Default Model     | Rationale                                     |
| --------------------- | ----------------- | --------------------------------------------- |
| Requirement Gathering | `claude-opus-4.6` | Nuanced conversation, spec quality matters    |
| Planning              | `claude-opus-4.6` | Architecture decisions need strong reasoning  |
| Building              | `claude-opus-4.6` | Code generation accuracy is critical          |
| Research              | `claude-opus-4.6` | Consistent default; users can override to save premium requests |

- Configuration uses specific Copilot SDK model names directly (e.g., `claude-opus-4.6`, `claude-sonnet-4`) — no abstract tier system
- Users can override any phase assignment to a different model
- Model identifiers must be valid Copilot SDK model names

### Model Fallback

If a configured model is unavailable (rate limited, deprecated):

1. Lopen logs a warning
2. Falls back to the next available model (Lopen maintains a fallback order in built-in defaults)
3. If no models available, surfaces error to user

---

## Hybrid Tool Strategy

Lopen uses a **hybrid approach** to tool registration with the Copilot SDK:

### Lopen-Managed Tools (Orchestration)

These tools are registered by Lopen and executed by Lopen when the LLM calls them. They give Lopen control over its own state:

| Tool                          | Purpose                                                                                                              |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `read_spec`                   | Read a specific section from a specification document                                                                |
| `read_research`               | Read findings from a research document                                                                               |
| `read_plan`                   | Read the current plan with task statuses                                                                             |
| `update_task_status`          | Mark a task as pending, in-progress, complete, or failed                                                             |
| `get_current_context`         | Retrieve the current workflow step, module, component, task                                                          |
| `log_research`                | Save research findings to `docs/requirements/{module}/RESEARCH-{topic}.md`                                           |
| `report_progress`             | Report what was accomplished in this iteration                                                                       |
| `verify_task_completion`      | Dispatch oracle sub-agent to verify a task is complete (see [Oracle Verification Tools](#oracle-verification-tools)) |
| `verify_component_completion` | Dispatch oracle sub-agent to verify all tasks in a component are complete                                            |
| `verify_module_completion`    | Dispatch oracle sub-agent to verify the module meets all acceptance criteria                                         |

**Benefits**:

- **Token efficient** — The LLM calls `read_spec("auth", "§JWT")` and Lopen returns only that section
- **State integrity** — Lopen updates its own state programmatically, not via LLM file edits
- **Guardrails** — Lopen controls what state the LLM can mutate

### Native Tools (Implementation)

The Copilot SDK's built-in tools remain available for implementation work:

- File read/write
- Shell command execution
- Git operations
- Web search (when available)

Lopen does **not** restrict or wrap these tools. The LLM uses them freely during task execution (step 6).

### Tool Registration

- Tools are registered with the SDK at the start of each invocation
- The tool set may vary by workflow step (e.g., `log_research` is only available during research phases; `verify_task_completion` is only available during step 6)
- Tool schemas follow the Copilot SDK's function-calling format

### Oracle Verification Tools

The `verify_task_completion`, `verify_component_completion`, and `verify_module_completion` tools implement [Core § Oracle Verification](../core/SPECIFICATION.md#oracle-verification) as Lopen-managed tools. When the LLM calls one of these tools, Lopen's tool handler:

1. Collects the relevant diff, test results, and acceptance criteria for the requested scope
2. Dispatches a **cheap/fast model** (e.g., gpt-5-mini, gpt-4o) as a sub-agent to review the evidence against the specification in the same SDK session as a sub-agent
3. Returns the oracle's findings (pass/fail with specific gaps) back to the primary LLM as the tool result

**This happens within the same SDK invocation** — the tool call round-trip is part of the primary model's tool-calling loop, so it does **not** consume an additional premium request. The oracle model call is a standard-tier request.

**Tiered verification scope**:

| Tool                          | Scope                            | Checks                                                                                  |
| ----------------------------- | -------------------------------- | --------------------------------------------------------------------------------------- |
| `verify_task_completion`      | Single task diff                 | Task requirements met, tests exist/pass, code quality hooks pass                        |
| `verify_component_completion` | All task diffs for the component | Component-level acceptance criteria, no regressions across tasks, integration coherence |
| `verify_module_completion`    | Full module                      | All acceptance criteria in the spec, full test suite, cross-component integration       |

**Enforcement**: These tools are **mandatory** — Lopen's [back-pressure](../core/SPECIFICATION.md#back-pressure) rejects any `update_task_status(complete)` call that was not preceded by the corresponding `verify_*_completion` call in the same invocation. The LLM must call the verification tool and receive a passing result before marking work complete.

**Retry loop**: If the oracle reports gaps, the primary LLM sees the findings in its context and can address them, then call the verification tool again. This loop continues within the same SDK invocation until: verification passes, churn limits are hit, or the LLM gives up.

---

## Prompt Construction

### System Prompt Structure

Each SDK invocation includes a structured system prompt assembled by Lopen:

1. **Role and identity** — "You are working within Lopen, an orchestrator for module development..."
2. **Current workflow state** — Phase, step, module, component, task
3. **Instructions for current step** — What the LLM should do in this iteration
4. **Relevant context** — Spec sections, research excerpts, plan status (loaded via document management, see [Core § Document Management](../core/SPECIFICATION.md#document-management))
5. **Available tools** — List of Lopen tools and guidance on when to use them
6. **Constraints** — Token budget hints, commit conventions, coding standards

### Context Window Management

- Lopen aggressively manages what goes into each context window
- Only the current task's relevant documents are included (section-level, not full documents)
- Previous invocation history is **not** carried forward — session state serves as the memory
- Within an invocation, the SDK's tool-calling loop accumulates context naturally (tool calls, oracle verification results)
- If context would exceed budget, Lopen truncates or summarizes lower-priority sections

---

## Token & Cost Tracking

### Metrics

Lopen treats token usage and premium API requests as **first-class metrics**:

- **Context Window Usage**: `{used}/{total}` tokens per iteration (e.g., `2.4K/128K`)
- **Premium Request Counter**: Cumulative premium API calls in the current session (e.g., `🔥 23`)
- **Session Totals**: Aggregate token usage across all iterations in a session

These metrics are surfaced to the [TUI](../tui/SPECIFICATION.md) for display and persisted in [session state](../storage/SPECIFICATION.md).

### Tracking Mechanism

- Token counts are read from Copilot SDK response metadata
- Lopen records per-iteration and cumulative metrics
- Premium vs. standard request classification based on model tier

### Efficiency Strategies

1. **Section-level extraction** — Only relevant document sections sent to LLM (see [Core § Document Management](../core/SPECIFICATION.md#document-management))
2. **Programmatic updates** — Bookkeeping (checkboxes, status) handled in code, not by the LLM
3. **Fresh context per invocation** — No conversation history between SDK invocations; session state is the memory (see [Context Window Strategy](#context-window-strategy))
4. **Minimal prompts** — Structured prompts avoid unnecessary verbosity

---

## Notes

This specification defines **how Lopen integrates with the LLM backend**. It does not define what the LLM is asked to do (that's the [Core Workflow](../core/SPECIFICATION.md)) or how results are displayed (that's the [TUI](../tui/SPECIFICATION.md)).

---

## Acceptance Criteria

- [x] [LLM-01] Lopen authenticates with the Copilot SDK using credentials from the [Auth module](../auth/SPECIFICATION.md)
- [ ] [LLM-02] Each workflow phase invokes the SDK with a fresh context window (no conversation history carried forward)
- [ ] [LLM-03] System prompt includes: role/identity, workflow state, step instructions, relevant context, available tools, constraints
- [ ] [LLM-04] Context window contains only section-level document extractions, not full documents
- [ ] [LLM-05] Lopen-managed tools (`read_spec`, `read_research`, `read_plan`, `update_task_status`, `get_current_context`, `log_research`, `report_progress`) are registered and functional
- [ ] [LLM-06] Oracle verification tools (`verify_task_completion`, `verify_component_completion`, `verify_module_completion`) dispatch a sub-agent and return pass/fail verdicts
- [ ] [LLM-07] Oracle verification runs within the same SDK invocation (no additional premium request consumed)
- [ ] [LLM-08] `update_task_status(complete)` is rejected unless preceded by a passing `verify_*_completion` call in the same invocation
- [ ] [LLM-09] Tool registration varies by workflow step (e.g., `log_research` only available during research phases)
- [ ] [LLM-10] Per-phase model selection works — each phase can use a different configured model
- [x] [LLM-11] Model fallback activates when a configured model is unavailable (logs warning, falls back to next available)
- [ ] [LLM-12] Token usage metrics (context window usage, premium request count, session totals) are read from SDK response metadata and recorded
- [x] [LLM-13] Token metrics are surfaced to the TUI and persisted in session state
- [ ] [LLM-14] Context window budget is respected — lower-priority sections truncated or summarized when context would exceed budget

---

## Dependencies

- **[Copilot SDK](https://github.com/features/copilot)** — LLM invocation, tool calling, authentication, response metadata (token counts)
- **[Auth module](../auth/SPECIFICATION.md)** — Credential provision for SDK authentication
- **[Core module](../core/SPECIFICATION.md)** — Workflow state, task hierarchy, document management, back-pressure enforcement
- **[Storage module](../storage/SPECIFICATION.md)** — Session state persistence, token metrics storage
- **[Configuration module](../configuration/SPECIFICATION.md)** — Model assignments, budget settings, oracle model selection

---

## Skills & Hooks

- **verify-sdk-connection**: Lightweight SDK call to validate authentication and model availability before workflow start
- **track-token-usage**: Record per-iteration token metrics after each SDK invocation

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow logic, task management, failure handling
- [Storage Specification](../storage/SPECIFICATION.md) — Where session state and metrics are persisted
- [Configuration Specification](../configuration/SPECIFICATION.md) — Model assignments, token budgets, feature flags
- [TUI Specification](../tui/SPECIFICATION.md) — How metrics and progress are displayed
