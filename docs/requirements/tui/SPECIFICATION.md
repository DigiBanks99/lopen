# Terminal UI Specification

> A coding agent harness TUI that fills the terminal, providing a welcoming developer experience.

## Overview

Lopen is a full-screen terminal application (like neovim) that serves as a coding agent harness. The TUI provides a consistent, developer-friendly interface for interacting with AI agents across three workflow modes: **Draft Specification**, **Plan**, and **Build**.

---

## Layout Structure

The TUI fills the entire terminal with three distinct zones:

```

 TOP PANEL (max 20 lines)                                                    │
 ┌─────────────────┐                                                         │
 │  LOPEN LOGO     │  v1.0.0  │  Model: claude-sonnet  │  Mode: Build       │
 │  (ASCII Art)    │                                                         │
 └─────────────────┘                                                         │
                                          Context: 2.4K/128K  │  main  │  🟢 │

 WORKSPACE AREA (scrollable)                                                 │
                                                                             │
 Conversation history, tool outputs, code blocks, diffs                      │
                                                                             │
                                                                             │
                                                                             │

 PROMPT AREA                                                                 │
 > Multi-line input here...                                                  │
                                                                             │
 Enter: Submit  │  Ctrl+Enter: New line  │  Ctrl+C: Cancel  │  /help        │

```

### Top Panel

Always visible (suppressible with `--quiet` or `--no-logo`). Contains:

| Element | Position | Description |
|---------|----------|-------------|
| ASCII Logo | Left | Lopen branding |
| Version | Right of logo | `v{Major}.{Minor}.{Patch}` |
| Current Model | Center-right | Active AI model name |
| Agent Mode | Right | Draft Spec / Plan / Build |
| Context Usage | Bottom-right | `{used}/{total}` tokens |
| Git Branch | Bottom-right | Current branch if in repo |
| Auth Status | Bottom-right | 🟢 authenticated / 🔴 expired |
| Working Directory | Bottom | Current project path |

### Workspace Area

Scrollable area displaying:
- Conversation history (user prompts and agent responses)
- Tool call outputs (file edits, command results)
- Code blocks with syntax highlighting
- Diff views for file changes

### Prompt Area

Fixed at bottom with clear border separation:
- Multi-line text input
- **Enter**: Submit prompt
- **Ctrl+Enter**: Insert newline
- **Ctrl+C**: Cancel current operation
- Context-aware hints showing available commands

---

## Landing Page

On startup, display a modal overlay before entering the workspace:

```
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
    Ctrl+P         Switch to plan mode                                       │
    Ctrl+B         Switch to build mode                                      │
                                                                             │

  Press any key to continue...                              🟢 Authenticated │

```

### Behavior
- Modal dismisses on any keypress
- Quick commands section is **configurable from code** per workspace context
- Auth state shown at bottom row
- After dismissal, transitions seamlessly to the main workspace

---

## Agent Modes

| Mode | Purpose | Typical Commands |
|------|---------|------------------|
| Draft Specification | Define requirements and specs | `/spec`, research, outline |
| Plan | Create implementation plans | `/plan`, break down tasks |
| Build | Execute code changes | `/build`, apply changes |

Mode indicator always visible in top panel. Context-aware quick commands update based on current mode.

---

## UI Components

### Tool Call Display

Display tool calls in Copilot CLI style:

```
 Edit AGENTS.md (+2 -3)
 Read package.json
 Run command
  $ npm test
  └ 23 lines (success)
```

- Bullet prefix (●) for each tool call
- File operations show diff stats `(+N -M)`
- Command outputs collapsible with line count summary
- Expandable to show full output on demand

### Confirmation Modals

For actions requiring user confirmation:

```
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

```
 Thinking...
```

Display spinner with status text, then render complete response when finished (no character-by-character streaming).

### Error Display

| Severity | Display |
|----------|---------|
| Critical | Modal dialog with details and recovery options |
| Minor | Inline message in workspace with suggested fix |

```
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

```
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

```
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

| Semantic | Usage |
|----------|-------|
| Success (green) | Completed operations, confirmations |
| Error (red) | Failures, critical issues |
| Warning (yellow) | Cautions, non-blocking issues |
| Info (blue) | Informational messages |
| Muted (gray) | Secondary text, timestamps |
| Accent (cyan/magenta) | Highlights, selections |

Rely on terminal's color scheme for actual RGB values. Support `NO_COLOR` environment variable.

### Symbols

| Symbol | Fallback | Usage |
|--------|----------|-------|
| ● | * | Tool call bullet |
| ✓ | [OK] | Success |
| ✗ | [X] | Error |
| ⚠ | [!] | Warning |
| 💡 | [i] | Tip/suggestion |
| 🟢 | [OK] | Status good |
| 🔴 | [!!] | Status bad |

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

## CLI Flags

| Flag | Effect |
|------|--------|
| `--quiet`, `-q` | Suppress logo and non-essential output |
| `--no-logo` | Hide ASCII logo in top panel |
| `--no-color` | Disable colors (also respects `NO_COLOR` env) |

---

## Requirements Checklist

| ID | Requirement | Priority |
|----|-------------|----------|
| TUI-001 | Full-screen layout with three zones | High |
| TUI-002 | Landing page modal with quick commands | High |
| TUI-003 | Top panel with logo, version, model, mode, context | High |
| TUI-004 | Multi-line prompt with keyboard hints | High |
| TUI-005 | Tool call display (Copilot CLI style) | High |
| TUI-006 | Confirmation modals with Yes/No/Always/Other | High |
| TUI-007 | Spinner-based async feedback | Medium |
| TUI-008 | Error display (modal for critical, inline for minor) | High |
| TUI-009 | Diff viewer | Medium |
| TUI-010 | File picker | Low |
| TUI-011 | Semantic color palette | Medium |
| TUI-012 | Context-aware quick commands | Medium |
| TUI-013 | `--quiet` and `--no-logo` flags | Low |
