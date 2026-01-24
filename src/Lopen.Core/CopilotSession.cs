using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GitHub.Copilot.SDK;

namespace Lopen.Core;

/// <summary>
/// Wraps a CopilotSession from the SDK with streaming support.
/// </summary>
public class CopilotSession : ICopilotSession
{
    private readonly GitHub.Copilot.SDK.CopilotSession _session;
    private bool _disposed;

    internal CopilotSession(GitHub.Copilot.SDK.CopilotSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <inheritdoc />
    public string SessionId => _session.SessionId;

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

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

    /// <inheritdoc />
    public async Task<string?> SendAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        var response = await _session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: null,
            cancellationToken: ct);

        return response?.Data.Content;
    }

    /// <inheritdoc />
    public Task AbortAsync(CancellationToken ct = default)
    {
        return _session.AbortAsync(ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _session.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
