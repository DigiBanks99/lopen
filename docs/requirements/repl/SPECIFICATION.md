# REPL - Specification

> Interactive Read-Eval-Print Loop with session management

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-010 | REPL Mode | High | ⚪ Not Started |
| REQ-011 | Session State Management | High | ⚪ Not Started |
| REQ-012 | Command History | Medium | ⚪ Not Started |
| REQ-013 | Auto-completion | Medium | ⚪ Not Started |

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
- [ ] Starts interactive session
- [ ] Processes commands in a loop
- [ ] Graceful exit with `exit`, `quit`, or Ctrl+C
- [ ] Displays prompt indicating ready state
- [ ] Maintains context between commands (see REQ-011)

---

## REQ-011: Session State Management

### Description
Maintain state between commands within a REPL session.

### Acceptance Criteria
- [ ] Preserve conversation context across prompts
- [ ] Track authenticated state
- [ ] Manage Copilot SDK session lifecycle
- [ ] Support session save/restore

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
- [ ] Up/Down arrow navigation through history
- [ ] Persistent history across REPL sessions
- [ ] History file location: `~/.lopen/history`
- [ ] Configurable history size (default: 1000)

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
- [ ] Tab completion for commands
- [ ] Tab completion for subcommands
- [ ] Tab completion for common options
- [ ] Context-aware suggestions

### Implementation
- `IAutoCompleter` interface for completion providers
- `CompletionItem` record with Text and optional Description
- `CommandAutoCompleter` with command/subcommand/option registration
- Tab key cycles through completions in ConsoleInputWithHistory
- Context-aware: shows subcommands after command, options after `--`
- 17 unit tests covering all completion scenarios
