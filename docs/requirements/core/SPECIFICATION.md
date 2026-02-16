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
6. **Back-Pressure Aware** — Enforces quality, resource, and progress guardrails to prevent wasteful loops

> Token efficiency, cost tracking, and model selection are concerns of the [LLM module](../llm/SPECIFICATION.md). Storage and persistence are concerns of the [Storage module](../storage/SPECIFICATION.md). Settings and defaults are concerns of the [Configuration module](../configuration/SPECIFICATION.md).

---

## Modules

A **module** is a composable feature set that belongs together — an epic-level vertical slice. Each subfolder under `docs/requirements/` represents a module (e.g., `docs/requirements/auth/`, `docs/requirements/core/`).

- Module names correspond to their directory names
- Each module has a `SPECIFICATION.md` following the [Specification Pattern](#specification-pattern)
- Code structure is not enforced by Lopen — it is defined within the module specification or left to the LLM
- The user scopes work by selecting a module; within a module, the LLM has full autonomy over component identification, sequencing, and implementation approach

---

## Core Development Workflow

Lopen guides the LLM through a structured 7-step process for building modules. The workflow is **always** the 7 steps, but it is **re-entrant**: at each loop iteration, Lopen assesses where the project actually stands and enters at the correct step.

### The 7 Steps

1. **Draft Specification** — User provides initial idea, Lopen conducts guided conversation to gather requirements. The specification must follow the [Specification Pattern](#specification-pattern) including Acceptance Criteria
2. **Determine Dependencies** — Identify what's needed to make the specification work (libraries, APIs, other modules)
3. **Identify Components** — Break down the module into logical components; assess existing codebase against the spec to determine what is already done, partially done, or missing
4. **Select Next Component** — LLM chooses the next component to build based on dependency order and progress; re-assesses codebase state to verify prior completion claims
5. **Break Into Tasks** — Decompose the component into achievable atomic tasks
6. **Iterate Through Tasks** — Iteratively complete tasks via the LLM, self-correct on failures, track progress, enforce [Back-Pressure](#back-pressure) guardrails
7. **Repeat** — Return to step 4 until all components are complete and module [Acceptance Criteria](#acceptance-criteria) are satisfied

### The 3 Phases

- **Requirement Gathering** (Step 1) — Define what needs to be built and augment with research
- **Planning** (Steps 2–5) — Understand dependencies and architecture through research, select next most important component and break into tasks
- **Building** (Steps 6–7) — Iteratively construct components

### Phase Transitions

- **Requirement Gathering → Planning**: **Human-gated**. Specification completeness is the user's decision. Lopen may suggest gaps and highlight missing sections, but the user explicitly confirms the spec is ready to proceed. This is the only phase transition requiring user approval.
- **Planning → Building**: Automated. Proceeds when the plan is structurally complete (all components identified, tasks broken down).
- **Building → Complete**: Automated. Module is complete when all components are built and all [Acceptance Criteria](#acceptance-criteria) pass verification.

### Re-entrant Assessment

At each loop iteration, Lopen does **not** blindly trust session state. Instead:

1. Reads the specification and session state (session state is a **hint**, not ground truth)
2. Checks the plan/task tree for claimed-complete items
3. For claimed-complete items, performs targeted verification (checks specific files, tests, and artifacts mentioned)
4. Uses the section cache and file modification timestamps to detect what changed since last iteration
5. Feeds only the delta plus current task context to the LLM
6. Determines the correct workflow step based on what actually exists
7. May dispatch sub-agents for verification of complex claims (see [Oracle Verification](#oracle-verification))
8. Proceeds from the determined step forward

### Specification Drift Detection

Lopen detects when specifications change between iterations:

1. Hashes the content of each relevant SPECIFICATION.md section at every session save
2. On resume or next iteration, compares hashes against the saved values
3. If a spec section changed, flags the drift and reports it to the user
4. Re-enters at the appropriate phase (e.g., if Acceptance Criteria changed, re-assess from step 3)

### Workflow Characteristics

**Automatic Progression**: Lopen automatically advances through steps when completion criteria are met. No manual step transitions required (except the spec→plan human gate).

**LLM-Driven Component Ordering**: Within a module, the LLM identifies components (C4 component level) and determines the build order. The user does not micro-manage component selection — they scope to a module and Lopen handles the rest.

**Task-Level Iteration**: Within a component, tasks are completed sequentially with self-correction on failures.

**Cross-Module Dependencies**: The LLM handles cross-module dependencies autonomously (e.g., creating stubs, interfaces, or building dependent modules as needed). Lopen does not enforce a build order across modules.

---

## Specification Pattern

Every module specification (`SPECIFICATION.md`) must follow a canonical structure that Lopen can parse and enforce:

### Required Sections

```markdown
---
name: <module-name>
description: <brief description>
---

# <Module Name> Specification

## Overview
High-level purpose and scope of the module.

## [Domain-Specific Sections]
One or more sections defining the module's behavior, data model,
interfaces, and constraints. Structure varies per module.

## Acceptance Criteria
Machine-parseable quality gates that must pass before the module
is considered complete. Written as a checkbox list.

## Dependencies
Other modules, libraries, APIs, or external systems this module requires.

## Skills & Hooks
Module-specific tools, skills, or hooks to register with Lopen.
Defines verification commands, linters, or custom checks.

## Notes
Implementation notes, open questions, or caveats.

## References
Links to related specifications and external resources.
```

### Acceptance Criteria

The `## Acceptance Criteria` section defines the conditions under which a module is considered complete. These are the module's quality gates — Lopen enforces them before marking a module as done.

Acceptance criteria are written as a markdown checkbox list:

```markdown
## Acceptance Criteria

- [ ] All public API endpoints return appropriate HTTP status codes
- [ ] Unit test coverage exists for all public methods
- [ ] Integration tests pass for the complete authentication flow
- [ ] No unhandled exceptions in error paths
- [ ] Security linting passes (e.g., OWASP checks for auth module)
```

**Enforcement**:

- Lopen parses acceptance criteria programmatically
- During the Building phase, acceptance criteria are verified at component and module completion boundaries
- Verification uses a combination of tool call tracking, VCS hooks, and [Oracle Verification](#oracle-verification)
- A module cannot be marked complete until all acceptance criteria are satisfied

### Skills & Hooks

The `## Skills & Hooks` section defines module-specific verification and tooling:

```markdown
## Skills & Hooks

- **verify-build**: `dotnet build --no-restore`
- **verify-tests**: `dotnet test --no-build`
- **verify-lint**: `dotnet format --verify-no-changes`
- **pre-commit**: Run all verify-* skills before committing
```

Lopen registers these as tools available to the LLM and enforces that relevant ones are invoked before marking tasks complete.

---

## Back-Pressure

Back-pressure is Lopen's first-class mechanism for preventing wasteful, circular, or low-quality LLM behavior. It is the key differentiator between Lopen's orchestration and naive loop-and-retry approaches.

### Category 1: Resource Limits

Quantitative guardrails on token and premium request consumption.

- **Budgets are per-module** — Each module has a configurable token and premium request budget, tracked across sessions
- **Warning at 80%** — Lopen warns the user when 80% of the module budget is consumed
- **Confirmation at 90%** — Lopen pauses and requires user confirmation to continue past 90%
- **Rate limit recovery** — When the Copilot SDK returns a rate limit error (429), Lopen implements exponential backoff automatically and resumes when available
- Budget thresholds are configurable via [Configuration](../configuration/SPECIFICATION.md)

### Category 2: Progress Integrity

Prevent churn, false completion claims, and circular behavior.

- **Churn detection** — If the same task fails repeatedly (configurable threshold, default 3), Lopen escalates rather than retrying blindly
- **False completion prevention** — When the LLM claims a task is complete, Lopen verifies through:
  1. **Tool call tracking** — Lopen records which tools the LLM invoked. If a task requires running tests but the LLM never called the test runner, the completion claim is rejected
  2. **VCS commit hooks** — Pre-commit hooks (linters, tests, formatters) that the LLM cannot bypass. Lopen helps set these up in the repository
  3. **Oracle verification** — Mandatory oracle sub-agent validates the diff against the spec and acceptance criteria via `verify_task_completion`, `verify_component_completion`, or `verify_module_completion` tools (see [Oracle Verification](#oracle-verification))
- **Circular behavior detection** — Lopen tracks iteration patterns (e.g., same file read 3+ times, same command re-run without changes) and intervenes with corrective instructions

### Category 3: Quality Gates

Standards enforcement and best-practice validation.

- **Module-level gates** — Defined in the spec's [Acceptance Criteria](#acceptance-criteria) section; enforced at module completion
- **Component-level gates** — Derived from acceptance criteria; enforced at component completion
- **Built-in defaults** — Lopen ships with default quality rules (e.g., "tests must exist for new code", "commit messages follow convention"). These apply unless overridden
- **Repository setup** — Lopen helps the user set up their repository with quality infrastructure (hooks, linters, formatters) as part of the Planning phase

### Category 4: Tool Discipline

Prevent unnecessary tool invocations and shotgun debugging.

- **Active intervention with soft limits** — Lopen monitors tool call patterns per iteration. When it detects waste (e.g., reading the same file repeatedly, running the same failing command without changes), it injects a corrective instruction into the next prompt
- **Soft limits are configurable** — Default thresholds for tool call patterns (e.g., max 3 reads of the same file per iteration) can be adjusted via [Configuration](../configuration/SPECIFICATION.md)
- **No hard blocks** — Lopen warns and corrects but does not hard-block tool calls. The LLM retains the ability to override if it has a legitimate reason

### Global Skills

Lopen defines global skills that encapsulate verification tool calls for the project:

| Skill            | Purpose                                              |
| ---------------- | ---------------------------------------------------- |
| `verify-build`   | Run the project build and check for errors           |
| `verify-tests`   | Run the test suite and report results                |
| `verify-lint`    | Run linters/formatters and check for violations      |
| `verify-commit`  | Ensure VCS hooks pass before committing              |

These skills are always available. Lopen enforces that relevant skills are invoked before marking work complete. Module specs can define additional skills in their [Skills & Hooks](#skills--hooks) section.

### Oracle Verification

At task, component, and module completion boundaries, Lopen **requires** oracle verification via three Lopen-managed tools that the LLM must call before marking work complete:

| Tool | Boundary | Validation Scope |
| --- | --- | --- |
| `verify_task_completion` | Task completion | Task diff, tests exist/pass, code quality hooks pass, task requirements met |
| `verify_component_completion` | Component completion | All task diffs holistically, component-level acceptance criteria, no regressions, integration coherence |
| `verify_module_completion` | Module completion | Full module against all acceptance criteria, full test suite, cross-component integration |

Each tool is implemented as a Lopen-managed tool handler that dispatches a cheap/fast model (e.g., gpt-5-mini, gpt-4o) as a sub-agent. The oracle reviews the evidence (diffs, test results, acceptance criteria) and returns a pass/fail verdict with specific findings. This runs within the primary model's SDK tool-calling loop — **no additional premium request** is consumed (see [LLM § Oracle Verification Tools](../llm/SPECIFICATION.md#oracle-verification-tools)).

**Enforcement**: Back-pressure Category 2 (Progress Integrity) requires that `update_task_status(complete)` is rejected unless preceded by a passing `verify_*_completion` call. The LLM cannot bypass oracle verification.

**Retry within context**: If the oracle reports gaps, its findings are returned to the primary LLM as a tool result. The LLM addresses the gaps and re-invokes the verification tool — all within the same SDK invocation. This loop continues until verification passes, churn limits are hit (see [Back-Pressure § Category 2](#category-2-progress-integrity)), or the LLM gives up.

The oracle model is configurable via [Configuration](../configuration/SPECIFICATION.md).

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

## Git Safety & Rollback

Lopen uses Git as its safety net for destructive changes:

### Auto-Commit per Task

When `git.auto_commit` is enabled (default: `true`), Lopen instructs the LLM to commit after each task completion. Each commit is a rollback point with a conventional commit message.

### Branch per Module

Lopen creates a working branch for each module (e.g., `lopen/auth`, `lopen/storage`). This isolates module work from the main branch. The user merges when satisfied.

### Revert Command

When the LLM makes destructive changes that cannot be self-corrected:

- `lopen revert` rolls back to the last known-good commit (the most recent task-completion commit)
- The session state is updated to reflect the rollback
- The LLM is informed of the rollback in the next iteration context

See [CLI § lopen revert](../cli/SPECIFICATION.md) for command details.

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

**User Scoping**: When multiple modules exist, the user explicitly chooses which module to work on. Within the module, the LLM has full autonomy over component ordering and implementation approach.

**Context Isolation**: Each module has its own specifications, components, and tasks. Modules may reference other modules, for which progressive disclosure is used to load context.

**Cross-Module Dependencies**: The LLM handles cross-module dependencies autonomously. It may create stubs, interfaces, or build dependent components as needed. Lopen does not enforce a build order across modules.

**Progress Tracking**: System tracks completion state of all modules in project.

### Module Selection

When starting work:

1. System scans `docs/requirements/` subfolders for module specifications
2. Lists modules with current state (not started / in progress / complete)
3. User selects module to work on
4. Can switch modules mid-work (session saved per [Storage module](../storage/SPECIFICATION.md))

---

## Ancillary Features

These features support the core workflow but are not part of the 7-step process:

### Research

**Purpose**: Gather information needed for informed implementation decisions.

**When**: Research is **Lopen-initiated** — triggered autonomously during spec analysis and codebase assessment (typically steps 3–4) when the LLM's assessment reveals unknowns. The user does not trigger research manually.

**Triggers**:

- During component identification (step 3), the LLM encounters an unfamiliar technology, pattern, or API
- During codebase assessment, the LLM finds gaps between the spec and existing code that require investigation
- During task execution (step 6), the LLM encounters an implementation problem it cannot resolve from existing context
- Lopen recognizes these via the LLM's tool calls and response patterns (e.g., hedging language, repeated failures on the same problem)

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

**When**: During Planning phase (steps 2–5).

**How**:

- Lopen breaks down components into tasks
- Creates markdown plan with checkboxes
- User can review and adjust before Building begins

---

## Notes

This specification defines the **core workflow and orchestration behavior** of Lopen independent of user interface, LLM backend, storage mechanism, or configuration system.

---

## Acceptance Criteria

- [ ] [CORE-01] Lopen scans `docs/requirements/` and correctly identifies all module specifications
- [ ] [CORE-02] The 7-step workflow executes in order: Draft Spec → Dependencies → Components → Select → Tasks → Iterate → Repeat
- [ ] [CORE-03] Re-entrant assessment correctly determines the current workflow step from actual codebase state, not stale session data
- [ ] [CORE-04] Specification drift detection identifies when spec sections change between iterations and flags the drift to the user
- [ ] [CORE-05] The Requirement Gathering → Planning phase transition requires explicit user confirmation (human gate)
- [ ] [CORE-06] Planning → Building transition proceeds automatically when plan is structurally complete
- [ ] [CORE-07] Building → Complete transition proceeds automatically when all acceptance criteria pass verification
- [ ] [CORE-08] Task hierarchy supports Module → Component → Task → Subtask levels
- [ ] [CORE-09] Task state transitions follow: Pending → In Progress → Complete/Failed
- [ ] [CORE-10] Task completion requires passing oracle verification (`verify_task_completion`) before `update_task_status(complete)` is accepted
- [ ] [CORE-11] Back-pressure Category 1 (Resource Limits): warns at configured warning threshold, pauses at confirmation threshold
- [ ] [CORE-12] Back-pressure Category 2 (Progress Integrity): churn detection escalates after configured failure threshold
- [ ] [CORE-13] Back-pressure Category 2: false completion claims rejected when required tool calls were not made
- [ ] [CORE-14] Back-pressure Category 3 (Quality Gates): acceptance criteria enforced at module and component completion
- [ ] [CORE-15] Back-pressure Category 4 (Tool Discipline): corrective instructions injected when wasteful tool patterns detected
- [ ] [CORE-16] Git auto-commit creates a commit after each task completion when `git.auto_commit` is enabled
- [ ] [CORE-17] Branch per module creates working branches (e.g., `lopen/auth`) for each module
- [ ] [CORE-18] `lopen revert` rolls back to the last task-completion commit and updates session state
- [ ] [CORE-19] Document management extracts relevant sections (not full documents) for LLM context
- [ ] [CORE-20] Programmatic updates (task checkboxes, status tracking) happen without LLM invocation
- [ ] [CORE-21] Single task failures display inline and the LLM self-corrects
- [x] [CORE-22] Repeated task failures (at threshold) prompt user intervention
- [ ] [CORE-23] Critical system errors block execution and require user action
- [ ] [CORE-24] Module selection lists modules with current state and allows user to choose

---

## Dependencies

- **[LLM module](../llm/SPECIFICATION.md)** — SDK invocation, model selection, tool registration, prompt construction, oracle verification dispatch
- **[Storage module](../storage/SPECIFICATION.md)** — Session persistence, plan storage, section caching, document formats
- **[Configuration module](../configuration/SPECIFICATION.md)** — Workflow settings (failure_threshold, max_iterations, unattended), budget settings, git settings
- **Git** — Auto-commit, branch management, revert functionality

---

## Skills & Hooks

- **verify-build**: `dotnet build --no-restore` — Compile check before marking tasks complete
- **verify-tests**: `dotnet test --no-build` — Test suite must pass before marking tasks complete
- **verify-lint**: `dotnet format --verify-no-changes` — Code formatting check
- **verify-commit**: Run all verify-* skills before committing (pre-commit hook)
- **pre-workflow**: Validate that a module specification exists and is parseable before entering workflow

## References

- [LLM Specification](../llm/SPECIFICATION.md) — Copilot SDK integration, model selection, tool strategy, token tracking
- [Storage Specification](../storage/SPECIFICATION.md) — Session persistence, document formats, `.lopen/` structure
- [Configuration Specification](../configuration/SPECIFICATION.md) — User preferences, feature flags, model assignments
- [TUI Specification](../tui/SPECIFICATION.md) — Terminal UI layout, progressive disclosure, visual design
