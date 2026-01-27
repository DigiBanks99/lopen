using Microsoft.Extensions.AI;

namespace Lopen.Core;

/// <summary>
/// Authentication status for Copilot.
/// </summary>
public record CopilotAuthStatus(bool IsAuthenticated, string? AuthType = null, string? Login = null);

/// <summary>
/// Options for creating a Copilot session.
/// </summary>
public record CopilotSessionOptions
{
    /// <summary>
    /// Custom session ID for persistence.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Model to use (e.g., "gpt-5", "claude-sonnet-4.5").
    /// </summary>
    public string Model { get; init; } = "gpt-5";

    /// <summary>
    /// Enable streaming responses.
    /// </summary>
    public bool Streaming { get; init; } = true;

    /// <summary>
    /// Allow all tool operations without confirmation prompts.
    /// When true, the agent can execute file writes, shell commands, etc. automatically.
    /// </summary>
    public bool AllowAll { get; init; } = true;

    /// <summary>
    /// Custom tools to register with the session.
    /// </summary>
    public ICollection<AIFunction>? Tools { get; init; }

    /// <summary>
    /// Built-in tools to enable (e.g., "file_system", "git", "shell").
    /// </summary>
    public ICollection<string>? AvailableTools { get; init; }

    /// <summary>
    /// Built-in tools to disable.
    /// </summary>
    public ICollection<string>? ExcludedTools { get; init; }
}

/// <summary>
/// Information about an existing session.
/// </summary>
public record CopilotSessionInfo(
    string SessionId,
    DateTime StartTime,
    DateTime ModifiedTime,
    string? Summary = null);
