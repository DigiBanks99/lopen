---
name: core
description: The core workflow and orchestration requirements of Lopen
---

# Core Lopen Specification

## Overview

Lopen is an intelligent coding assistant and orchestrator that autonomously builds software modules from specifications. It drives the GitHub Copilot SDK in a structured loop, managing workflow state and context between iterations, replacing manual prompt engineering with a proper orchestration layer.

### Design Principles

1. **Opinionated** — Makes sensible default decisions to avoid over-engineering generic solutions
2. **Autonomous** — Self-corrects failures and advances through workflow automatically
3. **Re-entrant** — Assesses actual codebase state each iteration rather than trusting stale session data
4. **User Informed** — Correlates specifications, research, plans, and tasks automatically
5. **Structured** — Follows a consistent 7-step process for module development

> Token efficiency, cost tracking, and model selection are concerns of the [LLM module](../llm/SPECIFICATION.md). Storage and persistence are concerns of the [Storage module](../storage/SPECIFICATION.md). Settings and defaults are concerns of the [Configuration module](../configuration/SPECIFICATION.md).

---

## Core Development Workflow

Lopen guides the LLM through a structured 7-step process for building modules. The workflow is **always** the 7 steps, but it is **re-entrant**: at each loop iteration, Lopen assesses where the project actually stands and enters at the correct step.

### The 7 Steps

1. **Draft Specification** — User provides initial idea, Lopen conducts guided conversation to gather requirements
2. **Determine Dependencies** — Identify what's needed to make the specification work (libraries, APIs, other modules)
3. **Identify Components** — Break down the module into logical components; assess existing codebase against the spec to determine what is already done, partially done, or missing
4. **Select Next Component** — Choose the next most important component to build; re-assess codebase state to verify prior completion claims
5. **Break Into Tasks** — Decompose the component into achievable atomic tasks
6. **Iterate Through Tasks** — Execute tasks via the LLM, self-correct on failures, track progress
7. **Repeat** — Return to step 4 until all components are complete

### The 3 Phases

- **Requirement Gathering** (Step 1) — Define what needs to be built
- **Planning** (Steps 2–3) — Understand dependencies and architecture
- **Building** (Steps 4–7) — Iteratively construct components

### Re-entrant Assessment

At each loop iteration, Lopen does **not** blindly trust session state. Instead:

1. Reads the specification and session state (session state is a **hint**, not ground truth)
2. Instructs the LLM to assess the actual codebase against the spec
3. Determines the correct workflow step based on what actually exists
4. May trigger additional research (web, security, architecture) during steps 3–4 if gaps are found
5. Proceeds from the determined step forward

This means if specifications change between iterations, Lopen detects drift and re-enters at the appropriate phase — no manual intervention required.

### Workflow Characteristics

**Automatic Progression**: Lopen automatically advances through steps when completion criteria are met. No manual step transitions required.

**Semi-Automatic Reviews**: At phase boundaries, Lopen may offer reviews (e.g., spec review after step 1). User confirms to proceed and Lopen facilitates the review process.

**Component-by-Component**: In the Building phase, Lopen completes one component fully before moving to the next. User selects which component to tackle first.

**Task-Level Iteration**: Within a component, tasks are completed sequentially with self-correction on failures.

---

## Document Management

### Intelligent Resource Tracking

To minimize token usage and LLM overhead, Lopen includes an intelligent document management layer that operates in-process. Documents are stored according to the [Storage module](../storage/SPECIFICATION.md) conventions.

**Automatic Tracking**:

- Specifications, research documents, and plans are automatically tracked as created
- Resources are associated with modules, components, and tasks
- No manual resource management required from user

**Structured Documents**:

- Specifications use consistent markdown structure with headers
- Research documents follow standard formats
- Plans use markdown checkboxes for task tracking
- Structure enables automated parsing and section extraction

**Section-Level Extraction**:

- Document parser extracts relevant sections based on current context
- Only the sections relevant to the current task are injected into the LLM context
- Lopen provides the LLM with tracking metadata (section locations, resource references)
- Example: When working on authentication, only `§ Authentication` sections are provided

**Programmatic Updates**:

- Task completion updates plan document checkboxes without invoking the LLM
- Component status tracking is maintained by Lopen, not delegated to the LLM
- Reduces unnecessary LLM token consumption for bookkeeping

**Resource Correlation**:

- System automatically correlates specifications → components → tasks
- Current task context includes relevant spec sections, research notes, and plan items
- No manual hunting through documents required

---

## Task Management

### Task Hierarchy

Tasks are organized hierarchically:

