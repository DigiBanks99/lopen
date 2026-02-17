using Lopen.Core;
using Microsoft.Extensions.Logging;

namespace Lopen.Tui;

/// <summary>
/// TUI-mode output renderer that bridges orchestrator output events to the TUI activity panel.
/// Replaces <see cref="HeadlessRenderer"/> when TUI mode is active.
/// </summary>
public sealed class TuiOutputRenderer : IOutputRenderer
{
    private readonly IActivityPanelDataProvider _activityProvider;
    private readonly IUserPromptQueue? _promptQueue;
    private readonly ILogger<TuiOutputRenderer> _logger;

    public TuiOutputRenderer(
        IActivityPanelDataProvider activityProvider,
        IUserPromptQueue? promptQueue,
        ILogger<TuiOutputRenderer> logger)
    {
        _activityProvider = activityProvider ?? throw new ArgumentNullException(nameof(activityProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptQueue = promptQueue;
    }

    public Task RenderProgressAsync(string phase, string step, double progress, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Progress: [{Phase}] {Step} ({Progress:P0})", phase, step, progress);

        var pct = progress >= 0 ? $" ({progress:P0})" : "";
        var entry = new ActivityEntry
        {
            Summary = $"[{phase}] {step}{pct}",
            Kind = ActivityEntryKind.PhaseTransition,
        };

        _activityProvider.AddEntry(entry);
        return Task.CompletedTask;
    }

    public Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Error rendered: {Message}", message);

        var details = new List<string>();
        if (exception is not null)
        {
            details.Add($"{exception.GetType().Name}: {exception.Message}");
        }

        _activityProvider.AddTaskFailure("Error", message, details.Count > 0 ? details : null);
        return Task.CompletedTask;
    }

    public Task RenderResultAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Result rendered: {Message}", message);

        var entry = new ActivityEntry
        {
            Summary = message,
            Kind = ActivityEntryKind.Action,
        };

        _activityProvider.AddEntry(entry);
        return Task.CompletedTask;
    }

    public async Task<string?> PromptAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_promptQueue is null)
        {
            _logger.LogDebug("PromptAsync called but no prompt queue available; returning null");
            return null;
        }

        _logger.LogDebug("Prompting user: {Message}", message);

        // Add a conversation entry to show the prompt in the activity panel
        _activityProvider.AddEntry(new ActivityEntry
        {
            Summary = $"⏳ {message}",
            Kind = ActivityEntryKind.Conversation,
        });

        // Wait for the user to respond via the TUI prompt area
        var response = await _promptQueue.DequeueAsync(cancellationToken);
        return response;
    }
}
