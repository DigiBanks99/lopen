---
name: tui
description: The TUI requirements of Lopen
---

# Terminal UI Specification

> A coding agent harness TUI that fills the terminal, providing a welcoming developer experience.

## Overview

Lopen is a terminal application (like neovim) that serves as an intelligent coding assistant and agent team orchestrator. The TUI provides a split-screen interface that balances agent activity with contextual awareness, supporting a structured workflow for autonomous module development.

> This document focuses on UI-specific requirements.

### TUI Design Goals

- **Balanced Layout**: Split-screen showing both activity and context simultaneously
- **Progressive Disclosure**: Show current work fully, collapse previous work to summaries
- **Persistent Context**: Always show task progress, hierarchy, and resources
- **Token Visibility**: Display context usage and premium requests prominently
- **Minimal Clutter**: Clean visual design with colorful status indicators, minimal content styling

---

## Core Workflow Integration

The TUI visualizes and enables interaction with Lopen's 7-step workflow (see [Core Specification § Core Development Workflow](../core/SPECIFICATION.md#core-development-workflow)):

**Phase Visualization**:

- Top panel shows current phase: Requirement Gathering / Planning / Building
- Step progress indicator (e.g., ●●●○○○○ Step 3/7)

**Automatic Progression**:

- UI updates automatically as workflow advances through steps
- No manual step transitions required from user

**Phase Transitions**:

- Semi-automatic reviews offered at boundaries (e.g., spec review after step 1)
- Research summaries displayed when transitioning phases

---

## Logo

ASCII art logo:

```sh
╻  ┏━┓┏━┓┏━╸┏┓╻
┃  ┃ ┃┣━┛┣╸ ┃┗┫
┗━╸┗━┛╹  ┗━╸╹ ╹
```

---

## Layout Structure

The TUI uses a split-screen layout to balance agent activity with contextual awareness:

