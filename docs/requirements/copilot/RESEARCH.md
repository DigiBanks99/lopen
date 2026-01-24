# Copilot SDK Research

> Research for REQ-020 through REQ-024: GitHub Copilot SDK Integration
> Last validated: 2026-01-24
> SDK Version: 0.1.17 (verified from NuGet + source)

## Key Finding

**GitHub Copilot SDK for .NET is now available!**

```bash
dotnet add package GitHub.Copilot.SDK --version 0.1.17
```

This is the official SDK that provides the same engine behind Copilot CLI as a programmable API.

## Prerequisites

### 1. Copilot CLI Installation
```bash
# Install Copilot CLI (required - SDK communicates with it via JSON-RPC)
# Follow: https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli
copilot --version
```

### 2. Authentication
Users must authenticate with Copilot CLI:
```bash
copilot auth login
```

### 3. Subscription
GitHub Copilot subscription required (free tier available with limited usage).

---

## SDK Architecture

```
lopen CLI (System.CommandLine)
       ↓
  ICopilotService (Lopen.Core interface)
       ↓
  CopilotService (Lopen.Core wrapper)
       ↓
  GitHub.Copilot.SDK (CopilotClient + CopilotSession)
       ↓ JSON-RPC over stdio
  Copilot CLI (server mode - auto-managed)
```

The SDK automatically manages the Copilot CLI process lifecycle via `CopilotClient`.

---

## Verified API Patterns (from SDK Source)

### CopilotClient
```csharp
// Main client class - manages CLI process and sessions
public partial class CopilotClient : IDisposable, IAsyncDisposable
{
    public CopilotClient(CopilotClientOptions? options = null);
    public ConnectionState State { get; }  // Disconnected, Connecting, Connected, Error
    
    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync();
    public Task ForceStopAsync();
    
    public Task<CopilotSession> CreateSessionAsync(SessionConfig? config = null, CancellationToken ct = default);
    public Task<CopilotSession> ResumeSessionAsync(string sessionId, ResumeSessionConfig? config = null, CancellationToken ct = default);
    
    public Task<List<SessionMetadata>> ListSessionsAsync(CancellationToken ct = default);
    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
    
    public Task<PingResponse> PingAsync(string? message = null, CancellationToken ct = default);
    public Task<GetStatusResponse> GetStatusAsync(CancellationToken ct = default);
    public Task<GetAuthStatusResponse> GetAuthStatusAsync(CancellationToken ct = default);
    public Task<GetModelsResponse> GetModelsAsync(CancellationToken ct = default);
}
```

### CopilotClientOptions
```csharp
public class CopilotClientOptions
{
    public string? CliPath { get; set; }           // Default: "copilot" from PATH
    public string[]? CliArgs { get; set; }         // Extra args for CLI
    public string? Cwd { get; set; }               // Working directory
    public int Port { get; set; }                  // Default: 0 (random)
    public bool UseStdio { get; set; } = true;     // stdio vs TCP transport
    public string? CliUrl { get; set; }            // Connect to existing server
    public string LogLevel { get; set; } = "info"; // CLI log level
    public bool AutoStart { get; set; } = true;    // Auto-start on first call
    public bool AutoRestart { get; set; } = true;  // Restart on crash
    public IReadOnlyDictionary<string, string>? Environment { get; set; };
    public ILogger? Logger { get; set; };          // For SDK logging
}
```

### CopilotSession
```csharp
public partial class CopilotSession : IAsyncDisposable
{
    public string SessionId { get; }
    
    // Fire-and-forget send
    public Task<string> SendAsync(MessageOptions options, CancellationToken ct = default);
    
    // Send and block until session.idle
    public Task<AssistantMessageEvent?> SendAndWaitAsync(
        MessageOptions options, 
        TimeSpan? timeout = null,  // Default: 60 seconds
        CancellationToken ct = default);
    
    // Subscribe to events
    public IDisposable On(SessionEventHandler handler);
    
    // Abort current message
    public Task AbortAsync(CancellationToken ct = default);
    
    // Get conversation history
    public Task<IReadOnlyList<SessionEvent>> GetMessagesAsync(CancellationToken ct = default);
}
```

