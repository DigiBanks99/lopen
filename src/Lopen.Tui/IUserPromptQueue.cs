namespace Lopen.Tui;

/// <summary>
/// Thread-safe queue for user prompts submitted from the TUI prompt area.
/// The TUI enqueues prompts; the orchestrator or command handler dequeues them.
/// </summary>
public interface IUserPromptQueue
{
    /// <summary>
    /// Enqueues a user prompt for processing.
    /// </summary>
    void Enqueue(string prompt);

    /// <summary>
    /// Tries to dequeue the next prompt. Returns false if the queue is empty.
    /// </summary>
    bool TryDequeue(out string prompt);

    /// <summary>
    /// Waits asynchronously until a prompt is available, then returns it.
    /// </summary>
    Task<string> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of prompts currently in the queue.
    /// </summary>
    int Count { get; }
}
