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
5. **Loop-Driven** — Each workflow iteration is a discrete SDK invocation with fresh context

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
- **Iteration boundary**: Each SDK invocation is a fresh context window; Lopen loads only the relevant context from its own state and the file system
- **Termination signals**: Module complete, user interrupt, critical error, or maximum iteration limit (configurable via [Configuration](../configuration/SPECIFICATION.md))

---

## Model Selection

### Per-Step Configuration

Users can assign different Copilot models to different workflow phases via [Configuration](../configuration/SPECIFICATION.md):

| Phase                 | Default Model | Rationale                                     |
| --------------------- | ------------- | --------------------------------------------- |
| Requirement Gathering | Premium       | Nuanced conversation, spec quality matters    |
| Planning              | Premium       | Architecture decisions need strong reasoning  |
| Building              | Premium       | Code generation accuracy is critical          |
| Research              | Standard      | Information gathering is less reasoning-heavy |

- **"Premium"** and **"Standard"** are abstract tiers mapped to specific Copilot SDK model identifiers in configuration
- Users can override any assignment (e.g., use standard-tier for building to save premium requests)
- Model identifiers must be valid Copilot SDK model names

### Model Fallback

If a configured model is unavailable (rate limited, deprecated):

1. Lopen logs a warning
2. Falls back to the next available model in the same tier
3. If no models available in the tier, surfaces error to user

---

## Hybrid Tool Strategy

Lopen uses a **hybrid approach** to tool registration with the Copilot SDK:

### Lopen-Managed Tools (Orchestration)

These tools are registered by Lopen and executed by Lopen when the LLM calls them. They give Lopen control over its own state:

| Tool                  | Purpose                                                                    |
| --------------------- | -------------------------------------------------------------------------- |
| `read_spec`           | Read a specific section from a specification document                      |
| `read_research`       | Read findings from a research document                                     |
| `read_plan`           | Read the current plan with task statuses                                   |
| `update_task_status`  | Mark a task as pending, in-progress, complete, or failed                   |
| `get_current_context` | Retrieve the current workflow step, module, component, task                |
| `log_research`        | Save research findings to `docs/requirements/{module}/RESEARCH-{topic}.md` |
| `report_progress`     | Report what was accomplished in this iteration                             |

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

- Tools are registered with the SDK at the start of each iteration
- The tool set may vary by workflow step (e.g., `log_research` is only available during research phases)
- Tool schemas follow the Copilot SDK's function-calling format

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
- Previous iteration history is **not** carried forward — session state serves as the memory
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
3. **Fresh context per iteration** — No conversation history accumulation; session state is the memory
4. **Minimal prompts** — Structured prompts avoid unnecessary verbosity

---

## Notes

This specification defines **how Lopen integrates with the LLM backend**. It does not define what the LLM is asked to do (that's the [Core Workflow](../core/SPECIFICATION.md)) or how results are displayed (that's the [TUI](../tui/SPECIFICATION.md)).

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow logic, task management, failure handling
- [Storage Specification](../storage/SPECIFICATION.md) — Where session state and metrics are persisted
- [Configuration Specification](../configuration/SPECIFICATION.md) — Model assignments, token budgets, feature flags
- [TUI Specification](../tui/SPECIFICATION.md) — How metrics and progress are displayed
