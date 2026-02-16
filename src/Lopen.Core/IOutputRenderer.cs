namespace Lopen.Core;

/// <summary>
/// Abstracts output rendering for both headless (plain text) and TUI modes.
/// Commands use this interface instead of writing to Console directly.
/// </summary>
public interface IOutputRenderer
{
    /// <summary>
    /// Renders a progress update (phase/step transitions, task completions).
    /// </summary>
    Task RenderProgressAsync(string phase, string step, double progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders an error message.
    /// </summary>
    Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a result or informational message.
    /// </summary>
    Task RenderResultAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prompts for user input. Returns null in headless mode (non-interactive).
    /// </summary>
    Task<string?> PromptAsync(string message, CancellationToken cancellationToken = default);
}