### SessionConfig
```csharp
public class SessionConfig
{
    public string? SessionId { get; set; }           // Custom ID for persistence
    public string? Model { get; set; }               // "gpt-5", "claude-sonnet-4.5", etc.
    public string? ConfigDir { get; set; }           // Override config directory
    public ICollection<AIFunction>? Tools { get; set; }
    public SystemMessageConfig? SystemMessage { get; set; }
    public List<string>? AvailableTools { get; set; }
    public List<string>? ExcludedTools { get; set; }
    public ProviderConfig? Provider { get; set; }     // BYOK configuration
    public PermissionHandler? OnPermissionRequest { get; set; };
    public bool Streaming { get; set; }               // Enable delta events
    public Dictionary<string, object>? McpServers { get; set; };
    public List<CustomAgentConfig>? CustomAgents { get; set; };
    public List<string>? SkillDirectories { get; set; };
    public List<string>? DisabledSkills { get; set; };
}
```

### MessageOptions
```csharp
public class MessageOptions
{
    public string Prompt { get; set; } = string.Empty;
    public List<UserMessageDataAttachmentsItem>? Attachments { get; set; };
    public string? Mode { get; set; };  // "enqueue" or "immediate"
}
```

---

## Event Types (from Generated/SessionEvents.cs)

All events inherit from `SessionEvent` base class:

| Event Class | Type Discriminator | Purpose |
|-------------|-------------------|---------|
| `AssistantMessageEvent` | `assistant.message` | Complete assistant response |
| `AssistantMessageDeltaEvent` | `assistant.message_delta` | Streaming chunk |
| `AssistantReasoningEvent` | `assistant.reasoning` | Reasoning content |
| `AssistantReasoningDeltaEvent` | `assistant.reasoning_delta` | Streaming reasoning |
| `SessionIdleEvent` | `session.idle` | Response complete |
| `SessionErrorEvent` | `session.error` | Error occurred |
| `SessionStartEvent` | `session.start` | Session created |
| `SessionResumeEvent` | `session.resume` | Session resumed |
| `ToolExecutionStartEvent` | `tool.execution_start` | Tool invocation started |
| `ToolExecutionCompleteEvent` | `tool.execution_complete` | Tool finished |
| `ToolExecutionProgressEvent` | `tool.execution_progress` | Tool progress update |
| `UserMessageEvent` | `user.message` | User message logged |
| `SubagentStartedEvent` | `subagent.started` | Sub-agent spawned |
| `SubagentCompletedEvent` | `subagent.completed` | Sub-agent finished |

### Streaming Delta Structure
```csharp
public partial class AssistantMessageDeltaData
{
    public required string MessageId { get; set; }
    public required string DeltaContent { get; set; }  // The actual chunk!
    public double? TotalResponseSizeBytes { get; set; }
    public string? ParentToolCallId { get; set; }
}
```

---

## Error Handling Patterns

The SDK uses standard .NET exceptions. **No custom exception types** like `CopilotCliNotFoundException` are defined - these are standard exceptions:

### Connection Errors
```csharp
try
{
    await using var client = new CopilotClient();
    await client.StartAsync();
}
catch (FileNotFoundException)
{
    // Copilot CLI not found in PATH
    Console.Error.WriteLine("Please install Copilot CLI: https://docs.github.com/en/copilot");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("not connected"))
{
    // Connection failed
    Console.Error.WriteLine("Failed to connect to Copilot CLI");
}
```

### Session Errors
```csharp
try
{
    var response = await session.SendAndWaitAsync(options);
}
catch (TimeoutException)
{
    // SendAndWaitAsync exceeded timeout (default 60s)
    Console.Error.WriteLine("Request timed out");
}
catch (InvalidOperationException ex)
{
    // Session disposed, error during processing
    Console.Error.WriteLine($"Session error: {ex.Message}");
}
```

### Event-Based Error Handling
```csharp
session.On(evt =>
{
    if (evt is SessionErrorEvent errorEvent)
    {
        Console.Error.WriteLine($"Error: {errorEvent.Data.Message}");
    }
});
```

