# TUI Module Specification

## Overview

The TUI module provides Lopen's headed (interactive) mode — a scrolling REPL terminal interface inspired by Claude Code, GitHub Copilot CLI, Amp Code, Open Code, and OpenAI Codex. It replaces the current "TUI not yet implemented" stub in `RootCommandHandler` with a fully functional interactive experience.

### Design Principles

- **Scrolling REPL, not full-screen** — output flows sequentially like a terminal session. No fixed panes, no split-screen layout. Built entirely with Spectre.Console.
- **Minimal code for maximum gain** — lean on RadLine for input, Spectre.Console for rendering, and existing core abstractions (`IOutputRenderer`, `IUserPromptQueue`, `IPauseController`) for orchestrator integration. Avoid reimplementing what libraries already provide.
- **Isolated, composable components** — every visual element is a standalone component that can be rendered independently via a component gallery. Complex views compose simpler components.
- **Immediate, responsive input** — text appears as typed, editing feels native, and the interface never blocks on rendering.

## Startup Behaviour

When Lopen launches without `--headless`, the TUI activates.

### Session Detection

On startup, before showing the first prompt:

1. Query `ISessionManager.ListSessionsAsync()` for incomplete sessions.
2. If the `--resume` flag was provided, resume the most recent incomplete session immediately — no prompt.
3. If `--resume` was **not** provided and incomplete sessions exist, render a hint line above the first prompt:
   ```
   ℹ Open session found. Use /resume to continue or /sessions to browse.
   ```
4. If no incomplete sessions exist, proceed directly to the prompt.

The default behaviour (no flag) is to **not** resume — the user must opt in.

### First Prompt

The TUI starts directly at the prompt input — no welcome screen. The workflow overview block (see §Workflow Overview) renders above the prompt from the very first interaction, showing the active model even before any workflow is running.

When no workflow is active, the overview block shows only the model and token counters (both at zero):

```
┌ lopen ─────────────────────────────────┐
│ model: gpt-4.1          tokens: 0      │
└────────────────────────────────────────┘
❯ _
```

## Prompt Input

The prompt input uses **RadLine** (`spectreconsole/radline`) for line editing, providing native Spectre.Console integration via `IAnsiConsole`.

### Capabilities

| Capability | Mechanism |
|---|---|
| Character-by-character rendering | RadLine's built-in key handling |
| Cursor movement (←/→, Home/End) | Built-in commands |
| Word jump (Ctrl+←/→) | `PreviousWordCommand` / `NextWordCommand` |
| Backspace and Delete | `BackspaceCommand` / `DeleteCommand` |
| Command history (↑/↓) | `ILineEditorHistory` implementation |
| Multi-line input (Shift+Enter) | `NewLineCommand` with multi-line mode |
| Tab completion for slash commands | `ITextCompletion` implementation |
| Submit (Enter) | `SubmitCommand` |

### Prompt Character

The prompt uses `❯` (U+276F) as the prompt character, styled with the theme's primary color.

### History

Command history persists across sessions using RadLine's `ILineEditorHistory`. History is stored in the Lopen data directory (alongside session data). Empty inputs and duplicate consecutive entries are excluded from history.

### Cancellation

- **Ctrl+C during input** — clears the current input line, does not exit.
- **Ctrl+C during processing** — cancels the current orchestrator step (via per-turn `CancellationTokenSource`). A second Ctrl+C force-exits.
- **Ctrl+D on empty input** — exits Lopen.

### Pause/Resume

**Ctrl+P** toggles `IPauseController.Toggle()`. When paused, the orchestrator gate blocks between steps. A visual indicator appears in the workflow overview block:

```
│ ⏸ PAUSED  ● Build › task 3/5           │
```

## Command Palette

Typing `?` on an empty prompt or pressing **Ctrl+O** at any point opens a filterable command palette overlay — similar to VS Code's command palette.

### Behaviour

