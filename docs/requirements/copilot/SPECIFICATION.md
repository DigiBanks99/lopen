# Copilot Integration - Specification

> GitHub Copilot SDK integration for AI-powered chat and agentic workflows

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-020 | Copilot SDK Integration | Critical | 🟢 Complete |
| REQ-021 | Chat Command | High | 🟢 Complete |
| REQ-022 | Streaming Responses | High | 🟢 Complete |
| REQ-023 | Custom Tools | Medium | 🟢 Complete |
| REQ-024 | Session Persistence | Medium | 🟢 Complete |

---

## REQ-020: Copilot SDK Integration

### Description

Integrate the official GitHub Copilot SDK (`GitHub.Copilot.SDK`) to enable AI-powered interactions.

### Prerequisites

- GitHub Copilot subscription (free tier available)
- Copilot CLI installed and authenticated (`copilot --version`)

### Acceptance Criteria

- [x] Add `GitHub.Copilot.SDK` NuGet package
- [x] Create `ICopilotService` interface in Lopen.Core
- [x] Implement `CopilotService` with SDK client management
- [x] Handle connection to Copilot CLI server (JSON-RPC)
- [x] Support session lifecycle management (create, use, dispose)

### Architecture

```tree
lopen CLI (System.CommandLine)
       ↓
  ICopilotService (interface)
       ↓
  CopilotService (SDK wrapper)
       ↓ JSON-RPC
  Copilot CLI (server mode)
```

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-020-01 | Create Copilot client | Client starts without error |
| TC-020-02 | Create session | Session created with model config |
| TC-020-03 | Client cleanup | Graceful shutdown on dispose |

---

## REQ-021: Chat Command

### Description

Add a `chat` command for interactive AI conversations.

### Command Signature

```bash
lopen chat                     # Start interactive chat
lopen chat "query"             # Single query mode
lopen chat --model gpt-4.1     # Specify model
lopen chat --streaming         # Enable streaming (default)
```

### Acceptance Criteria

- [x] `lopen chat` starts interactive chat session
- [x] Single query mode with inline prompt argument
- [x] Model selection via `--model` option
- [x] Streaming enabled by default
- [x] Graceful exit with `exit`, `quit`, or Ctrl+C

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-021-01 | `lopen chat "Hello"` | Returns AI response |
| TC-021-02 | `lopen chat --model gpt-4.1` | Uses specified model |
| TC-021-03 | Interactive exit | Clean session cleanup |

---

## REQ-022: Streaming Responses

### Description

Display AI responses as they are generated, word by word.

### Acceptance Criteria

- [x] Subscribe to `AssistantMessageDeltaEvent` events
- [x] Write delta content immediately to console
- [x] Handle `SessionIdleEvent` to finalize response
- [x] Respect `NO_COLOR` for output styling
- [x] Support cancellation via Ctrl+C

### Implementation Pattern

```csharp
session.On(ev =>
{
    if (ev is AssistantMessageDeltaEvent delta)
        Console.Write(delta.Data.DeltaContent);
    if (ev is SessionIdleEvent)
        Console.WriteLine();
});
```

---

## REQ-023: Custom Tools

### Description

Enable custom tools that Copilot can invoke during conversations.

### Acceptance Criteria

- [x] Define tools using `Microsoft.Extensions.AI` pattern
- [x] Register tools with session configuration
- [x] Handle tool invocations automatically
- [x] Built-in tools: file operations (read, list, exists, cwd)
- [x] Built-in tools: git operations (status, diff, log)

### Implementation

Built-in `LopenTools` class with:
- `lopen_read_file` - Read file contents
- `lopen_list_directory` - List directory entries
- `lopen_get_cwd` - Get current working directory
- `lopen_file_exists` - Check if file/directory exists
- `lopen_git_status` - Get git repository status
- `lopen_git_diff` - Get git diff (optional file path and staged flag)
- `lopen_git_log` - Get recent git commits (with limit and format options)

`CopilotSessionOptions` extended with:
- `Tools` - Custom AIFunction collection
- `AvailableTools` - Built-in tools to enable
- `ExcludedTools` - Built-in tools to disable

### Example Tool

```csharp
var getTool = AIFunctionFactory.Create(
    ([Description("File path")] string path) => File.ReadAllText(path),
    "read_file",
    "Read contents of a file"
);
```

---

## REQ-024: Session Persistence

### Description

Save and restore chat sessions across CLI invocations.

### Acceptance Criteria

- [x] Save session state to `~/.lopen/sessions/`
- [x] Resume sessions by ID
- [x] List available sessions
- [x] Delete old sessions

### Command Signature

```bash
lopen chat --resume <session-id>
lopen sessions list
lopen sessions delete <id>
```

### Implementation

- `lopen chat --resume/-r <id>` resumes existing session
- `lopen sessions list` shows all sessions with timestamps
- `lopen sessions delete <id>` removes a session
- Session ID displayed after each chat interaction

---

## Implementation Notes

See [RESEARCH.md](RESEARCH.md) for detailed implementation guidance.

### Dependencies

- `GitHub.Copilot.SDK` - Official SDK
- `Microsoft.Extensions.AI` - Tool definition pattern
- Copilot CLI must be installed separately

### SDK Communication

The SDK communicates with Copilot CLI via JSON-RPC. The SDK manages the CLI process lifecycle automatically.