```sh
┌─────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  ╻  ┏━┓┏━┓┏━╸┏┓╻  v1.0.0  │ claude-sonnet  │ Context: 2.4K/128K (🔥 23 premium)  │  main  │  🟢   │
│  ┃  ┃ ┃┣━┛┣╸ ┃┗┫                                                                                    │
│  ┗━╸┗━┛╹  ┗━╸╹ ╹                          Phase: Building ●●●○○○○ Step 6/7: Iterate Tasks          │
├──────────────────────────────────────────────┬──────────────────────────────────────────────────────┤
│ MAIN ACTIVITY AREA (scrollable)             │ CONTEXT PANEL                                        │
│                                              │                                                      │
│ Agent: Implementing JWT token validation     │ ▶ Current Task: Implement JWT token validation      │
│                                              │   Progress: 60% (3/5 subtasks done)                 │
│ ● Edit src/auth.ts (+45 -12)                │   ├─✓ Parse token from header                       │
│   + Added validateToken function            │   ├─✓ Verify signature with secret                  │
│   + Imported JWT library                    │   ├─▶ Check expiration (current)                    │
│   [click to see full diff]                  │   ├─○ Validate custom claims                        │
│                                              │   └─○ Handle edge cases & errors                    │
│ ● Run tests                                  │                                                      │
│   $ npm test src/auth.test.ts               │ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│   ✓ All 12 tests passing                    │                                                      │
│   [view output]                             │ 📊 Component: auth-module                            │
│                                              │    Tasks: 3/5 complete                               │
│ Moving to expiration validation...           │    ├─✓ Setup JWT library                            │
│                                              │    ├─✓ Create token generator                       │
│                                              │    ├─▶ Token validation (60% - current)             │
│                                              │    ├─○ Refresh token logic                          │
│                                              │    └─○ Integration tests                            │
│                                              │                                                      │
│ [Previous actions collapsed - scroll up]     │ 📦 Module: authentication                           │
│                                              │    Components: 1/3 in progress                      │
│                                              │    ├─▶ auth-module (current)                        │
│                                              │    ├─○ session-module                               │
│                                              │    └─○ permission-module                            │
│                                              │                                                      │
│                                              │ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
│                                              │                                                      │
│                                              │ 📚 Active Resources:                                 │
│                                              │ [1] SPECIFICATION.md § Authentication                │
│                                              │ [2] research/jwt-best-practices.md                   │
│                                              │ [3] plan.md § Security & Token Handling              │
│                                              │                                                      │
│                                              │ Press 1-9 to view • Auto-tracked & managed          │
├──────────────────────────────────────────────┴──────────────────────────────────────────────────────┤
│ > Your prompt here (or let Lopen continue working)...                                              │
│                                                                                                     │
│ Enter: Submit │ Alt+Enter: New line │ 1-9: View resource │ Tab: Focus panel │ Ctrl+P: Pause      │
└─────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### Top Panel

Always visible (suppressible with `--quiet` or `--no-logo`). Contains:

| Element           | Position      | Description                                      |
| ----------------- | ------------- | ------------------------------------------------ |
| ASCII Logo        | Left          | Lopen branding                                   |
| Version           | Right of logo | `v{Major}.{Minor}.{Patch}`                       |
| Current Model     | Center        | Active AI model name                             |
| Context Usage     | Center-right  | `{used}/{total}` tokens                          |
| Premium Requests  | Center-right  | Count of premium API calls (🔥 indicator)         |
| Git Branch        | Right         | Current branch if in repo                        |
| Auth Status       | Right         | 🟢 authenticated / 🔴 expired                      |
| Current Phase     | Bottom        | Requirement Gathering / Planning / Building      |
| Current Step      | Bottom        | Step progress indicator (e.g., ●●●○○○○ Step 3/7) |
| Working Directory | (Omitted)     | Shown in context panel instead                   |

**Note on Metrics**: Context window usage and premium request count are core UI elements, visible across all workflows (not just Lopen-specific). These metrics help users manage costs and stay aware of resource consumption.

### Main Activity Area (Left Pane)

Scrollable area displaying agent activity:

- Agent narrative (what it's currently doing)
- Tool call outputs (file edits, command results, tests)
- Progressive disclosure: current action shown fully, previous actions collapsed
- Expandable sections for detailed output (diffs, test results, errors)
- Research activities shown inline
- Code blocks with syntax highlighting
- Minimum 50%, max 80% width depending on screen size

**Interaction**:

- Click or press shortcut keys to expand collapsed sections
- Scroll to view history
- Auto-scrolls to show current activity

### Context Panel (Right Pane)

Maximum 50%, min 20% width depending on screen size

Persistent view of work context:

**Current Task Section**:

- Task name and progress percentage
- Subtask breakdown with completion status using [task states](../core/SPECIFICATION.md#task-states)
- Real-time updates as work progresses

**Component & Module Hierarchy**:

- Hierarchical tree of components with completion state
- Current component highlighted
- Module-level progress summary

**Active Resources**:

- Numbered list of relevant documents (specs, research, plans)
- Quick access via number keys (1-9)
- Auto-tracked by intelligent document management layer
- Shows relevant sections (e.g., § Authentication)

### Prompt Area

Fixed at bottom with clear border separation:

- Multi-line text input
- **Enter**: Submit prompt
- **Alt+Enter**: Insert newline
- **Ctrl+P**: Pause agent execution
- **Ctrl+C**: Cancel current operation
- Context-aware hints showing available commands and shortcuts

---

## Session Management

### UI for Session Resumption

See [Core Specification § Session Management](../core/SPECIFICATION.md#session-management) for session persistence details.

The TUI displays a resume prompt on startup when previous session detected:

**Resume Session Modal**:

```sh
 Resume Session? ───────────────────────────────────────────╮
                                                              │
  Previous session found for: authentication module          │
                                                              │
  Phase: Building (Step 6/7)                                 │
  Progress: 60% (3/5 tasks complete in auth-module)          │
  Last activity: 2 hours ago                                 │
                                                              │
  [Resume]  [Start New]  [View Details]                      │
                                                              │