1. On trigger, render the list of available commands below the prompt.
2. As the user types, filter the list in real-time to show only matching commands.
3. Arrow keys (↑/↓) navigate the filtered list; Enter selects the highlighted command.
4. Escape or Ctrl+C dismisses the palette and returns to the prompt.
5. Selecting a command executes it immediately (for action commands like `/clear`) or populates the prompt with the command text (for commands that take arguments).

### Command List

The palette displays all registered slash commands with their descriptions. The list is dynamically populated from the slash command registry — adding a new slash command automatically adds it to the palette.

## Slash Commands

All commands are prefixed with `/`. Unknown commands produce an error message: `Unknown command: /<name>. Type ? for available commands.`

### Core Commands

| Command | Description | Behaviour |
|---|---|---|
| `/help` | Show available commands | Renders the full command list with descriptions in a formatted panel |
| `/model` | Show or switch model | Without argument: shows current model. With argument (`/model gpt-4.1`): switches model and confirms |
| `/skills` | List available skills | Renders registered skills with descriptions |
| `/sessions` | Browse previous sessions | Lists recent sessions with ID, module, date, and status. User can select one to resume |
| `/resume` | Resume last session | Resumes the most recent incomplete session. If none exists, reports that |
| `/clear` | Clear terminal | Clears the screen; the workflow overview block re-renders above the next prompt |
| `/exit` | Exit lopen | Graceful shutdown — auto-saves if a session is active |

### Command Dispatch

Slash commands are dispatched via a registry pattern. Each command is a handler with:
- A name (the `/` prefix string)
- A description (shown in the palette and `/help`)
- An execute method

The dispatch returns a result indicating whether the command was handled, was unrecognised, or requested an exit.

## Workflow Overview

Before each prompt, a workflow overview block renders showing the current state. This is Pattern C — a compact box reprinted before every prompt interaction.

### States

**No active workflow:**
```
┌ lopen ─────────────────────────────────┐
│ model: gpt-4.1          tokens: 0      │
└────────────────────────────────────────┘
```

**Active workflow:**
```
┌ lopen ─────────────────────────────────┐
│ ✓ Assess  ✓ Plan  ● Build  ○ Verify   │
│ auth-module: task 3/5 — add-jwt-auth   │
│ tokens: 1.2k/50k    model: gpt-4.1    │
└────────────────────────────────────────┘
```

**Paused:**
```
┌ lopen ─────────────────────────────────┐
│ ⏸ PAUSED                               │
│ ✓ Assess  ✓ Plan  ● Build  ○ Verify   │
│ auth-module: task 3/5 — add-jwt-auth   │
│ tokens: 1.2k/50k    model: gpt-4.1    │
└────────────────────────────────────────┘
```

### Phase Indicators

| Symbol | Meaning |
|---|---|
| `✓` | Phase complete |
| `●` | Phase active |
| `○` | Phase pending |
| `⏸` | Workflow paused |

### Phase Mapping

The four display phases map from `WorkflowPhase` and `WorkflowStep`:

| Display Phase | Workflow Phase | Steps |
|---|---|---|
| Assess | `RequirementGathering` | `DraftSpecification` |
| Plan | `Planning` | `DetermineDependencies`, `IdentifyComponents`, `SelectNextComponent`, `BreakIntoTasks` |
| Build | `Building` | `IterateThroughTasks`, `Repeat` |
| Verify | (post-completion) | When `IsComplete` is true |

### Data Sources

| Field | Source |
|---|---|
| Phase indicators | `IWorkflowEngine.CurrentPhase` / `CurrentStep` / `IsComplete` |
| Module and task | `SessionState.Module`, `SessionState.Component`, `SessionState.TaskHierarchy` |
| Token count | `ITokenTracker.GetSessionMetrics()` → `CumulativeInputTokens + CumulativeOutputTokens` |
| Token budget | `LopenOptions.Budget` (from configuration module) |
| Model | `LopenOptions.Model` or current model selection |

### Model Change Notification

When the model changes (via `/model` command or automatic model selection by phase), the overview block on the next prompt reflects the new model. Additionally, a one-line notification renders immediately:

```
◆ Model switched to gpt-4.1
```

## Response Rendering

When the orchestrator produces output (via `IOutputRenderer`), the TUI renders it using Spectre.Console components.