### Authentication Check
```csharp
var authStatus = await client.GetAuthStatusAsync();
if (!authStatus.IsAuthenticated)
{
    Console.Error.WriteLine("Not authenticated. Run 'copilot auth login' first.");
    return;
}
```

---

## Cancellation (Ctrl+C) Handling

### Using CancellationToken
```csharp
// Create linked token for graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;  // Prevent immediate termination
    cts.Cancel();
};

try
{
    await session.SendAndWaitAsync(options, cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    // User pressed Ctrl+C - abort in-flight request
    await session.AbortAsync();
}
```

### Aborting Mid-Request
```csharp
// AbortAsync() cancels the current message without destroying session
await session.AbortAsync();

// Session remains valid for new messages
await session.SendAsync(new MessageOptions { Prompt = "New question" });
```

### Graceful Cleanup
```csharp
await using var client = new CopilotClient();
// ... use client ...

// StopAsync() gracefully closes all sessions first
await client.StopAsync();

// ForceStopAsync() for immediate shutdown if StopAsync hangs
// await client.ForceStopAsync();
```

---

## ICopilotService Interface Design for Lopen.Core

```csharp
namespace Lopen.Core;

/// <summary>
/// Service for interacting with GitHub Copilot.
/// </summary>
public interface ICopilotService : IAsyncDisposable
{
    /// <summary>
    /// Whether Copilot CLI is available and authenticated.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get authentication status.
    /// </summary>
    Task<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get available models.
    /// </summary>
    Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Create a new chat session.
    /// </summary>
    Task<ICopilotSession> CreateSessionAsync(CopilotSessionOptions? options = null, CancellationToken ct = default);
    
    /// <summary>
    /// Resume an existing session by ID.
    /// </summary>
    Task<ICopilotSession> ResumeSessionAsync(string sessionId, CancellationToken ct = default);
    
    /// <summary>
    /// List available sessions.
    /// </summary>
    Task<IReadOnlyList<CopilotSessionInfo>> ListSessionsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Delete a session.
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>
/// A single chat session with Copilot.
/// </summary>
public interface ICopilotSession : IAsyncDisposable
{
    /// <summary>
    /// Session identifier.
    /// </summary>
    string SessionId { get; }
    
    /// <summary>
    /// Send a message and get streaming response chunks.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct = default);
    
    /// <summary>
    /// Send a message and wait for complete response.
    /// </summary>
    Task<string?> SendAsync(string prompt, CancellationToken ct = default);
    
    /// <summary>
    /// Abort the current message.
    /// </summary>
    Task AbortAsync(CancellationToken ct = default);
}

/// <summary>
/// Session creation options.
/// </summary>
public record CopilotSessionOptions
{
    public string? SessionId { get; init; }
    public string Model { get; init; } = "gpt-5";
    public bool Streaming { get; init; } = true;
}

/// <summary>
/// Authentication status.
/// </summary>
public record CopilotAuthStatus(bool IsAuthenticated, string? AuthType, string? Login);

/// <summary>
/// Session metadata.
/// </summary>
public record CopilotSessionInfo(string SessionId, DateTime StartTime, DateTime ModifiedTime, string? Summary);
```

---

## Chat Command Implementation for Lopen.Cli

