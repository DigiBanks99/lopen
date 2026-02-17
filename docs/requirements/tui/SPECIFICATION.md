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

- Semi-automatic reviews offered at boundaries, though progression from phase 1 is human driven
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
│  ╻  ┏━┓┏━┓┏━╸┏┓╻  v1.0.0 │ claude-opus-4.6  │ Context: 2.4K/128K (🔥 23 premium)  │  main  │  🟢   │
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

See [Storage Specification § Session Persistence](../storage/SPECIFICATION.md#session-persistence) for session persistence details.

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
                             v1.0.0                                          │
                         Interactive Agent Loop                              │
                                                                             │

  Quick Commands                                                             │
                                                                             │
    /help          Show available commands                                   │
    /spec          Start requirement gathering                               │
    /plan          Start planning mode                                       │
    /build         Start build mode                                          │
    /session       Manage sessions                                           │
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
- "Phase: Planning" (Steps 2-5)  
- "Phase: Building" (Steps 6-7)

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

## Slash Commands

Within the TUI prompt area, users can type slash commands as shortcuts to CLI subcommands. These are convenience aliases — they invoke the same logic as the corresponding `lopen <command>`:

| Slash Command   | Equivalent CLI Command | Description                           |
| --------------- | ---------------------- | ------------------------------------- |
| `/help`         | `lopen --help`         | Show available commands and usage     |
| `/spec`         | `lopen spec`           | Start or resume requirement gathering |
| `/plan`         | `lopen plan`           | Start or resume planning phase        |
| `/build`        | `lopen build`          | Start or resume building phase        |
| `/session list` | `lopen session list`   | List all sessions                     |
| `/session show` | `lopen session show`   | Show current session details          |
| `/config show`  | `lopen config show`    | Show resolved configuration           |
| `/revert`       | `lopen revert`         | Revert to last known-good commit      |
| `/auth status`  | `lopen auth status`    | Check authentication state            |

- Slash commands are only available within the TUI prompt area
- Unknown slash commands display an error with a list of valid commands
- Any text not starting with `/` is treated as a user prompt to the LLM

---

## User Input During Active Workflow

When the agent is actively working (e.g., during task execution in the Building phase), the TUI prompt area remains available for:

- **Metadata inspection**: Users can browse the context panel, expand/collapse tool call outputs, and view resources without interrupting the agent
- **Queued messages**: User prompts typed during active execution are queued and delivered as additional context in the next SDK invocation (the next loop iteration), not injected into the current invocation
- **Pause**: `Ctrl+P` pauses agent execution, allowing the user to type a prompt that will be included in the next invocation when resumed

This design preserves the fresh-context-per-invocation model (see [LLM § Context Window Strategy](../llm/SPECIFICATION.md#context-window-strategy)) while keeping the user informed and able to influence the next iteration.

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

| Flag            | Effect                                                                                                                             |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `--quiet`, `-q` | Alias for `--headless` — disables TUI, plain text output (see [CLI Specification](../cli/SPECIFICATION.md#headless-mode-behavior)) |
| `--no-logo`     | Hide ASCII logo in top panel                                                                                                       |
| `--no-color`    | Disable colors (also respects `NO_COLOR` env)                                                                                      |
| `--no-welcome`  | Skip landing page modal, go straight to workspace                                                                                  |
| `--unattended`  | Suppress failure confirmations, full autonomous mode                                                                               |
| `--resume [ID]` | Resume specific session by ID (skip resume prompt)                                                                                 |
| `--no-resume`   | Ignore previous session, start fresh                                                                                               |

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

## Acceptance Criteria

### Layout & Structure

- [ ] [TUI-01] Split-screen layout with activity (left) and context (right) panes, ratio adjustable from 50/50 to 80/20
- [ ] [TUI-02] Top panel displays logo, version, model, context usage, premium requests, git branch, auth status, phase, and step
- [ ] [TUI-03] Context panel shows current task, task tree with completion states, and active resources
- [ ] [TUI-04] Main activity area supports scrolling with progressive disclosure
- [ ] [TUI-05] Multi-line prompt area with keyboard hints at bottom
- [ ] [TUI-06] Landing page modal with quick commands on first startup (skippable with `--no-welcome`)
- [ ] [TUI-07] Session resume modal displayed when previous active session detected

### Display & Interaction

- [ ] [TUI-08] Current action expanded, previous actions collapsed to summaries
- [ ] [TUI-09] Tool call outputs expandable via click or keyboard shortcut
- [ ] [TUI-10] Real-time task progress updates in context panel
- [ ] [TUI-11] Hierarchical task tree with status indicators (✓/▶/○)
- [ ] [TUI-12] Numbered resource access (press 1-9 to view active resources)
- [ ] [TUI-13] Inline research display with ability to drill into full document
- [ ] [TUI-14] Phase transition summaries shown in activity area
- [x] [TUI-15] Diff viewer with syntax highlighting and line numbers
- [ ] [TUI-16] File picker with tree view navigation
- [ ] [TUI-17] Phase/step visualization (●/○ progress indicator) in top panel
- [ ] [TUI-18] Module selection modal with arrow key navigation
- [ ] [TUI-19] Component selection UI with tree view

### User Interaction Patterns

- [ ] [TUI-20] Multi-line prompt input with Alt+Enter for newlines
- [ ] [TUI-21] Keyboard shortcuts functional: Tab (focus panel), Ctrl+P (pause), number keys (resources)
- [ ] [TUI-22] Guided conversation UI for requirement gathering (step 1)
- [ ] [TUI-23] Confirmation modals with Yes/No/Always/Other options
- [ ] [TUI-24] Expandable sections via click or keyboard shortcut

### Feedback & Status

- [ ] [TUI-25] Task failures displayed inline and auto-expanded
- [ ] [TUI-26] Repeated failure confirmation modal shown at configured threshold
- [ ] [TUI-27] Critical error modal with details and recovery options
- [ ] [TUI-28] Spinner-based async feedback for long-running operations
- [ ] [TUI-29] Context window usage displayed in top panel
- [ ] [TUI-30] Premium request counter displayed in top panel (🔥 indicator)
- [ ] [TUI-31] Real-time progress percentages in context panel

### Visual Design

- [ ] [TUI-32] Semantic color palette (green/red/yellow/blue/gray/cyan) using terminal theme colors
- [ ] [TUI-33] Unicode symbols with ASCII fallbacks for all indicators
- [ ] [TUI-34] Box-drawing characters used for borders and panels
- [ ] [TUI-35] Syntax highlighting in code blocks
- [x] [TUI-36] Consistent panel styling throughout the application
- [ ] [TUI-37] `NO_COLOR` environment variable respected

### Slash Commands & Input

- [ ] [TUI-38] Slash commands (`/help`, `/spec`, `/plan`, `/build`, `/session`, `/config`, `/revert`, `/auth`) invoke corresponding CLI commands
- [ ] [TUI-39] Unknown slash commands display error with valid command list
- [ ] [TUI-40] Queued user messages delivered as context in the next SDK invocation
- [ ] [TUI-41] `Ctrl+P` pauses agent execution

### Component Gallery

- [ ] [TUI-42] `lopen test tui` launches interactive component gallery
- [ ] [TUI-43] Gallery lists all TUI components with selection navigation
- [ ] [TUI-44] Each component renders with realistic mock/stub data
- [x] [TUI-45] Components are fully interactive in preview (shortcuts, scroll, expand/collapse)
- [ ] [TUI-46] Components accept injected data with no live dependencies in preview
- [ ] [TUI-47] Components self-register with gallery for automatic listing
- [ ] [TUI-48] Stub data exercises multiple visual states (empty, populated, error, loading)
- [ ] [TUI-49] All components tested via the `lopen test tui` command and fully functional
- [x] [TUI-50] A `TuiOutputRenderer` implementation of `IOutputRenderer` bridges orchestrator output events to the TUI activity panel, replacing the default `HeadlessRenderer` when TUI mode is active
- [x] [TUI-51] All TUI data providers (`IContextPanelDataProvider`, `IActivityPanelDataProvider`, `IUserPromptQueue`, `ISessionDetector`) are registered in DI and wired to live orchestrator/session data when TUI mode is active
- [x] [TUI-52] TUI `RunAsync` launches the `WorkflowOrchestrator` on a background thread and renders its progress in real time — the TUI is not a passive shell but actively drives and displays the workflow

---

## Dependencies

- **[Core module](../core/SPECIFICATION.md)** — Workflow phases, task hierarchy, task states, document management, failure handling
- **[LLM module](../llm/SPECIFICATION.md)** — Token metrics, context window usage, premium request counts
- **[Storage module](../storage/SPECIFICATION.md)** — Session state for resume modal, plan data for task tree
- **[Configuration module](../configuration/SPECIFICATION.md)** — Display settings (show_token_usage, show_premium_count)
- **[CLI module](../cli/SPECIFICATION.md)** — Command definitions for slash command aliases
- **[Auth module](../auth/SPECIFICATION.md)** — Auth status indicator
- **[Spectre.Console](https://spectreconsole.net/)** — .NET terminal UI library (spinners, trees, panels, tables)

---

## Skills & Hooks

- **verify-tui-render**: Validate that all TUI components render without errors using stub data
- **verify-tui-gallery**: Validate that `lopen test tui` launches and all components are listed and previewable

---

## Notes

- The TUI is built on Spectre.Console (or equivalent .NET terminal UI library). Component architecture must support dependency injection of data/state for testability and the component gallery.
- TUI-specific flags (`--no-welcome`, `--no-logo`, `--no-color`) are owned by this module and not duplicated in the CLI spec's global flags table.
- `lopen test tui` is a development/testing command owned by this module; it is not listed in the CLI spec's command structure.

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow phases, task hierarchy, task states, document management, failure handling
- [LLM Specification](../llm/SPECIFICATION.md) — Token metrics, context window usage, premium request counts
- [Storage Specification](../storage/SPECIFICATION.md) — Session state for resume modal, plan data for task tree
- [Configuration Specification](../configuration/SPECIFICATION.md) — Display settings, TUI-related configuration
- [CLI Specification](../cli/SPECIFICATION.md) — Command definitions for slash command aliases
- [Auth Specification](../auth/SPECIFICATION.md) — Authentication state for status indicator
