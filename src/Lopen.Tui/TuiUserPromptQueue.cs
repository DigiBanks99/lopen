using System.Threading.Channels;
using Lopen.Core;

namespace Lopen.Tui;

/// <summary>
/// Thread-safe prompt queue bridging TUI input to the orchestrator.
/// Uses a Channel&lt;string&gt; for lock-free async communication.
/// </summary>
public sealed class TuiUserPromptQueue : IUserPromptQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private int _count;

    public void Enqueue(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (_channel.Writer.TryWrite(prompt))
            Interlocked.Increment(ref _count);
    }

    public bool TryDequeue(out string prompt)
    {
        if (_channel.Reader.TryRead(out prompt!))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }
        return false;
    }

    public async Task<string> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var result = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _count);
        return result;
    }

    public int Count => Volatile.Read(ref _count);
}
