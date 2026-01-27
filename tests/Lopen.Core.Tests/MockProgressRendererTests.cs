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

    [Fact]
    public async Task ShowProgressBarAsync_IncrementsProgressBarCallCount()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressBarAsync("Processing items", 10, async ctx =>
        {
            for (int i = 0; i < 10; i++)
            {
                ctx.Increment();
            }
        });

        mock.ProgressBarCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ShowProgressBarAsync_RecordsDescription()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressBarAsync("Running tests", 5, ctx => Task.CompletedTask);

        mock.ProgressBarCalls.Count.ShouldBe(1);
        mock.ProgressBarCalls[0].Description.ShouldBe("Running tests");
        mock.ProgressBarCalls[0].TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task ShowProgressBarAsync_RecordsIncrements()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressBarAsync("Processing", 3, async ctx =>
        {
            ctx.Increment();
            ctx.Increment();
            ctx.Increment();
        });

        mock.ProgressBarCalls[0].Increments.Count.ShouldBe(3);
        mock.ProgressBarCalls[0].CurrentValue.ShouldBe(3);
    }

    [Fact]
    public async Task ShowProgressBarAsync_RecordsDescriptionUpdates()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressBarAsync("Tasks", 2, async ctx =>
        {
            ctx.UpdateDescription("Task 1");
            ctx.Increment();
            ctx.UpdateDescription("Task 2");
            ctx.Increment();
        });

        mock.ProgressBarCalls[0].DescriptionUpdates.ShouldBe(new[] { "Task 1", "Task 2" });
    }

    [Fact]
    public async Task ShowProgressBarAsync_SetsOperationExecuted()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressBarAsync("Work", 1, ctx =>
        {
            ctx.Increment();
            return Task.CompletedTask;
        });

        mock.OperationExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ShowProgressBarAsync_ThrowsConfiguredException()
    {
        var mock = new MockProgressRenderer
        {
            ExceptionToThrow = new InvalidOperationException("Progress bar error")
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await mock.ShowProgressBarAsync("Failing", 5, ctx => Task.CompletedTask)
        );
    }

    [Fact]
    public async Task Reset_ClearsProgressBarState()
    {
        var mock = new MockProgressRenderer();
        await mock.ShowProgressBarAsync("Test", 5, ctx => Task.CompletedTask);

        mock.Reset();

        mock.ProgressBarCallCount.ShouldBe(0);
        mock.ProgressBarCalls.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ShowProgressBarAsync_MultipleIncrementAmounts()
    {
        var mock = new MockProgressRenderer();

        await mock.ShowProgressBarAsync("Items", 10, async ctx =>
        {
            ctx.Increment(3);
            ctx.Increment(5);
            ctx.Increment(2);
        });

        mock.ProgressBarCalls[0].Increments.ShouldBe(new[] { 3, 5, 2 });
        mock.ProgressBarCalls[0].CurrentValue.ShouldBe(10);
    }
}