```

**Interaction**:

- Arrow keys to navigate options
- Enter to confirm selection
- Shows key session details to help user decide
- `[View Details]` expands to show full session state

---

## Landing Page

On startup (when no session to resume), display a modal overlay before entering the workspace:

```sh
'EOF'
                                                                             │
                            ╻  ┏━┓┏━┓┏━╸┏┓╻                                  │
                            ┃  ┃ ┃┣━┛┣╸ ┃┗┫                                  │
                            ┗━╸┗━┛╹  ┗━╸╹ ╹                                  │
                                                                             │
                             v1.0.0-alpha                                    │
                         Interactive Agent Loop                              │
                                                                             │

  Quick Commands                                                             │
                                                                             │
    /help          Show available commands                                   │
    /plan          Start planning mode                                       │
    /build         Start build mode                                          │
    Ctrl+P         Switch to plan mode (workspace: pause agent)              │
    Ctrl+B         Switch to build mode                                      │
                                                                             │

  Press any key to continue...                              🟢 Authenticated │

```

### Behavior

- Modal dismisses on any keypress
- Quick commands section is **configurable from code** per workspace context
- Auth state shown at bottom row
- After dismissal, transitions seamlessly to the main workspace
- Skippable with `--no-welcome` flag

---

## Workflow Phases & Step Transitions

### UI Visualization of Workflow

See [Core Specification § Core Development Workflow](../core/SPECIFICATION.md#core-development-workflow) for complete workflow details.

The TUI displays phase and step progression in the **Top Panel**:

**Phase Indicator**: Shows current phase name

- "Phase: Requirement Gathering" (Step 1)
- "Phase: Planning" (Steps 2-3)  
- "Phase: Building" (Steps 4-7)

**Step Progress**: Visual progress indicator

- Example: `●●●○○○○ Step 3/7: Identify Components`
- Filled circles (●) = completed steps
- Empty circles (○) = remaining steps
- Current step shown with label

**Phase Transition Summaries**: Displayed in main activity area when transitioning between phases. Collapsible sections showing:

- Research findings
- Component breakdown
- Dependency analysis
- Test results

---

## UI Components

### Tool Call Display

Display tool calls with progressive disclosure:

```sh
▼ Edit src/auth.ts (+45 -12)
   + Added validateToken function
   + Imported JWT library
   [click to see full diff]

▼ Run tests
   $ npm test src/auth.test.ts
   ✓ All 12 tests passing
   [view output]

▲ Research JWT best practices                     [expanded]
   Finding: Use RS256 for production...
   Finding: Token expiration should be configurable...
   [See full research document]
```

**Progressive Disclosure**:

- **Current action**: Fully expanded with details visible
- **Previous actions**: Collapsed to summary line, expandable on click
- **Importance-based**: Errors and warnings auto-expand
- Bullet prefix (●) for each tool call
- File operations show diff stats `(+N -M)`
- Click `[labels]` or press shortcut to expand/view full content

**Tool Call Types**:

- File edits (diff stats, expandable diff view)
- Command execution (exit status, expandable output)
- Research activities (inline with findings, links to full docs)
- Tests (pass/fail status, expandable results)
- Agent reasoning (collapsed by default, expandable)

### Confirmation Modals

For actions requiring user confirmation:

```sh
 Confirm Action ────────────────────────────────────────────╮
                                                             │
  Apply changes to 3 files?                                  │
                                                             │
  ● src/main.ts (+45 -12)                                    │
  ● src/utils.ts (+8 -3)                                     │
  ● README.md (+5 -0)                                        │
                                                             │
  [Yes]  [No]  [Always]  [Other...]                          │
                                                             │

```

**Options:**

- **Yes** - Proceed once
- **No** - Cancel
- **Always** - Remember choice for session
- **Other** - Opens text field for explanation/alternative instruction

**Dangerous actions** (delete, overwrite) require selecting from option list with explanation field.

### Progress & Loading

Use Spectre.Console spinners for async operations:

```sh
 Analyzing dependencies...
 Breaking component into tasks...
 Running tests...
```

Display spinner with status text, then render complete response when finished (no character-by-character streaming).

**Task Progress**:

- Real-time percentage and subtask completion in context panel
- Visual progress bar for long-running operations
- Clear indication of current vs completed vs pending work

### Error Display

| Severity | Display                                        |
| -------- | ---------------------------------------------- |
| Critical | Modal dialog with details and recovery options |
| Minor    | Inline message in workspace with suggested fix |

```sh
 Error ─────────────────────────────────────────────────────╮
                                                             │
  ✗ Authentication expired                                   │
                                                             │
  Your session has expired. Please re-authenticate.          │
                                                             │
  💡 Run: lopen auth login                                   │
                                                             │
  [Retry]  [Cancel]                                          │
                                                             │

