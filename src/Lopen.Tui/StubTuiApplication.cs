using Microsoft.Extensions.Logging;

namespace Lopen.Tui;

/// <summary>
/// No-op TUI application for headless mode and testing.
/// When --headless is active, this stub replaces the real TUI.
/// </summary>
internal sealed class StubTuiApplication(ILogger<StubTuiApplication> logger) : ITuiApplication
{
    public bool IsRunning { get; private set; }

    public Task RunAsync(string? initialPrompt = null, CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        InitialPrompt = initialPrompt;
        logger.LogDebug("StubTuiApplication started (headless mode)");
        return Task.CompletedTask;
    }

    /// <summary>Gets the initial prompt that was passed to RunAsync, for testing.</summary>
    public string? InitialPrompt { get; private set; }

    public Task StopAsync()
    {
        IsRunning = false;
        logger.LogDebug("StubTuiApplication stopped");
        return Task.CompletedTask;
    }
}
