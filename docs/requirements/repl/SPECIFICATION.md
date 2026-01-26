# REPL - Specification

> Interactive Read-Eval-Print Loop with session management

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-010 | REPL Mode | High | 🟢 Complete |
| REQ-011 | Session State Management | High | 🟢 Complete |
| REQ-012 | Command History | Medium | 🟢 Complete |
| REQ-013 | Auto-completion | Medium | 🟢 Complete |

---

## REQ-010: REPL Mode

### Description
Interactive command loop for continuous interaction with Copilot SDK.

### Command Signature
```bash
lopen repl                    # Start REPL
lopen                         # Default to REPL if no command
```

### Acceptance Criteria
- [x] Starts interactive session
- [x] Processes commands in a loop
- [x] Graceful exit with `exit`, `quit`, or Ctrl+C
- [x] Displays prompt indicating ready state
- [ ] Maintains context between commands (see REQ-011)

---

## REQ-011: Session State Management

### Description
Maintain state between commands within a REPL session.

### Acceptance Criteria
- [x] Preserve conversation context across prompts
- [x] Track authenticated state
- [x] Manage Copilot SDK session lifecycle
- [x] Support session save/restore

### Implementation
- `SessionState` model class with SessionId, StartedAt, IsAuthenticated, Username, ConversationHistory, Preferences
- `ISessionStateService` / `SessionStateService` for state management
- `ISessionStore` / `FileSessionStore` for session persistence in `~/.lopen/sessions/`
- `PersistableSessionState` for JSON serialization
- `SessionSummary` for session listing
- `MockSessionStore` for testing
- CLI commands: `repl-session save`, `repl-session load`, `repl-session list`, `repl-session delete`
- Integration with `ReplService` for automatic initialization and command tracking
- 49 unit tests covering all functionality

### State to Maintain
- Authentication status (synced with AuthService)
- Current session ID and start time
- Command count
- Conversation history
- User preferences

---

## REQ-012: Command History

### Description
Remember and navigate through previously entered commands.

### Acceptance Criteria
- [x] Up/Down arrow navigation through history
- [x] Persistent history across REPL sessions
- [x] History file location: `~/.lopen/history`
- [x] Configurable history size (default: 1000)

### Implementation
- `ICommandHistory` interface with navigation API (GetPrevious, GetNext, ResetPosition)
- `CommandHistory` class for in-memory history with max size limit
- `PersistentCommandHistory` for file-based persistence
- `ConsoleInputWithHistory` for enhanced console input with arrow key navigation
- Full line editing: Backspace, Delete, Home, End, Left/Right arrows, Escape to clear
- 29 unit tests covering all functionality

---

## REQ-013: Auto-completion

### Description
Provide intelligent command completion suggestions.

### Acceptance Criteria
- [x] Tab completion for commands
- [x] Tab completion for subcommands
- [x] Tab completion for common options
- [x] Context-aware suggestions

### Implementation
- `IAutoCompleter` interface for completion providers
- `CompletionItem` record with Text and optional Description
- `CommandAutoCompleter` with command/subcommand/option registration
- Tab key cycles through completions in ConsoleInputWithHistory
- Context-aware: shows subcommands after command, options after `--`
- 17 unit tests covering all completion scenarios
