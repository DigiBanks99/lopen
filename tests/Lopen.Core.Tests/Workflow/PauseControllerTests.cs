namespace Lopen.Core.Tests.Workflow;

using Lopen.Core.Workflow;

/// <summary>
/// Tests for PauseController. Covers JOB-037 (TUI-41) acceptance criteria.
/// </summary>
public class PauseControllerTests
{
    // ==================== Initial State ====================

    [Fact]
    public void InitialState_IsNotPaused()
    {
        var controller = new PauseController();
        Assert.False(controller.IsPaused);
    }

    // ==================== Pause ====================

    [Fact]
    public void Pause_SetsIsPausedTrue()
    {
        var controller = new PauseController();
        controller.Pause();
        Assert.True(controller.IsPaused);
    }

    [Fact]
    public void Pause_WhenAlreadyPaused_RemainsIdempotent()
    {
        var controller = new PauseController();
        controller.Pause();
        controller.Pause(); // second call should not throw
        Assert.True(controller.IsPaused);
    }

    // ==================== Resume ====================

    [Fact]
    public void Resume_SetsIsPausedFalse()
    {
        var controller = new PauseController();
        controller.Pause();
        controller.Resume();
        Assert.False(controller.IsPaused);
    }

    [Fact]
    public void Resume_WhenNotPaused_RemainsIdempotent()
    {
        var controller = new PauseController();
        controller.Resume(); // should not throw
        Assert.False(controller.IsPaused);
    }

    // ==================== Toggle ====================

    [Fact]
    public void Toggle_FromNotPaused_Pauses()
    {
        var controller = new PauseController();
        controller.Toggle();
        Assert.True(controller.IsPaused);
    }

    [Fact]
    public void Toggle_FromPaused_Resumes()
    {
        var controller = new PauseController();
        controller.Pause();
        controller.Toggle();
        Assert.False(controller.IsPaused);
    }

    [Fact]
    public void Toggle_Twice_ReturnsToOriginalState()
    {
        var controller = new PauseController();
        controller.Toggle();
        controller.Toggle();
        Assert.False(controller.IsPaused);
    }

    // ==================== WaitIfPausedAsync ====================

    [Fact]
    public async Task WaitIfPausedAsync_WhenNotPaused_ReturnsImmediately()
    {
        var controller = new PauseController();
        var task = controller.WaitIfPausedAsync();
        await task.WaitAsync(TimeSpan.FromMilliseconds(100));
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitIfPausedAsync_WhenPaused_BlocksUntilResume()
    {
        var controller = new PauseController();
        controller.Pause();

        var waitTask = Task.Run(async () =>
        {
            await controller.WaitIfPausedAsync();
        });

        // Should not complete immediately
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        // Resume should unblock
        controller.Resume();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitIfPausedAsync_WhenPaused_CancellationThrows()
    {
        var controller = new PauseController();
        controller.Pause();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => controller.WaitIfPausedAsync(cts.Token));
    }

    [Fact]
    public async Task WaitIfPausedAsync_AfterPauseAndResume_ReturnsImmediately()
    {
        var controller = new PauseController();
        controller.Pause();
        controller.Resume();

        var task = controller.WaitIfPausedAsync();
        await task.WaitAsync(TimeSpan.FromMilliseconds(100));
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitIfPausedAsync_MultiplePauseResumeCycles_WorksCorrectly()
    {
        var controller = new PauseController();

        // Cycle 1
        controller.Pause();
        controller.Resume();
        await controller.WaitIfPausedAsync();

        // Cycle 2
        controller.Pause();
        var waitTask = Task.Run(async () => await controller.WaitIfPausedAsync());
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);
        controller.Resume();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    // ==================== Thread Safety ====================

    [Fact]
    public async Task ConcurrentToggle_DoesNotThrow()
    {
        var controller = new PauseController();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            controller.Toggle();
        }));

        await Task.WhenAll(tasks);
        // Just verify no exception — final state is indeterminate due to races
    }
}
