using Lopen.Core;

namespace Lopen.Tui.Tests;

public class TuiUserPromptQueueTests
{
    [Fact]
    public void NewQueue_HasZeroCount()
    {
        TuiUserPromptQueue queue = new();
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Enqueue_IncrementsCount()
    {
        TuiUserPromptQueue queue = new();
        queue.Enqueue("hello");
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void TryDequeue_ReturnsTrueWhenItemAvailable()
    {
        TuiUserPromptQueue queue = new();
        queue.Enqueue("hello");

        bool result = queue.TryDequeue(out string prompt);
        Assert.True(result);
        Assert.Equal("hello", prompt);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void TryDequeue_ReturnsFalseWhenEmpty()
    {
        TuiUserPromptQueue queue = new();

        bool result = queue.TryDequeue(out _);
        Assert.False(result);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsEnqueuedItem()
    {
        TuiUserPromptQueue queue = new();
        queue.Enqueue("async test");

        string result = await queue.DequeueAsync();
        Assert.Equal("async test", result);
    }

    [Fact]
    public async Task DequeueAsync_WaitsForItem()
    {
        TuiUserPromptQueue queue = new();

        Task<string> dequeueTask = queue.DequeueAsync(CancellationToken.None);
        Assert.False(dequeueTask.IsCompleted);

        queue.Enqueue("delayed");
        string result = await dequeueTask;
        Assert.Equal("delayed", result);
    }

    [Fact]
    public async Task DequeueAsync_ThrowsOnCancellation()
    {
        TuiUserPromptQueue queue = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => queue.DequeueAsync(cts.Token));
    }

    [Fact]
    public void Enqueue_ThrowsOnNull()
    {
        TuiUserPromptQueue queue = new();
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }

    [Fact]
    public void MultipleEnqueueDequeue_MaintainsOrder()
    {
        TuiUserPromptQueue queue = new();
        queue.Enqueue("first");
        queue.Enqueue("second");
        queue.Enqueue("third");

        queue.TryDequeue(out string p1);
        queue.TryDequeue(out string p2);
        queue.TryDequeue(out string p3);

        Assert.Equal("first", p1);
        Assert.Equal("second", p2);
        Assert.Equal("third", p3);
    }

    [Fact]
    public void ImplementsIUserPromptQueue()
    {
        TuiUserPromptQueue queue = new();
        Assert.IsAssignableFrom<IUserPromptQueue>(queue);
    }
}
