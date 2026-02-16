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

- **Session IDs** follow the format `{module}-YYYYMMDD-{counter}` (e.g., `auth-20260214-1`, `auth-20260214-2`). The counter is a monotonically increasing integer per module per day, preventing collisions.
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
7. **Delete** — Individual sessions can be explicitly deleted via CLI

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

## Error Handling

### Corrupted State

If session state files (`.lopen/sessions/`, `.lopen/modules/`) are unreadable, malformed, or corrupted:

1. Lopen logs a warning with the specific file and error
2. The corrupted session is marked as unusable and excluded from resume options
3. The user is informed and offered to start a fresh session
4. Lopen does **not** attempt to repair corrupted state — it starts clean
5. Corrupted files are moved to `.lopen/corrupted/` for manual inspection, not deleted

### Disk Full / Write Failure

If Lopen cannot write to `.lopen/` (disk full, permissions, etc.):

1. This is a **critical system error** per [Core § Failure Handling](../core/SPECIFICATION.md#failure-handling--self-correction)
2. Lopen surfaces the error immediately with the specific path and OS error
3. The workflow pauses — session state integrity cannot be guaranteed without persistence
4. The user must resolve the disk/permission issue before resuming

### Cache Corruption

If cache files (`.lopen/cache/`) are corrupted:

1. Lopen silently invalidates and regenerates the affected cache entries
2. This is a recoverable situation — cache is always re-derivable from source documents
3. No user notification unless regeneration fails repeatedly

---

## Notes

This specification defines **where and how Lopen stores its state**. It does not define what state is tracked (that's the [Core Workflow](../core/SPECIFICATION.md)) or how state is displayed (that's the [TUI](../tui/SPECIFICATION.md)).

---

## Acceptance Criteria

- [ ] `.lopen/` directory is created in project root on first workflow run
- [ ] Session state (`state.json`) persists workflow phase, step, module, component, and task hierarchy
- [ ] Session metrics (`metrics.json`) persists per-iteration and cumulative token counts and premium request counts
- [ ] Session IDs follow the `{module}-YYYYMMDD-{counter}` format with no collisions
- [ ] `latest` symlink points to the most recent session directory
- [ ] State is auto-saved after: step completion, task completion/failure, phase transition, component completion, user pause/switch
- [ ] Session resume loads state from `.lopen/sessions/latest` and offers resume or start fresh
- [ ] `--resume {id}` resumes a specific session; `--no-resume` starts fresh
- [ ] Plans are stored at `.lopen/modules/{module}/plan.md` with checkbox task hierarchy
- [ ] Plan checkboxes are updated programmatically by Lopen, not by the LLM
- [ ] Section cache (`.lopen/cache/sections/`) is keyed by file path + section header + modification timestamp
- [ ] Section cache is invalidated when the source file changes
- [ ] Assessment cache is short-lived and invalidated on any file change in the assessed scope
- [ ] Corrupted session state is detected, warned, and the session is excluded from resume options
- [ ] Corrupted files are moved to `.lopen/corrupted/` for manual inspection
- [ ] Disk full / write failure is treated as a critical system error and pauses the workflow
- [ ] Corrupted cache entries are silently invalidated and regenerated
- [ ] Completed sessions are retained up to the configured `session_retention` limit, then pruned
- [ ] Individual sessions can be deleted via `DeleteSessionAsync`, removing the session directory and all files
- [ ] Research documents are stored at `docs/requirements/{module}/RESEARCH-{topic}.md` (in source, not `.lopen/`)
- [ ] Storage format is compact JSON by default, with on-demand prettification via `lopen session show --format`

---

## Dependencies

- **[Core module](../core/SPECIFICATION.md)** — Defines workflow state, task hierarchy, and document management that Storage persists
- **[Configuration module](../configuration/SPECIFICATION.md)** — Session retention, save_iteration_history, auto_resume settings
- **File system** — Local file I/O for `.lopen/` directory and project source documents

---

## Skills & Hooks

- **verify-state-integrity**: Validate that session state files are well-formed JSON before loading
- **pre-workflow**: Ensure `.lopen/` directory exists and is writable before entering workflow

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow state, task hierarchy, document management
- [LLM Specification](../llm/SPECIFICATION.md) — Token metrics, loop execution, context management
- [Configuration Specification](../configuration/SPECIFICATION.md) — Storage-related settings (retention, format preferences)
- [TUI Specification](../tui/SPECIFICATION.md) — How session state and progress are displayed
