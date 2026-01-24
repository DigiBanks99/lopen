using System.Runtime.CompilerServices;

namespace Lopen.Core;

/// <summary>
/// Mock Copilot session for testing.
/// </summary>
public class MockCopilotSession : ICopilotSession
{
    private readonly string _sessionId;
    private readonly Func<string, IAsyncEnumerable<string>>? _streamHandler;
    private readonly Func<string, Task<string?>>? _sendHandler;
    private bool _aborted;
    private bool _disposed;

    /// <summary>
    /// Creates a mock session with default behavior.
    /// </summary>
    public MockCopilotSession(string sessionId = "mock-session")
    {
        _sessionId = sessionId;
    }

    /// <summary>
    /// Creates a mock session with custom handlers.
    /// </summary>
    public MockCopilotSession(
        string sessionId,
        Func<string, IAsyncEnumerable<string>>? streamHandler,
        Func<string, Task<string?>>? sendHandler)
    {
        _sessionId = sessionId;
        _streamHandler = streamHandler;
        _sendHandler = sendHandler;
    }

    /// <inheritdoc />
    public string SessionId => _sessionId;

    /// <summary>
    /// Whether AbortAsync was called.
    /// </summary>
    public bool WasAborted => _aborted;

    /// <summary>
    /// Whether DisposeAsync was called.
    /// </summary>
    public bool WasDisposed => _disposed;

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        if (_streamHandler != null)
        {
            await foreach (var chunk in _streamHandler(prompt).WithCancellation(ct))
            {
                yield return chunk;
            }
            yield break;
        }

        // Default mock behavior
        yield return "Hello";
        await Task.Delay(1, ct);
        yield return " from ";
        await Task.Delay(1, ct);
        yield return "mock!";
    }

    /// <inheritdoc />
    public Task<string?> SendAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        if (_sendHandler != null)
            return _sendHandler(prompt);

        return Task.FromResult<string?>("Hello from mock!");
    }

    /// <inheritdoc />
    public Task AbortAsync(CancellationToken ct = default)
    {
        _aborted = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
