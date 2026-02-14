---
name: core
description: The core requirements of Lopen
---

# Core Lopen Specification

## Overview

Lopen is an intelligent coding assistant and agent team orchestrator that autonomously builds software modules from specifications. It is designed to be opinionated, efficient with token usage and premium API requests, and focused on providing users with clear visibility into progress without manual correlation of specifications, research, plans, and implementation.

### Design Principles

1. **Opinionated** - Makes sensible default decisions to avoid over-engineering generic solutions
2. **Token Efficient** - Minimizes context window usage through intelligent document management
3. **Cost Aware** - Tracks and displays token usage and premium API requests prominently
4. **Autonomous** - Self-corrects failures and advances through workflow automatically
5. **User Informed** - Correlates specifications, research, plans, and tasks automatically
6. **Structured** - Follows a consistent 7-step process for module development

---

## Core Development Workflow

Lopen guides users through a structured 7-step process for building modules:

### The 7 Steps

1. **Draft Specification** - User provides initial idea, Lopen conducts guided conversation to gather requirements
2. **Determine Dependencies** - Identify what's needed to make the specification work
3. **Identify Components** - Break down the module into logical components
4. **Select Next Component** - Choose the next most important component to build
5. **Break Into Tasks** - Decompose the component into achievable atomic tasks
6. **Iterate Through Tasks** - Execute tasks, self-correct on failures, track progress
7. **Repeat** - Return to step 4 until all components are complete

### The 3 Phases

These steps are organized into three phases:

- **Requirement Gathering** (Step 1) - Define what needs to be built
- **Planning** (Steps 2-3) - Understand dependencies and architecture
- **Building** (Steps 4-7) - Iteratively construct components

### Workflow Characteristics

**Automatic Progression**: Lopen automatically advances through steps when completion criteria are met. No manual step transitions required.

**Semi-Automatic Reviews**: At phase boundaries, Lopen may offer reviews (e.g., spec review after step 1). User confirms to proceed and lopen facilitates the review process.

**Component-by-Component**: In the Building phase, Lopen completes one component fully before moving to the next. User selects which component to tackle first.

**Task-Level Iteration**: Within a component, tasks are completed sequentially with self-correction on failures.

---

## Document Management

### Intelligent Resource Tracking

To minimize token usage and LLM overhead, Lopen includes an intelligent document management layer that operates in-process:

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
- LLM given clear guidance on where excerpts are stored
- Lopen can give LLM tracking information
- Significantly reduces context window usage
- Example: When working on authentication, only `§ Authentication` sections provided

**Programmatic Updates**:

- Task completion updates plan document checkboxes without LLM
- Component tracking of specification implementation in Lopen and not left to the LLM
- Reduces need for LLM to manage document state

**Resource Correlation**:

- System automatically correlates specifications to components to tasks
- User sees relevant spec sections, research notes, and plan items for current task
- No manual hunting through documents required

---

## Task Management

### Task Hierarchy

Tasks are organized hierarchically:

