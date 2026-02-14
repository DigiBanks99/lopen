---
name: storage
description: The storage and persistence requirements of Lopen
---

# Storage Specification

## Overview

Lopen persists session state, documents, and metadata to enable workflow resumption, context management, and progress tracking. This module defines the `.lopen/` directory structure, session persistence, document formats, and caching strategy.

### Design Principles

1. **Project-Local** — All state stored within the project directory under `.lopen/`
2. **Session-Oriented** — Each workflow run has its own session with isolated state
3. **Compact by Default** — Storage format optimized for small context windows, not human readability
4. **Inspectable on Demand** — Compact state can be prettified to human-readable formats when needed
5. **Crash-Safe** — State saved after each significant action so interrupted sessions can resume

---

## Directory Structure

All Lopen state lives under `.lopen/` in the project root:

```sh
.lopen/
├─ sessions/
│  ├─ {session-id}/
│  │  ├─ state.json          # Workflow state (phase, step, task progress)
│  │  ├─ metrics.json         # Token usage, premium request counts
│  │  └─ history/             # Per-iteration snapshots (optional, configurable)
│  └─ latest -> {session-id}  # Symlink to most recent session
├─ modules/
│  └─ {module-name}/
│     └─ plan.md              # Component/task breakdown with checkboxes
├─ cache/
│  ├─ sections/               # Cached document section extractions
│  └─ assessments/            # Cached codebase assessment results
└─ config.json                # Project-level configuration (see Configuration module)
```

### Conventions

- **Session IDs** are short, human-readable identifiers (e.g., timestamp-based: `20260214-1357`)
- **Module names** match the specification directory names under `docs/requirements/`
- The `.lopen/` directory should be added to `.gitignore` — it contains ephemeral session state, not project source

---

## Session Persistence

### What's Persisted

Each session captures:

| Data                    | Location                     | Purpose                                   |
| ----------------------- | ---------------------------- | ----------------------------------------- |
| Workflow state          | `sessions/{id}/state.json`   | Current phase, step, module, component    |
| Task hierarchy & status | `sessions/{id}/state.json`   | Full task tree with completion states     |
| Resource references     | `sessions/{id}/state.json`   | Which specs, research, plans are relevant |
| Token metrics           | `sessions/{id}/metrics.json` | Per-iteration and cumulative token counts |
| Session metadata        | `sessions/{id}/state.json`   | Timestamp, project, models used           |

### What's NOT Persisted in Sessions

- **Conversation history** — Each loop iteration starts fresh; session state *is* the memory (see [LLM § Loop Execution](../llm/SPECIFICATION.md#loop-execution))
- **Full document content** — Documents live in the project (e.g., `docs/requirements/`); sessions only store references
- **LLM outputs** — Not recorded unless explicitly saved as research or plan content

### Save Triggers

State is auto-saved after:

- Step completion (advancing from one workflow step to the next)
- Task completion or failure
- Phase transition
- Component completion
- User-initiated pause/switch

### Storage Format

- **Default**: Compact JSON — optimized for programmatic reading and minimal disk usage
- **Prettify**: On demand, Lopen can render session state as human-readable markdown, JSON, or YAML (e.g., `lopen session show --format md`)
- JSON is chosen over binary formats for debuggability while remaining compact

---

## Session Lifecycle

1. **Create** — New session created when workflow begins; assigned a unique ID
2. **Auto-Save** — State saved after each significant action
3. **Interrupt** — Session preserved if Lopen is closed or crashes
4. **Resume** — On startup, Lopen checks for existing sessions and offers to resume (see [Configuration § Resume Behavior](../configuration/SPECIFICATION.md))
5. **Complete** — Session marked as complete when module workflow finishes; retained for reference
6. **Cleanup** — Old completed sessions can be pruned (configurable retention policy)

### Resume Behavior

- On startup, Lopen checks `.lopen/sessions/latest`
- If an active (incomplete) session exists, offers to resume or start fresh
- Resume loads session state as a **hint** — the [Core Workflow](../core/SPECIFICATION.md#re-entrant-assessment) still assesses the actual codebase
- CLI flags: `--resume {id}`, `--no-resume` (see [Configuration](../configuration/SPECIFICATION.md))

---

## Document Formats

### Specifications

Specifications live in the project source (e.g., `docs/requirements/{module}/SPECIFICATION.md`) and follow a consistent markdown structure:

- YAML frontmatter with `name` and `description`
- H1 title
- H2 sections for major topics
- H3 subsections for details

This structure enables Lopen's section-level extraction (see [Core § Document Management](../core/SPECIFICATION.md#document-management)).

### Research Documents

Research documents are stored alongside specifications in the project source under `docs/requirements/{module}/`:

- Index file: `RESEARCH.md` — links to all research documents for the module
- Per-topic files: `RESEARCH-{topic}.md` (e.g., `RESEARCH-jwt-best-practices.md`)
- Standard structure: Summary, Findings, Sources, Relevance
- Linked to specification sections via metadata
- Research documents live in source (not `.lopen/`) because they are valuable project artifacts that should be version-controlled

### Plans

Plans are stored under `.lopen/modules/{module}/plan.md`:

- Markdown with checkbox lists for tasks
- Hierarchical structure matching the task hierarchy (see [Core § Task Management](../core/SPECIFICATION.md#task-management))
- Updated programmatically by Lopen (not by the LLM) when tasks complete

---

## Caching

### Section Cache

- When Lopen extracts a section from a document, the parsed result is cached under `.lopen/cache/sections/`
- Cache is keyed by file path + section header + file modification timestamp
- Cache is invalidated when the source file changes

### Assessment Cache

- Codebase assessment results (from re-entrant assessment) can be cached to avoid redundant LLM calls
- Cache is short-lived (invalidated on any file change in the assessed scope)
- This is an optimization — correctness is never sacrificed for caching

---

## Notes

This specification defines **where and how Lopen stores its state**. It does not define what state is tracked (that's the [Core Workflow](../core/SPECIFICATION.md)) or how state is displayed (that's the [TUI](../tui/SPECIFICATION.md)).

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow state, task hierarchy, document management
- [LLM Specification](../llm/SPECIFICATION.md) — Token metrics, loop execution, context management
- [Configuration Specification](../configuration/SPECIFICATION.md) — Storage-related settings (retention, format preferences)
- [TUI Specification](../tui/SPECIFICATION.md) — How session state and progress are displayed
