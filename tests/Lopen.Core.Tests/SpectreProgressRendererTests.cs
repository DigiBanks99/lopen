using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Core.Tests;

public class SpectreProgressRendererTests
{
    [Fact]
    public async Task ShowProgressAsync_ExecutesOperation()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);
        var executed = false;

        await renderer.ShowProgressAsync("Testing...", ctx =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task ShowProgressAsync_ReturnsResult()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);

        var result = await renderer.ShowProgressAsync("Computing...", ctx => Task.FromResult(42));

        result.ShouldBe(42);
    }

    [Fact]
    public async Task ShowProgressAsync_WithNoColor_ShowsTextOutput()
    {
        // Set NO_COLOR for this test
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreProgressRenderer(console);

            await renderer.ShowProgressAsync("Loading...", ctx => Task.FromResult(1));

            console.Output.ShouldContain("Loading");
            console.Output.ShouldContain("Done");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public async Task ShowProgressAsync_WithNoColor_ShowsStatusUpdates()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreProgressRenderer(console);

            await renderer.ShowProgressAsync("Starting", ctx =>
            {
                ctx.UpdateStatus("Processing");
                return Task.FromResult("done");
            });

            console.Output.ShouldContain("Starting");
            console.Output.ShouldContain("Processing");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Theory]
    [InlineData(SpinnerType.Dots)]
    [InlineData(SpinnerType.Arc)]
    [InlineData(SpinnerType.Line)]
    [InlineData(SpinnerType.SimpleDotsScrolling)]
    public async Task ShowProgressAsync_AcceptsAllSpinnerTypes(SpinnerType spinnerType)
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console, spinnerType);

        var result = await renderer.ShowProgressAsync("Working...", ctx => Task.FromResult(true));

        result.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        Should.Throw<ArgumentNullException>(() => new SpectreProgressRenderer(null!));
    }

    [Fact]
    public async Task ShowProgressAsync_PropagatesException()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.ShowProgressAsync("Failing...", ctx =>
                throw new InvalidOperationException("Test error")));
    }

    [Fact]
    public async Task ShowProgressAsync_WorksWithAsyncLambda()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);

        var result = await renderer.ShowProgressAsync("Async work...", async ctx =>
        {
            await Task.Delay(10);
            return "completed";
        });

        result.ShouldBe("completed");
    }

    [Fact]
    public async Task ShowProgressAsync_VoidOverload_Works()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);
        var executed = false;

        await renderer.ShowProgressAsync("Processing...", async ctx =>
        {
            await Task.Delay(1);
            executed = true;
        });

        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task ShowProgressBarAsync_ExecutesOperation()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);
        var itemsProcessed = 0;

        await renderer.ShowProgressBarAsync("Processing items", 5, async ctx =>
        {
            for (int i = 0; i < 5; i++)
            {
                itemsProcessed++;
                ctx.Increment();
            }
        });

        itemsProcessed.ShouldBe(5);
    }

    [Fact]
    public async Task ShowProgressBarAsync_WithNoColor_ShowsTextOutput()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreProgressRenderer(console);

            await renderer.ShowProgressBarAsync("Processing", 10, async ctx =>
            {
                for (int i = 0; i < 10; i++)
                {
                    ctx.Increment();
                }
            });

            console.Output.ShouldContain("Processing");
            console.Output.ShouldContain("complete");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public async Task ShowProgressBarAsync_ThrowsOnZeroCount()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await renderer.ShowProgressBarAsync("Test", 0, ctx => Task.CompletedTask));
    }

    [Fact]
    public async Task ShowProgressBarAsync_ThrowsOnNegativeCount()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await renderer.ShowProgressBarAsync("Test", -5, ctx => Task.CompletedTask));
    }

    [Fact]
    public async Task ShowProgressBarAsync_AllowsDescriptionUpdates()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);
        var descriptions = new List<string>();

        await renderer.ShowProgressBarAsync("Initial", 3, async ctx =>
        {
            ctx.UpdateDescription("Step 1");
            descriptions.Add("Step 1");
            ctx.Increment();
            ctx.UpdateDescription("Step 2");
            descriptions.Add("Step 2");
            ctx.Increment();
            ctx.UpdateDescription("Complete");
            descriptions.Add("Complete");
            ctx.Increment();
        });

        descriptions.ShouldBe(new[] { "Step 1", "Step 2", "Complete" });
    }

    [Fact]
    public async Task ShowProgressBarAsync_PropagatesException()
    {
        var console = new TestConsole();
        var renderer = new SpectreProgressRenderer(console);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.ShowProgressBarAsync("Failing", 5, ctx =>
                throw new InvalidOperationException("Progress error")));
    }
}
