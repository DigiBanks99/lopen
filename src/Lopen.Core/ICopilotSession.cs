namespace Lopen.Core;

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
