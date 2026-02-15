namespace Lopen.Tui;

/// <summary>
/// Main TUI application lifecycle interface.
/// Manages starting and stopping the terminal user interface.
/// </summary>
public interface ITuiApplication
{
    /// <summary>
    /// Starts the TUI application, entering the rendering loop.
    /// </summary>
    /// <param name="cancellationToken">Token to signal application shutdown.</param>
    Task RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests graceful shutdown of the TUI application.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Whether the TUI is currently running.
    /// </summary>
    bool IsRunning { get; }
}
