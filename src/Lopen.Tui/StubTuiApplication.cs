using Microsoft.Extensions.Logging;

namespace Lopen.Tui;

/// <summary>
/// No-op TUI application for headless mode and testing.
/// When --headless is active, this stub replaces the real TUI.
/// </summary>
internal sealed class StubTuiApplication(ILogger<StubTuiApplication> logger) : ITuiApplication
{
    public bool IsRunning { get; private set; }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        logger.LogDebug("StubTuiApplication started (headless mode)");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        logger.LogDebug("StubTuiApplication stopped");
        return Task.CompletedTask;
    }
}