```

### Diff Viewer

Display file changes with clear visual diff (inspired by VS Code and nvimdiff):

```sh
 src/main.ts ───────────────────────────────────────────────╮
  10   │     const config = loadConfig();                    │
  11 - │     console.log("Starting...");                     │
  11 + │     logger.info("Starting application");            │
  12   │     await initialize();                             │

```

- Line numbers with `-` (removed) and `+` (added) markers
- Syntax highlighting preserved
- Context lines around changes

### File Picker

Use Spectre.Console tree/selection components for file browsing:

```sh
 Select File ───────────────────────────────────────────────╮
  📁 src/                                                    │
    📄 main.ts                                               │
  ▸ 📄 utils.ts                                              │
    📄 config.ts                                             │
  📁 tests/                                                  │
    📄 main.test.ts                                          │

```

Support formats that agents can read (text files, code, markdown, JSON, etc.).

---

## Visual Design

### Color Palette

Use **semantic colors** that work with terminal themes (Ghostty, Windows Terminal, iTerm2):

| Semantic              | Usage                               |
| --------------------- | ----------------------------------- |
| Success (green)       | Completed operations, confirmations |
| Error (red)           | Failures, critical issues           |
| Warning (yellow)      | Cautions, non-blocking issues       |
| Info (blue)           | Informational messages              |
| Muted (gray)          | Secondary text, timestamps          |
| Accent (cyan/magenta) | Highlights, selections              |

Rely on terminal's color scheme for actual RGB values. Support `NO_COLOR` environment variable.

### Symbols

| Symbol | Fallback | Usage            |
| ------ | -------- | ---------------- |
| ●      | *        | Tool call bullet |
| ✓      | [OK]     | Success          |
| ✗      | [X]      | Error            |
| ⚠      | [!]      | Warning          |
| 💡      | [i]      | Tip/suggestion   |
| 🟢      | [OK]     | Status good      |
| 🔴      | [!!]     | Status bad       |

### Borders & Panels

- Use box-drawing characters for clear visual separation
- Rounded corners preferred: `╭ ╮ ╰ ╯`
- Consistent panel styling throughout

---

## Terminal Support

### Requirements

- Fills available terminal size (no minimum enforced)
- Adapts layout responsively to terminal dimensions
- Supports modern terminals: Ghostty, Windows Terminal, iTerm2, Alacritty

### Capabilities Detection

- TrueColor (24-bit) preferred, fallback to 256 → 16 colors
- Unicode/emoji support with ASCII fallbacks
- Mouse support optional (keyboard-first design)

---

## Document Management

### UI for Resource Display

See [Core Specification § Document Management](../core/SPECIFICATION.md#document-management) for the intelligent resource tracking system.

The TUI displays resources in the **Context Panel** (right pane):

**Active Resources Section**:

- Numbered list of relevant documents (e.g., `[1] SPECIFICATION.md § Authentication`)
- Shows specific section references (e.g., `§ Authentication`)
- Quick access via number keys (press 1-9 to view)
- Auto-tracked indication: "Auto-tracked & managed"

**Visual Presentation**:

```sh
📚 Active Resources:
[1] SPECIFICATION.md § Authentication
[2] research/jwt-best-practices.md
[3] plan.md § Security & Token Handling

Press 1-9 to view • Auto-tracked & managed
```

**Resource Viewer**:

- Pressing a number key opens resource in modal or split view
- Shows relevant section with context
- Syntax highlighting for code
- Can navigate to full document if needed

---

## Multi-Module Projects

### UI for Module Selection

See [Core Specification § Multi-Module Projects](../core/SPECIFICATION.md#multi-module-projects) for module execution model.

The TUI provides a module selection interface when multiple modules exist:

**Module Selection Modal**:

```sh
 Select Module ─────────────────────────────────────────────╮
                                                              │
  Which module should we work on?                            │
                                                              │
  ▶ authentication (in progress - 60% complete)              │
    logging (not started)                                    │
    error-handling (not started)                             │
    api-gateway (not started)                                │
                                                              │
  [Select]  [View Details]                                   │
                                                              │

