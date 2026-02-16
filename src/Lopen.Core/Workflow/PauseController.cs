namespace Lopen.Core.Workflow;

/// <summary>
/// Thread-safe pause controller using SemaphoreSlim for async wait support.
/// </summary>
internal sealed class PauseController : IPauseController
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lock = new();
    private volatile bool _isPaused;

    public bool IsPaused => _isPaused;

    public void Pause()
    {
        lock (_lock)
        {
            if (_isPaused)
                return;

            _isPaused = true;
            _gate.Wait(TimeSpan.Zero);
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (!_isPaused)
                return;

            _isPaused = false;
            _gate.Release();
        }
    }

    public void Toggle()
    {
        lock (_lock)
        {
            if (_isPaused)
            {
                _isPaused = false;
                _gate.Release();
            }
            else
            {
                _isPaused = true;
                _gate.Wait(TimeSpan.Zero);
            }
        }
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
    {
        if (!_isPaused)
            return;

        // Wait for the gate to be released (Resume called)
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _gate.Release(); // Immediately release so the gate remains open
    }
}