```
Module (e.g., authentication)
├─ Component (e.g., auth-module)
│  ├─ Task (e.g., Implement JWT token validation)
│  │  ├─ Subtask (e.g., Parse token from header)
│  │  ├─ Subtask (e.g., Verify signature)
│  │  └─ Subtask (e.g., Check expiration)
│  ├─ Task (e.g., Create refresh token logic)
│  └─ Task (e.g., Write integration tests)
├─ Component (e.g., session-module)
└─ Component (e.g., permission-module)
```

### Task States

- **○ Pending** — Not started
- **▶ In Progress** — Currently being worked on
- **✓ Complete** — Successfully completed
- **✗ Failed** — Failed and blocked (rare — usually self-corrects)

### Task Completion Criteria

A task is only marked complete when:

1. Implementation is finished
2. Tests pass (if applicable)
3. No errors or failures remain
4. Documentation is completed or updated (if applicable)
5. Functionality verified with test tools (if applicable)

If a task fails, the LLM attempts to self-correct. The task remains in "In Progress" state until successful.

---

## Failure Handling & Self-Correction

### Philosophy

Lopen is designed to autonomously recover from failures without blocking progress:

**Show, Don't Block**: Failures are displayed clearly to the user, but execution continues. The LLM attempts self-correction.

**Pattern Detection**: If the same task fails repeatedly (e.g., 3+ times), the system recognizes a pattern and may intervene.

**User Intervention**: When patterns detected, user is asked if they want to continue or intervene manually.

**Unattended Mode**: For fully autonomous operation, intervention prompts can be suppressed via [configuration](../configuration/SPECIFICATION.md).

### Failure Types

| Type                       | Behavior                                 | User Action              |
| -------------------------- | ---------------------------------------- | ------------------------ |
| Single task failure        | Show inline, LLM self-corrects, continue | None — observe           |
| Repeated task failure (3+) | Prompt user to confirm continuation      | Confirm or intervene     |
| Critical system error      | Block and require user action            | Recovery action required |
| Minor warning              | Show inline, continue                    | None — informational     |

---

## Multi-Module Projects

### Module Execution Model

**One Module at a Time**: Lopen completes the full 7-step workflow for one module before starting another.

**User Selection**: When multiple modules exist, user explicitly chooses which to work on next.

**Context Isolation**: Each module has its own specifications, components, and tasks. Modules may reference other modules, for which progressive disclosure is used to load context.

**Progress Tracking**: System tracks completion state of all modules in project.

### Module Selection

When starting work:

1. System scans project for module specifications
2. Lists modules with current state (not started / in progress / complete)
3. User selects module to work on
4. Can switch modules mid-work (session saved per [Storage module](../storage/SPECIFICATION.md))

---

## Ancillary Features

These features support the core workflow but are not part of the 7-step process:

### Research

**Purpose**: Gather information needed for informed implementation decisions.

**When**: Research can occur during any phase, triggered by knowledge gaps. Steps 3–4 commonly trigger research when assessing the codebase reveals unknowns (e.g., security considerations, third-party API patterns, architectural decisions).

**How**:

- Lopen identifies need for research (e.g., "best practices for JWT tokens", "security audit of auth flow")
- Instructs the LLM to conduct research using available tools (web search, codebase analysis, documentation review)
- Creates research document with findings at `docs/requirements/{module}/RESEARCH-{topic}.md` (see [Storage § Research Documents](../storage/SPECIFICATION.md#research-documents))
- Links research to relevant specification sections

**Display**: Research activities shown inline during workflow (see [TUI Specification](../tui/SPECIFICATION.md) for UI details).

**Summaries**: Research summaries provided at phase transitions.

### Specification Review

**Purpose**: Validate that drafted specification is complete and correct before building.

**When**: Offered after Requirement Gathering (step 1), before Planning phase.

**How**:

- Lopen instructs the LLM to analyze the draft specification for gaps, ambiguities, and conflicts
- Presents findings to user
- User decides to revise spec, proceed as-is, or cancel

**Trigger**: Semi-automatic — Lopen offers review, user confirms.

### Plan Refinement

**Purpose**: Ensure implementation plan is achievable and well-structured.

**When**: During Planning phase (steps 2–3).

**How**:

- Lopen breaks down components into tasks
- Creates markdown plan with checkboxes
- User can review and adjust before Building begins

---

## Notes

This specification defines the **core workflow and orchestration behavior** of Lopen independent of user interface, LLM backend, storage mechanism, or configuration system.

## References

- [LLM Specification](../llm/SPECIFICATION.md) — Copilot SDK integration, model selection, tool strategy, token tracking
- [Storage Specification](../storage/SPECIFICATION.md) — Session persistence, document formats, `.lopen/` structure
- [Configuration Specification](../configuration/SPECIFICATION.md) — User preferences, feature flags, model assignments
- [TUI Specification](../tui/SPECIFICATION.md) — Terminal UI layout, progressive disclosure, visual design
