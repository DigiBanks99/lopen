using System.Collections.Concurrent;
using Lopen.Core;

namespace Lopen.Tui;

/// <summary>
/// Thread-safe implementation of <see cref="IUserPromptQueue"/> using a
/// <see cref="ConcurrentQueue{T}"/> and <see cref="SemaphoreSlim"/> for async waiting.
/// </summary>
public sealed class UserPromptQueue : IUserPromptQueue
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _queue.Enqueue(prompt);
        _signal.Release();
    }

    public bool TryDequeue(out string prompt)
    {
        prompt = string.Empty;
        if (_queue.TryDequeue(out var value))
        {
            prompt = value;
            return true;
        }
        return false;
    }

    public async Task<string> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
        _queue.TryDequeue(out var prompt);
        return prompt!;
    }

    public int Count => _queue.Count;
}
