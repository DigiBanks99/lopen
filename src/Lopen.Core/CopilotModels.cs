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
}

/// <summary>
/// Information about an existing session.
/// </summary>
public record CopilotSessionInfo(
    string SessionId,
    DateTime StartTime,
    DateTime ModifiedTime,
    string? Summary = null);
