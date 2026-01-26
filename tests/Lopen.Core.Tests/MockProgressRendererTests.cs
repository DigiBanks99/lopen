using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class MockProgressRendererTests
{
    [Fact]
    public async Task ShowProgressAsync_IncrementsCallCount()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressAsync("Testing...", ctx => Task.FromResult(42));

        mock.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ShowProgressAsync_RecordsInitialStatus()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressAsync("Initial status", ctx => Task.FromResult("result"));

        mock.StatusUpdates.ShouldContain("Initial status");
    }

    [Fact]
    public async Task ShowProgressAsync_RecordsStatusUpdates()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressAsync("Starting", ctx =>
        {
            ctx.UpdateStatus("Step 1");
            ctx.UpdateStatus("Step 2");
            return Task.FromResult("done");
        });

        mock.StatusUpdates.Count.ShouldBe(3);
        mock.StatusUpdates.ShouldContain("Starting");
        mock.StatusUpdates.ShouldContain("Step 1");
        mock.StatusUpdates.ShouldContain("Step 2");
    }

    [Fact]
    public async Task ShowProgressAsync_SetsOperationExecuted()
    {
        var mock = new MockProgressRenderer();
        mock.OperationExecuted.ShouldBeFalse();

        await mock.ShowProgressAsync("Working", ctx => Task.FromResult(1));

        mock.OperationExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ShowProgressAsync_ReturnsOperationResult()
    {
        var mock = new MockProgressRenderer();

        var result = await mock.ShowProgressAsync("Computing", ctx => Task.FromResult(42));

        result.ShouldBe(42);
    }

    [Fact]
    public async Task ShowProgressAsync_WithoutReturnValue_ExecutesOperation()
    {
        var mock = new MockProgressRenderer();
        var executed = false;

        await mock.ShowProgressAsync("Processing", ctx =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.ShouldBeTrue();
        mock.OperationExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ShowProgressAsync_ThrowsConfiguredException()
    {
        var mock = new MockProgressRenderer
        {
            ExceptionToThrow = new InvalidOperationException("Test error")
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await mock.ShowProgressAsync("Failing", ctx => Task.FromResult(1))
        );
    }

    [Fact]
    public async Task Reset_ClearsAllState()
    {
        var mock = new MockProgressRenderer();
        await mock.ShowProgressAsync("Test", ctx =>
        {
            ctx.UpdateStatus("Update");
            return Task.FromResult(1);
        });

        mock.Reset();

        mock.StatusUpdates.Count.ShouldBe(0);
        mock.CallCount.ShouldBe(0);
        mock.OperationExecuted.ShouldBeFalse();
        mock.ExceptionToThrow.ShouldBeNull();
    }

    [Fact]
    public async Task ShowProgressAsync_MultipleCalls_AccumulatesCounts()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressAsync("First", ctx => Task.FromResult(1));
        await mock.ShowProgressAsync("Second", ctx => Task.FromResult(2));
        await mock.ShowProgressAsync("Third", ctx => Task.FromResult(3));

        mock.CallCount.ShouldBe(3);
        mock.StatusUpdates.ShouldContain("First");
        mock.StatusUpdates.ShouldContain("Second");
        mock.StatusUpdates.ShouldContain("Third");
    }

    [Fact]
    public async Task StatusUpdates_AreInOrder()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressAsync("Initial", ctx =>
        {
            ctx.UpdateStatus("A");
            ctx.UpdateStatus("B");
            ctx.UpdateStatus("C");
            return Task.FromResult(true);
        });

        mock.StatusUpdates.ShouldBe(new[] { "Initial", "A", "B", "C" });
    }
}
