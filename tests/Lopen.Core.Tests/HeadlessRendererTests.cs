using Lopen.Core;

namespace Lopen.Core.Tests;

public class HeadlessRendererTests
{
    [Fact]
    public async Task RenderProgressAsync_WritesToStdout()
    {
        var stdout = new StringWriter();
        var renderer = new HeadlessRenderer(stdout);

        await renderer.RenderProgressAsync("building", "execute-task", 0.5);

        Assert.Contains("[building] execute-task", stdout.ToString());
        Assert.Contains("50", stdout.ToString());
    }

    [Fact]
    public async Task RenderProgressAsync_NegativeProgress_OmitsPercentage()
    {
        var stdout = new StringWriter();
        var renderer = new HeadlessRenderer(stdout);

        await renderer.RenderProgressAsync("planning", "assess-state", -1);

        var output = stdout.ToString();
        Assert.Contains("[planning] assess-state", output);
        Assert.DoesNotContain("%", output);
    }

    [Fact]
    public async Task RenderErrorAsync_WritesToStderr()
    {
        var stderr = new StringWriter();
        var renderer = new HeadlessRenderer(stderr: stderr);

        await renderer.RenderErrorAsync("Something went wrong");

        Assert.Contains("Error: Something went wrong", stderr.ToString());
    }

    [Fact]
    public async Task RenderErrorAsync_WithException_IncludesDetails()
    {
        var stderr = new StringWriter();
        var renderer = new HeadlessRenderer(stderr: stderr);

        await renderer.RenderErrorAsync("Failed", new InvalidOperationException("bad state"));

        var output = stderr.ToString();
        Assert.Contains("Error: Failed", output);
        Assert.Contains("InvalidOperationException: bad state", output);
    }

    [Fact]
    public async Task RenderResultAsync_WritesToStdout()
    {
        var stdout = new StringWriter();
        var renderer = new HeadlessRenderer(stdout);

        await renderer.RenderResultAsync("Build complete");

        Assert.Contains("Build complete", stdout.ToString());
    }

    [Fact]
    public async Task PromptAsync_ReturnsNull()
    {
        var renderer = new HeadlessRenderer();

        var result = await renderer.PromptAsync("Enter something:");

        Assert.Null(result);
    }

    [Fact]
    public void DefaultConstructor_UsesConsoleWriters()
    {
        // Verifies no exceptions during construction
        var renderer = new HeadlessRenderer();
        Assert.NotNull(renderer);
    }
}
