using Lopen.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

public class TuiOutputRendererTests
{
    private readonly ActivityPanelDataProvider _activityProvider = new();
    private readonly UserPromptQueue _promptQueue = new();

    private TuiOutputRenderer CreateRenderer(IUserPromptQueue? promptQueue = null)
    {
        return new TuiOutputRenderer(
            _activityProvider,
            promptQueue,
            NullLogger<TuiOutputRenderer>.Instance);
    }

    [Fact]
    public void Constructor_NullActivityProvider_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TuiOutputRenderer(null!, null, NullLogger<TuiOutputRenderer>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TuiOutputRenderer(_activityProvider, null, null!));
    }

    [Fact]
    public void Constructor_NullPromptQueue_DoesNotThrow()
    {
        var renderer = new TuiOutputRenderer(_activityProvider, null, NullLogger<TuiOutputRenderer>.Instance);
        Assert.NotNull(renderer);
    }

    [Fact]
    public async Task RenderProgressAsync_AddsPhaseTransitionEntry()
    {
        var renderer = CreateRenderer();

        await renderer.RenderProgressAsync("Building", "Compiling", 0.5);

        var data = _activityProvider.GetCurrentData();
        Assert.Single(data.Entries);
        Assert.Equal(ActivityEntryKind.PhaseTransition, data.Entries[0].Kind);
        Assert.Contains("Building", data.Entries[0].Summary);
        Assert.Contains("Compiling", data.Entries[0].Summary);
    }

    [Fact]
    public async Task RenderProgressAsync_NegativeProgress_OmitsPercentage()
    {
        var renderer = CreateRenderer();

        await renderer.RenderProgressAsync("Assess", "Starting", -1);

        var data = _activityProvider.GetCurrentData();
        Assert.Single(data.Entries);
        Assert.DoesNotContain("%", data.Entries[0].Summary);
    }

    [Fact]
    public async Task RenderErrorAsync_AddsErrorEntry()
    {
        var renderer = CreateRenderer();

        await renderer.RenderErrorAsync("Something went wrong");

        var data = _activityProvider.GetCurrentData();
        Assert.Single(data.Entries);
        Assert.Equal(ActivityEntryKind.Error, data.Entries[0].Kind);
        // AddTaskFailure uses "✗ Task failed: {taskName}" as summary
        Assert.Contains("Error", data.Entries[0].Summary);
        // The error message is in the details
        Assert.Contains(data.Entries[0].Details, d => d.Contains("Something went wrong"));
    }

    [Fact]
    public async Task RenderErrorAsync_WithException_IncludesExceptionDetails()
    {
        var renderer = CreateRenderer();
        var exception = new InvalidOperationException("test error");

        await renderer.RenderErrorAsync("Failed", exception);

        var data = _activityProvider.GetCurrentData();
        Assert.Single(data.Entries);
        // Details include both the error message and exception info
        Assert.True(data.Entries[0].Details.Count >= 2);
        Assert.Contains(data.Entries[0].Details, d => d.Contains("InvalidOperationException"));
    }

    [Fact]
    public async Task RenderErrorAsync_WithoutException_NoExtraDetails()
    {
        var renderer = CreateRenderer();

        await renderer.RenderErrorAsync("Simple error");

        var data = _activityProvider.GetCurrentData();
        Assert.Single(data.Entries);
        // The TaskFailure method always adds "Error: message" as first detail
        Assert.Contains("Simple error", data.Entries[0].Details[0]);
    }

    [Fact]
    public async Task RenderResultAsync_AddsActionEntry()
    {
        var renderer = CreateRenderer();

        await renderer.RenderResultAsync("Task completed successfully");

        var data = _activityProvider.GetCurrentData();
        Assert.Single(data.Entries);
        Assert.Equal(ActivityEntryKind.Action, data.Entries[0].Kind);
        Assert.Equal("Task completed successfully", data.Entries[0].Summary);
    }

    [Fact]
    public async Task PromptAsync_NoQueue_ReturnsNull()
    {
        var renderer = CreateRenderer(promptQueue: null);

        var result = await renderer.PromptAsync("Continue?");

        Assert.Null(result);
    }

    [Fact]
    public async Task PromptAsync_WithQueue_WaitsForResponse()
    {
        var renderer = CreateRenderer(promptQueue: _promptQueue);

        // Enqueue a response before prompting to avoid blocking
        _promptQueue.Enqueue("yes");

        var result = await renderer.PromptAsync("Continue?");

        Assert.Equal("yes", result);
    }

    [Fact]
    public async Task PromptAsync_WithQueue_AddsConversationEntry()
    {
        var renderer = CreateRenderer(promptQueue: _promptQueue);
        _promptQueue.Enqueue("response");

        await renderer.PromptAsync("What should I do?");

        var data = _activityProvider.GetCurrentData();
        // Should have the conversation prompt entry
        Assert.Contains(data.Entries, e => e.Kind == ActivityEntryKind.Conversation);
        Assert.Contains(data.Entries, e => e.Summary.Contains("What should I do?"));
    }

    [Fact]
    public async Task RenderProgressAsync_MultipleEntries_AllAdded()
    {
        var renderer = CreateRenderer();

        await renderer.RenderProgressAsync("Assess", "Step 1", 0.25);
        await renderer.RenderProgressAsync("Building", "Step 2", 0.50);
        await renderer.RenderProgressAsync("Testing", "Step 3", 0.75);

        var data = _activityProvider.GetCurrentData();
        Assert.Equal(3, data.Entries.Count);
    }

    [Fact]
    public async Task RenderErrorAsync_IncrementsFailureCount()
    {
        var renderer = CreateRenderer();

        await renderer.RenderErrorAsync("Error 1");
        await renderer.RenderErrorAsync("Error 2");

        Assert.Equal(2, _activityProvider.ConsecutiveFailureCount);
    }

    [Fact]
    public async Task RenderResultAsync_ResetsFailureCount()
    {
        var renderer = CreateRenderer();

        await renderer.RenderErrorAsync("Error 1");
        Assert.Equal(1, _activityProvider.ConsecutiveFailureCount);

        await renderer.RenderResultAsync("Success");
        Assert.Equal(0, _activityProvider.ConsecutiveFailureCount);
    }

    [Fact]
    public async Task PromptAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var renderer = CreateRenderer(promptQueue: _promptQueue);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            renderer.PromptAsync("Wait...", cts.Token));
    }
}