### Thinking Indicator

While the LLM is processing, show an animated spinner:

```
◆ Thinking...
```

Uses `AnsiConsole.Status()` with `Spinner.Known.Dots` styled with the theme's muted color. The spinner replaces itself when the response begins.

### Content Streaming

Response content renders progressively. The rendering handles:

- **Code blocks** (` ``` `) — collected and rendered in a `Panel` with `BoxBorder.Rounded`
- **Bullet lists** (`- `) — rendered with `•` prefix in accent color
- **Bold text** (`**text**`) — rendered with `[bold]` markup
- **Regular text** — rendered inline as received

Content is rendered via `AnsiConsole.Markup()` / `AnsiConsole.MarkupLine()` with proper `Markup.Escape()` on any user-provided or LLM-generated text to prevent markup injection.

### Tool Call Display

When the orchestrator invokes tools, each tool call renders as:

1. **Spinner** while running: `⠋ Running: git-diff`
2. **Result** on completion:
   ```
   ✔ git-diff (1.2s)
   ┌──────────────────────────────────┐
   │ <tool output, truncated if long> │
   └──────────────────────────────────┘
   ```
3. **Failure**: `✘ git-diff (failed: <reason>)` in error color

Tool calls render sequentially — one spinner at a time.

### Stats Bar

After each response, render a stats line followed by a rule separator:

```
tokens: 142 (+142) | duration: 1.2s | model: gpt-4.1
─────────────────────────────────────────────────────
```

The `+142` shows tokens consumed by this specific response. The cumulative total appears in the workflow overview block.

## Session Management

### `/sessions` Command

Renders a table of recent sessions:

```
┌─────────────────────────────────────────────────────┐
│ Sessions                                            │
├──────────────────┬──────────┬───────────┬───────────┤
│ ID               │ Module   │ Step      │ Updated   │
├──────────────────┼──────────┼───────────┼───────────┤
│ auth-20260308-1  │ auth     │ Build     │ 2h ago    │
│ core-20260307-2  │ core     │ Complete  │ 1d ago    │
└──────────────────┴──────────┴───────────┴───────────┘
Select a session to resume (or Esc to cancel):
```

The user selects a session with arrow keys + Enter, which triggers resumption.

### `/resume` Command

Resumes the most recent incomplete session. Behaviour:

1. Query `ISessionManager.GetLatestSessionIdAsync()`.
2. If an incomplete session exists, load it and display: `Resuming session {id} at {step}`.
3. If no incomplete session exists, display: `No incomplete sessions found.`

### Resumption Flow

When a session is resumed (via `/resume`, `/sessions` selection, or `--resume` flag):

1. `ITokenTracker.RestoreMetrics()` restores cumulative token counters.
2. The workflow overview block updates to reflect the restored state.
3. The orchestrator continues from the persisted step via `IWorkflowEngine.InitializeAsync()`.

## Theme

The TUI uses a centralized theme with **terminal-native ANSI colors** (indices 0–15). This ensures colours respect the user's terminal theme configuration.

### Semantic Colour Roles

| Role | ANSI Colour | Usage |
|---|---|---|
| Primary | `Color.Blue` (12) | Prompt char, active elements, primary text accents |
| Accent | `Color.Aqua` (14) | Links, highlights, interactive elements |
| Success | `Color.Lime` (10) | Completed phases, successful tool calls |
| Warning | `Color.Yellow` (11) | Warnings, budget approaching limit |
| Error | `Color.Red` (9) | Errors, failed tool calls, critical states |
| Muted | `Color.Grey` (8) | Spinners, secondary information, timestamps |
| Text | `Color.White` (15) | Primary text content |

### Theme Implementation

A static theme class provides:
- `Color` constants for each semantic role
- `Style` objects combining colours with decorations (bold, dim, italic)
- Helper methods for generating Spectre markup strings (e.g., `Styled(text, role)`)
- `Markup.Escape()` wrappers to prevent injection

All UI components reference the theme by semantic role, never by direct colour. This enables future theme switching without modifying components.

### Unicode Glyphs

| Glyph | Usage |
|---|---|
| `❯` (U+276F) | Prompt character |
| `◆` (U+25C6) | Section markers (thinking, model switch) |
| `✓` (U+2713) | Completed phases/items |
| `●` (U+25CF) | Active/current phase |
| `○` (U+25CB) | Pending phase |
| `•` (U+2022) | Bullet list items |
| `✔` (U+2714) | Successful tool call |
| `✘` (U+2718) | Failed tool call |
| `⏸` (U+23F8) | Paused indicator |
| `ℹ` (U+2139) | Informational hint |

## Orchestrator Integration

The TUI module provides concrete implementations of existing core abstractions to wire the interactive experience into the orchestrator loop.

### `TuiOutputRenderer` — implements `IOutputRenderer`

| Method | TUI Behaviour |
|---|---|
| `RenderProgressAsync(phase, step, progress)` | Updates internal state used by the workflow overview block. On phase transitions, renders a divider: `───── Entering {phase} ─────` |
| `RenderErrorAsync(message, exception?)` | Renders error in a `Panel` with error colour and `BoxBorder.Rounded` |
| `RenderResultAsync(message)` | Renders message as formatted content (markdown-aware) |
| `PromptAsync(message)` | Delegates to RadLine prompt, displaying the message. Returns user input or `null` on Ctrl+C |

### `TuiUserPromptQueue` — implements `IUserPromptQueue`

A thread-safe queue (e.g., `Channel<string>`) allowing the TUI input loop to enqueue user messages that the orchestrator drains into the next LLM context. This decouples the TUI input thread from the orchestrator processing thread.

| Method | Behaviour |
|---|---|
| `Enqueue(prompt)` | Adds a user message to the queue |
| `TryDequeue(out prompt)` | Non-blocking dequeue |
| `DequeueAsync(ct)` | Async dequeue, awaits until a message is available or cancelled |
| `Count` | Current queue depth |

### Service Registration

The TUI registers its implementations **before** `AddLopenCore()` is called, since core uses `TryAddSingleton<IOutputRenderer>`. This ensures `TuiOutputRenderer` takes precedence over `HeadlessRenderer`.

`IUserPromptQueue` is registered as a singleton — it is not registered by `AddLopenCore()` (which resolves it as optional), so the TUI's registration is the only one.

### REPL Loop

The TUI runs a REPL loop that:

1. Renders the workflow overview block.
2. Reads input via RadLine.
3. Dispatches slash commands if the input starts with `/`.
4. Otherwise, enqueues the input via `IUserPromptQueue.Enqueue()`.
5. If no orchestrator workflow is running, starts one via `IWorkflowOrchestrator.RunAsync()`.
6. If a workflow is already running, the enqueued message is drained by the orchestrator on its next LLM call.
7. Repeats.

The REPL loop and orchestrator run on separate logical threads. The `IOutputRenderer` methods are called from the orchestrator thread and must render safely to the console (which RadLine is not actively using between prompts).

## Component Gallery

A `lopen tui gallery` CLI command launches an interactive gallery for visual testing of TUI components in isolation.

### Behaviour

1. Renders a selection list of all registered components.
2. User selects a component with arrow keys + Enter.
3. The selected component renders with mock/sample data.
4. After viewing, the user returns to the selection list (Escape or Enter to go back).

### Registered Components

Each component registers itself in the gallery with:
- A display name
- A render method that takes no external dependencies (uses mock data)

The gallery must include at minimum:
- Workflow overview block (all states: no workflow, active, paused, complete)
- Prompt input (demonstrating history, multi-line, tab completion)
- Command palette (showing filtering)
- Response rendering (thinking → content → tool calls → stats)
- Session list table
- Error panel
- Slash command help output

## Acceptance Criteria

- [ ] [TUI-01] Running `lopen` without `--headless` launches the TUI REPL instead of printing "TUI not yet implemented"
- [ ] [TUI-02] The prompt character `❯` renders in the theme's primary colour and accepts text input via RadLine
- [ ] [TUI-03] Left/right arrow keys, Home/End, and Ctrl+arrow word-jump work within the prompt input
- [ ] [TUI-04] Up/down arrows navigate command history; history persists across lopen sessions
- [ ] [TUI-05] Shift+Enter inserts a newline; multi-line input displays correctly and submits on Enter
- [ ] [TUI-06] Tab-completing a `/` prefix shows matching slash commands
- [ ] [TUI-07] Ctrl+C on an empty prompt clears the line; Ctrl+C during processing cancels the current step; double Ctrl+C exits
- [ ] [TUI-08] Ctrl+D on an empty prompt exits lopen gracefully
- [ ] [TUI-09] Ctrl+P toggles pause/resume via `IPauseController.Toggle()`; the paused state is visible in the workflow overview
- [ ] [TUI-10] Typing `?` on an empty prompt opens the command palette with all registered commands
- [ ] [TUI-11] Ctrl+O opens the command palette regardless of prompt content
- [ ] [TUI-12] The command palette filters commands in real-time as the user types
- [ ] [TUI-13] Arrow keys navigate the palette; Enter selects; Escape dismisses
- [ ] [TUI-14] All seven slash commands (`/help`, `/model`, `/skills`, `/sessions`, `/resume`, `/clear`, `/exit`) are functional
- [ ] [TUI-15] `/model` without arguments shows the current model; `/model <name>` switches and confirms
- [ ] [TUI-16] `/sessions` renders a table of recent sessions and allows selection for resumption
- [ ] [TUI-17] `/resume` resumes the most recent incomplete session with token metrics restored
- [ ] [TUI-18] The workflow overview block renders before every prompt
- [ ] [TUI-19] The overview shows phase indicators (✓/●/○) correctly mapped from `WorkflowPhase`/`WorkflowStep`
- [ ] [TUI-20] The overview shows current module, component, and task when a workflow is active
- [ ] [TUI-21] The overview shows cumulative token count and current model at all times (including before any workflow)
- [ ] [TUI-22] Model changes produce a visible notification (`◆ Model switched to <name>`)
- [ ] [TUI-23] A thinking spinner (`◆ Thinking...`) displays while the LLM processes
- [ ] [TUI-24] Response content renders with code blocks in panels, bullet lists with `•`, and bold text
- [ ] [TUI-25] All rendered text uses `Markup.Escape()` to prevent markup injection from LLM output
- [ ] [TUI-26] Tool calls show a spinner while running, then `✔`/`✘` with duration and optional output panel
- [ ] [TUI-27] A stats bar with token delta, duration, and model renders after each response
- [ ] [TUI-28] `TuiOutputRenderer` implements `IOutputRenderer` and is registered before `AddLopenCore()`
- [ ] [TUI-29] `TuiUserPromptQueue` implements `IUserPromptQueue` using a thread-safe channel
- [ ] [TUI-30] User input typed during orchestrator processing is enqueued and drained into the next LLM call
- [ ] [TUI-31] On startup with `--resume`, the session resumes immediately without prompting
- [ ] [TUI-32] On startup without `--resume` but with open sessions, a hint line appears above the first prompt
- [ ] [TUI-33] On startup with no open sessions, the prompt appears directly with no session hint
- [ ] [TUI-34] `lopen tui gallery` launches and lists all registered components
- [ ] [TUI-35] Each gallery component renders with mock data independently of external services
- [ ] [TUI-36] The gallery includes at minimum: workflow overview (4 states), prompt input, command palette, response rendering, session list, error panel, and help output
- [ ] [TUI-37] All colours use the centralized theme with terminal-native ANSI 0-15 indices
- [ ] [TUI-38] All UI components reference the theme by semantic role, not by direct colour
- [ ] [TUI-39] `/exit` triggers graceful shutdown with session auto-save if active
- [ ] [TUI-40] Unknown slash commands produce a clear error message with guidance

## Dependencies

### Internal Modules

| Module | Dependency | Rationale |
|---|---|---|
| `core` | `IOutputRenderer`, `IUserPromptQueue`, `IPauseController`, `IWorkflowOrchestrator`, `IWorkflowEngine` | Orchestrator integration and workflow state |
| `storage` | `ISessionManager`, `SessionState`, `SessionMetrics` | Session discovery, resumption, persistence |
| `llm` | `ITokenTracker`, `SessionTokenMetrics`, `WorkflowPhase` | Token display and model information |
| `configuration` | `LopenOptions` | Budget limits, model defaults |
| `cli` | `RootCommandHandler`, `GlobalOptions` | Entry point integration, `--headless`/`--resume` flags |

### External Packages

| Package | Version | Rationale |
|---|---|---|
| `Spectre.Console` | ≥ 0.49.0 | All rendering: panels, tables, rules, markup, spinners, figlet |
| `RadLine` | ≥ 0.9.0 | Prompt input: line editing, history, multi-line, tab completion |

### Runtime Requirements

- .NET 10.0 (`net10.0`)
- Terminal with UTF-8 support (for unicode glyphs)
- 16-colour ANSI support minimum (for terminal-native theme)

## Skills & Hooks

### Build Verification

```bash
dotnet build src/Lopen.Tui/Lopen.Tui.csproj --no-restore
dotnet test tests/Lopen.Tui.Tests/Lopen.Tui.Tests.csproj --no-restore
```

### Component Gallery Verification

```bash
dotnet run --project src/Lopen.Cli -- tui gallery
```

Each component in the gallery must render without exceptions and produce visible terminal output.

### Integration Verification

```bash
# Verify TUI launches (non-interactive smoke test)
echo "/exit" | dotnet run --project src/Lopen.Cli

# Verify headless mode still works
dotnet run --project src/Lopen.Cli -- --headless --module test-module --prompt "hello"
```

### Linting

```bash
dotnet format src/Lopen.Tui/Lopen.Tui.csproj --verify-no-changes
dotnet format tests/Lopen.Tui.Tests/Lopen.Tui.Tests.csproj --verify-no-changes
```

## Notes

- **RadLine macOS limitation**: Modifier+Enter (Shift+Enter for newlines) does not register on macOS due to a `System.Console.ReadKey` OS-level bug. RadLine's maintainers plan to move away from this API. For macOS users, an alternative multi-line trigger (e.g., `Alt+Enter` or a slash command) may be needed.
- **RadLine preview status**: RadLine is at v0.9.0 and has been in preview for several years. The NuGet description states it will be merged into Spectre.Console. If RadLine is abandoned or becomes incompatible, the fallback is a custom `Console.ReadKey()` loop (~400-500 LOC) implementing the same capabilities.
- **Accent/combining character handling**: RadLine's combining character support is acknowledged as partial (works for western alphabets and most emoji). Full Unicode support (CJK, complex emoji sequences) is out of scope for the initial delivery.
- **Thread safety**: The REPL input loop and orchestrator run on separate threads. All `IOutputRenderer` method calls from the orchestrator must be safe to execute while RadLine is not actively reading input. This is naturally the case in a REPL (read → process → render → read), but must be verified under edge cases (user types during rendering).
- **Future theme switching**: The theme abstraction (semantic roles, not direct colours) is designed to support future theme switching (e.g., `/theme catppuccin-mocha`). This is explicitly out of scope for the initial delivery.
- **Response streaming**: The initial delivery uses post-hoc rendering (full response → render). True token-by-token streaming would require `IAsyncEnumerable<AgentEvent>` from the LLM module, which is a separate enhancement.

## References

- [TUI POC](/home/lopen/source/tui-poc) — Proof of concept demonstrating the scrolling REPL pattern with Spectre.Console
- [RadLine](https://github.com/spectreconsole/radline) — Line editor library by Spectre.Console author
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) — Console rendering library
- [Core Specification](../core/SPECIFICATION.md) — Workflow orchestration, pause control, output rendering abstractions
- [Storage Specification](../storage/SPECIFICATION.md) — Session management, persistence
- [LLM Specification](../llm/SPECIFICATION.md) — Token tracking, model selection
- [CLI Specification](../cli/SPECIFICATION.md) — Command structure, headless mode, global flags
