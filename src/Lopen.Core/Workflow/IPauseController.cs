namespace Lopen.Core.Workflow;

/// <summary>
/// Controls pause/resume of workflow execution.
/// The TUI toggles pause via Ctrl+P; the orchestrator awaits the gate before each step.
/// </summary>
public interface IPauseController
{
    /// <summary>Whether execution is currently paused.</summary>
    bool IsPaused { get; }

    /// <summary>Pauses workflow execution. The next WaitIfPausedAsync call will block.</summary>
    void Pause();

    /// <summary>Resumes workflow execution, releasing any blocked WaitIfPausedAsync calls.</summary>
    void Resume();

    /// <summary>Toggles between paused and resumed state.</summary>
    void Toggle();

    /// <summary>
    /// Blocks until execution is resumed (or cancellation is requested).
    /// Returns immediately if not paused.
    /// </summary>
    Task WaitIfPausedAsync(CancellationToken cancellationToken = default);
}