```csharp
// In Program.cs - add chat command
var chatCommand = new Command("chat", "Start AI chat session");

var modelOption = new Option<string>("--model", () => "gpt-5", "AI model to use");
modelOption.Aliases.Add("-m");

var promptArg = new Argument<string?>("prompt", () => null, "Single query (optional)");

chatCommand.Options.Add(modelOption);
chatCommand.Arguments.Add(promptArg);

chatCommand.SetAction(async parseResult =>
{
    var model = parseResult.GetValue(modelOption);
    var prompt = parseResult.GetValue(promptArg);
    
    // Check Copilot availability
    await using var copilotService = new CopilotService();
    
    var authStatus = await copilotService.GetAuthStatusAsync();
    if (!authStatus.IsAuthenticated)
    {
        output.Error("Not authenticated. Run 'copilot auth login' first.");
        return 1;
    }
    
    await using var session = await copilotService.CreateSessionAsync(new CopilotSessionOptions
    {
        Model = model,
        Streaming = true
    });
    
    // Single query mode
    if (!string.IsNullOrEmpty(prompt))
    {
        await foreach (var chunk in session.StreamAsync(prompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();
        return 0;
    }
    
    // Interactive mode
    output.Info($"Chat session started (model: {model}). Type 'exit' to quit.");
    
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    while (!cts.IsCancellationRequested)
    {
        output.Write("You: ");
        var input = Console.ReadLine();
        
        if (input is null or "exit" or "quit")
            break;
        
        if (string.IsNullOrWhiteSpace(input))
            continue;
        
        output.Write("AI: ");
        try
        {
            await foreach (var chunk in session.StreamAsync(input, cts.Token))
            {
                Console.Write(chunk);
            }
            Console.WriteLine();
        }
        catch (OperationCanceledException)
        {
            await session.AbortAsync();
            Console.WriteLine("\n[Aborted]");
        }
    }
    
    output.Info("Goodbye!");
    return 0;
});

rootCommand.Subcommands.Add(chatCommand);
```

---

## Streaming Response Implementation (REQ-022)

```csharp
public class CopilotSession : ICopilotSession
{
    private readonly GitHub.Copilot.SDK.CopilotSession _session;
    
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<string>();
        var done = new TaskCompletionSource();
        
        using var subscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    channel.Writer.TryWrite(delta.Data.DeltaContent);
                    break;
                    
                case SessionIdleEvent:
                    channel.Writer.Complete();
                    done.TrySetResult();
                    break;
                    
                case SessionErrorEvent error:
                    var ex = new InvalidOperationException(error.Data.Message);
                    channel.Writer.Complete(ex);
                    done.TrySetException(ex);
                    break;
            }
        });
        
        // Send the message
        await _session.SendAsync(new MessageOptions { Prompt = prompt }, ct);
        
        // Yield chunks as they arrive
        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
        {
            yield return chunk;
        }
        
        await done.Task;
    }
}
```

---

## REPL Integration

The chat command can integrate with `ReplService` for consistent input handling:

```csharp
// Option 1: Chat as a REPL command (within lopen repl)
// The existing REPL can dispatch to chat:
autoCompleter.RegisterCommand("chat", "AI chat mode", options: ["--model", "-m"]);

// Option 2: Standalone chat REPL
// Use ConsoleInputWithHistory for familiar line editing:
var history = new PersistentCommandHistory("~/.lopen/chat-history");
var consoleInput = new ConsoleInputWithHistory(history);

while (!cts.IsCancellationRequested)
{
    output.Write("You: ");
    var input = consoleInput.ReadLine();
    // ... process with copilot session
}
```

---

## Testing Strategy

### Unit Tests (Mock ICopilotService)

```csharp
public class MockCopilotService : ICopilotService
{
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);
    
    public Task<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken ct) 
        => Task.FromResult(new CopilotAuthStatus(true, "user", "testuser"));
    
    public Task<ICopilotSession> CreateSessionAsync(CopilotSessionOptions? options, CancellationToken ct)
        => Task.FromResult<ICopilotSession>(new MockCopilotSession());
    
    // ... other methods
}

public class MockCopilotSession : ICopilotSession
{
    public string SessionId => "mock-session";
    
    public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken ct)
    {
        // Simulate streaming response
        yield return "Hello";
        await Task.Delay(10, ct);
        yield return " from ";
        await Task.Delay(10, ct);
        yield return "mock!";
    }
    
    public Task<string?> SendAsync(string prompt, CancellationToken ct)
        => Task.FromResult<string?>("Hello from mock!");
    
    public Task AbortAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### Test Examples
```csharp
[Fact]
public async Task StreamAsync_YieldsChunks()
{
    var session = new MockCopilotSession();
    var chunks = new List<string>();
    
    await foreach (var chunk in session.StreamAsync("test"))
    {
        chunks.Add(chunk);
    }
    
    chunks.Should().BeEquivalentTo(["Hello", " from ", "mock!"]);
}