```sh
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

- **○ Pending** - Not started
- **▶ In Progress** - Currently being worked on
- **✓ Complete** - Successfully completed
- **✗ Failed** - Failed and blocked (rare - usually self-corrects)

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

**Unattended Mode**: For fully autonomous operation, intervention prompts can be suppressed with `--unattended` flag.

### Failure Types

| Type                       | Behavior                                 | User Action              |
| -------------------------- | ---------------------------------------- | ------------------------ |
| Single task failure        | Show inline, LLM self-corrects, continue | None - observe           |
| Repeated task failure (3+) | Prompt user to confirm continuation      | Confirm or intervene     |
| Critical system error      | Block and require user action            | Recovery action required |
| Minor warning              | Show inline, continue                    | None - informational     |

---

## Token & Cost Management

### Core Metrics

Lopen treats token usage and premium API requests as **first-class metrics** visible throughout the application:

**Context Window Usage**: `{used}/{total}` tokens displayed prominently (e.g., `2.4K/128K`)

**Premium Request Counter**: Number of premium API calls made in current session (e.g., `🔥 23`)

These metrics are **not Lopen-specific** - they apply to any workflow or module using the application.

### Token Efficiency Strategies

1. **Document Management Layer**: Section-level extraction reduces context sent to LLM
2. **Programmatic Updates**: Routine document updates (checkboxes, status) handled in code
3. **Progressive Context**: Only current task context loaded, not entire module history. Related docs are loaded as needed
4. **Smart Caching**: Previously analyzed sections cached when possible
5. **Minimal Prompts**: Structured prompts avoid unnecessary verbosity
6. **Context Allocation**: Wherever possible, a new context window should be used and context to be loaded from Lopen memory or the file system

---

## Session Management

### Session Persistence

Lopen automatically saves session state to enable resumption after interruption:

**What's Persisted**:

- Current phase and step in workflow
- Module hierarchy with completion state
- Component and task lists with progress
- Active resource references (specs, research, plans)
- Conversation history (configurable retention length)
- Session metadata (timestamp, project, model used)

**Storage Location**: Project-local directory (e.g., `.lopen/sessions/{session-id}.json`)

**Storage Format**: Anything that is ideal for small context windows. Not necessarily human readable.

**Storage Interact**: For Human flows, minimised storage files can be prettified to markdown, JSON, YAML or something similarly human readable.

**Resume Behavior**:

- On startup, Lopen checks for existing sessions
- If found, offers to resume or start fresh
- User can resume specific session by ID: `lopen --resume {id}`
- Or force new session: `lopen --no-resume`

### Session Lifecycle

1. **Start**: Session created when workflow begins
2. **Auto-Save**: State saved after each significant action (step complete, task done)
3. **Interrupt**: Session preserved if Lopen closed or crashed
4. **Resume**: User prompted to continue from last checkpoint
5. **Complete**: Session archived when module fully built

---

## Multi-Module Projects

### Module Execution Model

**One Module at a Time**: Lopen completes the full 7-step workflow for one module before starting another.

**User Selection**: When multiple modules exist, user explicitly chooses which to work on next.

**Context Isolation**: Each module has its own specifications, components, and tasks. Modules my reference other modules for which progressive disclosure is used to load context.

**Progress Tracking**: System tracks completion state of all modules in project.

### Module Selection

When starting work:

1. System scans project for module specifications
2. Lists modules with current state (not started / in progress / complete)
3. User selects module to work on
4. Can switch modules mid-work (session saved)

---

## Ancillary Features

These features support the core workflow but are not part of the 7-step process:

### Research

**Purpose**: Gather information needed for informed implementation decisions.

**When**: Research can occur during any phase, triggered by knowledge gaps.

**How**:

- Lopen identifies need for research (e.g., "best practices for JWT tokens")
- Conducts research using available tools
- Creates research document with findings
- Links research to relevant specification sections

**Display**: Research activities shown inline during workflow (see TUI spec for UI details).

**Summaries**: Research summaries provided at phase transitions.

### Specification Review

**Purpose**: Validate that drafted specification is complete and correct before building.

**When**: Offered after Requirement Gathering (step 1), before Planning phase.

**How**:

- Lopen analyzes draft specification for gaps, ambiguities, conflicts
- Presents findings to user
- User decides to revise spec, proceed as-is, or cancel

**Trigger**: Semi-automatic - Lopen offers review, user confirms.

### Plan Refinement

**Purpose**: Ensure implementation plan is achievable and well-structured.

**When**: During Planning phase (steps 2-3).

**How**:

- Lopen breaks down components into tasks
- Creates markdown plan with checkboxes
- User can review and adjust before Building begins

## Notes

This specification defines the **core behavior** of Lopen independent of user interface.

## References

- [TUI Specification](../tui/SPECIFICATION.md) - For UI-specific requirements (split-screen layout, progressive disclosure, visual design).