```

---

## Input Experiences

### Requirement Gathering (Step 1)

**Initial Ideation**:

- User starts with multi-line prompt describing their idea/module
- Can be brief or detailed
- Submit when ready

**Guided Conversation**:

- Lopen analyzes the initial idea
- Conducts structured interview to gather:
  - Detailed requirements
  - Constraints and dependencies
  - Success criteria
  - Edge cases and error scenarios
- Iterative Q&A until spec is complete

**Spec Drafting**:

- Lopen drafts specification based on conversation
- Shows spec in activity area (expandable/reviewable)
- Offers review before proceeding to Planning phase

### Other Input-Heavy Scenarios

**Module selection**: Visual picker with arrow keys
**Component selection**: Tree view with descriptions
**Confirmation prompts**: Clear options with keyboard shortcuts
**Resource viewing**: Numbered access (press 1-9) or interactive file browser

---

## CLI Flags

| Flag            | Effect                                               |
| --------------- | ---------------------------------------------------- |
| `--quiet`, `-q` | Suppress logo and non-essential output               |
| `--no-logo`     | Hide ASCII logo in top panel                         |
| `--no-color`    | Disable colors (also respects `NO_COLOR` env)        |
| `--no-welcome`  | Skip landing page modal, go straight to workspace    |
| `--unattended`  | Suppress failure confirmations, full autonomous mode |
| `--resume [ID]` | Resume specific session by ID (skip resume prompt)   |
| `--no-resume`   | Ignore previous session, start fresh                 |

---

## Interactive Component Gallery (`lopen test tui`)

### Purpose

Every TUI component must be presentable and interactively testable as a stub — without real backing functionality. The command `lopen test tui` launches a component gallery that lets developers and users browse, view, and interact with each UI component in isolation using mock data.

This is a **runtime feature**, not a replacement for the standard 3-tier testing requirements. All components must still have unit, integration, and end-to-end tests.

### Behavior

**Launch**: `lopen test tui` starts the gallery in the current terminal.

**Gallery View**: Displays a selectable list of all TUI components:

```sh
╭─ Lopen TUI Component Gallery ────────────────────────────────────╮
│                                                                   │
│  Select a component to preview:                                   │
│                                                                   │
│  ▶ Top Panel                                                      │
│    Main Activity Area                                             │
│    Context Panel                                                  │
│    Prompt Area                                                    │
│    Landing Page                                                   │
│    Session Resume Modal                                           │
│    Confirmation Modal                                             │
│    Error Display                                                  │
│    Diff Viewer                                                    │
│    File Picker                                                    │
│    Module Selection Modal                                         │
│    Progress & Spinners                                            │
│    Tool Call Display                                              │
│                                                                   │
│  ↑/↓: Navigate │ Enter: Preview │ q: Quit                        │
╰───────────────────────────────────────────────────────────────────╯
```

**Component Preview**: Selecting a component renders it with realistic mock data. The component is fully interactive — keyboard shortcuts, expand/collapse, scrolling, and all other interactions work as they would in the real application.

**Stub Data**: Each component defines its own stub/mock data set that exercises its visual states (empty, populated, error, loading, etc.). No real LLM calls, file system operations, or network requests are made.

**Navigation**: Press `Esc` or `q` to return to the gallery list from a component preview.

### Architectural Requirement

Every TUI component must be designed so that it can be rendered with injected stub data and no live dependencies. This means:

- Components accept data/state as input (not fetched internally)
- All external dependencies (LLM, file system, network) are behind interfaces that can be stubbed
- Each component registers itself with the gallery so new components are automatically listed

---

## Requirements Checklist

**Note**: For core workflow, task management, and document management requirements, see [Core Specification § Requirements Checklist](../core/SPECIFICATION.md#requirements-checklist). This checklist focuses on UI-specific requirements.

### Layout & Structure

| ID      | Requirement                                                        | Priority |
| ------- | ------------------------------------------------------------------ | -------- |
| TUI-001 | Split-screen layout (activity left, context right, 50/50 to 80/20) | High     |
| TUI-002 | Top panel with logo, version, model, context, premium requests     | High     |
| TUI-003 | Context panel: current task, task tree, resources                  | High     |
| TUI-004 | Main activity area with progressive disclosure                     | High     |
| TUI-005 | Multi-line prompt area with keyboard hints                         | High     |
| TUI-006 | Landing page modal with quick commands                             | Medium   |
| TUI-007 | Session resume modal on restart                                    | High     |

### Display & Interaction

| ID      | Requirement                                                  | Priority |
| ------- | ------------------------------------------------------------ | -------- |
| TUI-201 | Progressive disclosure: current expanded, previous collapsed | High     |
| TUI-202 | Expandable tool call outputs (click or key to expand)        | High     |
| TUI-203 | Real-time task progress updates in context panel             | High     |
| TUI-204 | Hierarchical task tree with status indicators (✓/▶/○)        | High     |
| TUI-205 | Numbered resource access (press 1-9 to view)                 | Medium   |
| TUI-206 | Inline research display with drill-into                      | Medium   |
| TUI-207 | Phase transition summaries in activity area                  | Medium   |
| TUI-208 | Diff viewer with syntax highlighting                         | Medium   |
| TUI-209 | File picker with tree view                                   | Low      |
| TUI-210 | Phase/step visualization in top panel                        | High     |
| TUI-211 | Module selection modal UI                                    | Medium   |
| TUI-212 | Component selection UI                                       | High     |

### User Interaction Patterns

| ID      | Requirement                                      | Priority |
| ------- | ------------------------------------------------ | -------- |
| TUI-301 | Multi-line prompt with Alt+Enter for newlines    | High     |
| TUI-302 | Keyboard shortcuts (Tab, Ctrl+P, number keys)    | High     |
| TUI-303 | Guided conversation UI for requirement gathering | High     |
| TUI-304 | Confirmation modals with Yes/No/Always/Other     | High     |
| TUI-305 | Expandable sections (click/key to expand)        | High     |

### Feedback & Status

| ID      | Requirement                                        | Priority |
| ------- | -------------------------------------------------- | -------- |
| TUI-401 | Task failure display (inline, auto-expanded)       | High     |
| TUI-402 | Repeated failure confirmation modal                | High     |
| TUI-403 | Critical error modal with recovery options         | High     |
| TUI-404 | Spinner-based async feedback                       | Medium   |
| TUI-405 | Context window usage display in top panel          | High     |
| TUI-406 | Premium request counter in top panel (🔥 indicator) | High     |
| TUI-407 | Real-time progress percentages                     | Medium   |

### Visual Design

| ID      | Requirement                                | Priority |
| ------- | ------------------------------------------ | -------- |
| TUI-501 | Semantic color palette (balanced approach) | Medium   |
| TUI-502 | Unicode symbols with ASCII fallbacks       | Medium   |
| TUI-503 | Box-drawing characters for borders         | Low      |
| TUI-504 | Syntax highlighting in code blocks         | Medium   |
| TUI-505 | Consistent panel styling throughout        | Medium   |

### CLI Flags & Configuration

| ID      | Requirement                                          | Priority |
| ------- | ---------------------------------------------------- | -------- |
| TUI-601 | `--quiet`, `--no-logo` flags                         | Low      |
| TUI-602 | `--no-color` flag (respect `NO_COLOR` env)           | Medium   |
| TUI-603 | `--no-welcome` flag                                  | Low      |
| TUI-604 | `--unattended` flag (suppress failure confirmations) | Medium   |
| TUI-605 | `--resume [ID]`, `--no-resume` flags                 | Medium   |

### Component Gallery

| ID      | Requirement                                                            | Priority |
| ------- | ---------------------------------------------------------------------- | -------- |
| TUI-701 | `lopen test tui` launches interactive component gallery                | High     |
| TUI-702 | Gallery lists all TUI components with selection navigation             | High     |
| TUI-703 | Each component renders with realistic mock/stub data                   | High     |
| TUI-704 | Components are fully interactive in preview (shortcuts, scroll, etc.)  | High     |
| TUI-705 | Components accept injected data; no live dependencies in preview       | High     |
| TUI-706 | Components self-register with gallery for automatic listing            | Medium   |
| TUI-707 | Stub data exercises multiple visual states (empty, error, loading)     | Medium   |

## References

[Core Specification](../core/SPECIFICATION.md) - The complete 7-step workflow, document management, task hierarchy, failure handling, and session management.
