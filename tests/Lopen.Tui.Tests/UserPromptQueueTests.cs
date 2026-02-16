using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for UserPromptQueue — thread-safe queue for user prompts
/// submitted from the TUI prompt area.
/// Covers JOB-041 (TUI-05) acceptance criteria.
/// </summary>
public class UserPromptQueueTests
{
    // ==================== Enqueue / TryDequeue ====================

    [Fact]
    public void Enqueue_ThenTryDequeue_ReturnsPrompt()
    {
        var queue = new UserPromptQueue();
        queue.Enqueue("hello world");

        Assert.True(queue.TryDequeue(out var prompt));
        Assert.Equal("hello world", prompt);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new UserPromptQueue();
        Assert.False(queue.TryDequeue(out var prompt));
        Assert.Equal(string.Empty, prompt);
    }

    [Fact]
    public void Enqueue_ThrowsOnNull()
    {
        var queue = new UserPromptQueue();
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }

    [Fact]
    public void Enqueue_MultipleThenDequeue_PreservesOrder()
    {
        var queue = new UserPromptQueue();
        queue.Enqueue("first");
        queue.Enqueue("second");
        queue.Enqueue("third");

        Assert.True(queue.TryDequeue(out var p1));
        Assert.Equal("first", p1);
        Assert.True(queue.TryDequeue(out var p2));
        Assert.Equal("second", p2);
        Assert.True(queue.TryDequeue(out var p3));
        Assert.Equal("third", p3);
        Assert.False(queue.TryDequeue(out _));
    }

    // ==================== Count ====================

    [Fact]
    public void Count_ReflectsQueueSize()
    {
        var queue = new UserPromptQueue();
        Assert.Equal(0, queue.Count);

        queue.Enqueue("a");
        Assert.Equal(1, queue.Count);

        queue.Enqueue("b");
        Assert.Equal(2, queue.Count);

        queue.TryDequeue(out _);
        Assert.Equal(1, queue.Count);
    }

    // ==================== DequeueAsync ====================

    [Fact]
    public async Task DequeueAsync_WaitsUntilEnqueued()
    {
        var queue = new UserPromptQueue();
        var dequeueTask = queue.DequeueAsync();

        Assert.False(dequeueTask.IsCompleted);

        queue.Enqueue("delayed prompt");

        var result = await dequeueTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("delayed prompt", result);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsImmediatelyWhenAvailable()
    {
        var queue = new UserPromptQueue();
        queue.Enqueue("ready");

        var result = await queue.DequeueAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal("ready", result);
    }

    [Fact]
    public async Task DequeueAsync_CancellationThrows()
    {
        var queue = new UserPromptQueue();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => queue.DequeueAsync(cts.Token));
    }

    // ==================== Thread Safety ====================

    [Fact]
    public async Task ConcurrentEnqueueAndDequeue_NoDataLoss()
    {
        var queue = new UserPromptQueue();
        const int count = 100;
        var dequeued = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Enqueue from multiple threads
        var enqueueTasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(() => queue.Enqueue($"item-{i}")));
        await Task.WhenAll(enqueueTasks);

        Assert.Equal(count, queue.Count);

        // Dequeue from multiple threads
        var dequeueTasks = Enumerable.Range(0, count)
            .Select(_ => Task.Run(() =>
            {
                if (queue.TryDequeue(out var p))
                    dequeued.Add(p);
            }));
        await Task.WhenAll(dequeueTasks);

        Assert.Equal(count, dequeued.Count);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task DequeueAsync_MultipleWaiters_EachGetsOne()
    {
        var queue = new UserPromptQueue();

        var task1 = queue.DequeueAsync();
        var task2 = queue.DequeueAsync();

        queue.Enqueue("first");
        queue.Enqueue("second");

        var results = await Task.WhenAll(
            task1.WaitAsync(TimeSpan.FromSeconds(2)),
            task2.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Contains("first", results);
        Assert.Contains("second", results);
    }

    // ==================== Empty String ====================

    [Fact]
    public void Enqueue_EmptyString_IsAllowed()
    {
        var queue = new UserPromptQueue();
        queue.Enqueue(string.Empty);

        Assert.True(queue.TryDequeue(out var prompt));
        Assert.Equal(string.Empty, prompt);
    }
}