[Fact]
public async Task CreateSession_WhenNotAuthenticated_ThrowsOrReturnsError()
{
    var service = new MockCopilotService { IsAuthenticated = false };
    
    var status = await service.GetAuthStatusAsync();
    
    status.IsAuthenticated.Should().BeFalse();
}
```

### Integration Tests
```csharp
[Trait("Category", "Integration")]
[Trait("Requires", "CopilotCli")]
public class CopilotIntegrationTests
{
    [SkippableFact]
    public async Task CreateSession_WithRealCli_Works()
    {
        // Skip if CLI not available
        var cli = Process.Start("copilot", "--version");
        Skip.If(cli == null, "Copilot CLI not installed");
        
        await using var client = new CopilotClient();
        var status = await client.GetAuthStatusAsync();
        Skip.IfNot(status.IsAuthenticated, "Not authenticated");
        
        await using var session = await client.CreateSessionAsync();
        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = "Say 'test'" });
        
        response.Should().NotBeNull();
    }
}
```

---

## Package References

```xml
<!-- Lopen.Core.csproj -->
<ItemGroup>
  <PackageReference Include="GitHub.Copilot.SDK" Version="0.1.17" />
  <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="9.5.0" />
</ItemGroup>
```

---

## Available Models (via GetModelsAsync)

```csharp
var models = await client.GetModelsAsync();
// Returns ModelInfo with id, name, capabilities, billing multiplier
```

Common models:
- `gpt-5` - Default, balanced
- `gpt-5.1` - Latest GPT
- `claude-sonnet-4.5` - Anthropic
- `gemini-3-pro-preview` - Google

---

## Custom Tools (REQ-023)

### AIFunctionFactory Pattern

```csharp
using Microsoft.Extensions.AI;
using System.ComponentModel;

// Simple lambda tool
var readFile = AIFunctionFactory.Create(
    ([Description("File path")] string path) => File.ReadAllText(path),
    "read_file",
    "Read file contents"
);

// Static method tool
[Description("Execute shell command")]
public static async Task<string> RunCommand(
    [Description("Command to run")] string command,
    CancellationToken ct = default)
{
    var psi = new ProcessStartInfo("bash", $"-c \"{command}\"")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    using var proc = Process.Start(psi)!;
    await proc.WaitForExitAsync(ct);
    return await proc.StandardOutput.ReadToEndAsync(ct);
}
var shellTool = AIFunctionFactory.Create(RunCommand);
```

### Tool Registration

```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5",
    Tools = [readFile, shellTool],
    // Or use built-in tools:
    AvailableTools = ["file_system", "git", "shell"],
    ExcludedTools = ["web"]  // Disable specific tools
});
```

### Built-in Tools

| Tool Name | Description |
|-----------|-------------|
| `file_system` | Read, write, list files |
| `git` | Status, diff, commit, log |
| `shell` | Execute shell commands |
| `web` | HTTP requests |

### Error Handling in Tools

```csharp
var safeTool = AIFunctionFactory.Create(
    (string path) =>
    {
        if (!File.Exists(path))
            return new { error = $"File not found: {path}" };
        return new { content = File.ReadAllText(path) };
    },
    "read_file",
    "Read file contents safely"
);
```

### Recommended Lopen Tools (Priority Order)

1. **lopen_config** - Get/set Lopen preferences
2. **lopen_history** - Access command history
3. **lopen_session** - Manage session state
4. **lopen_context** - Add context from files/repos

---

## References

- [GitHub Copilot SDK Repository](https://github.com/github/copilot-sdk)
- [.NET SDK Source](https://github.com/github/copilot-sdk/tree/main/dotnet/src)
- [.NET Cookbook](https://github.com/github/copilot-sdk/tree/main/cookbook/dotnet)
- [Error Handling Guide](https://github.com/github/copilot-sdk/blob/main/cookbook/dotnet/error-handling.md)
- [Session Persistence](https://github.com/github/copilot-sdk/blob/main/cookbook/dotnet/persisting-sessions.md)
- [NuGet Package](https://www.nuget.org/packages/GitHub.Copilot.SDK)
- [Copilot CLI Installation](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli)
