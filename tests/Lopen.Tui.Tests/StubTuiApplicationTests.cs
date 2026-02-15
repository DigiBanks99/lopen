using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

public class StubTuiApplicationTests
{
    private readonly StubTuiApplication _app = new(NullLogger<StubTuiApplication>.Instance);

    [Fact]
    public void IsRunning_InitiallyFalse()
    {
        Assert.False(_app.IsRunning);
    }

    [Fact]
    public async Task RunAsync_SetsIsRunningTrue()
    {
        await _app.RunAsync();

        Assert.True(_app.IsRunning);
    }

    [Fact]
    public async Task StopAsync_SetsIsRunningFalse()
    {
        await _app.RunAsync();
        await _app.StopAsync();

        Assert.False(_app.IsRunning);
    }

    [Fact]
    public async Task RunAsync_CompletesImmediately()
    {
        var task = _app.RunAsync();

        Assert.True(task.IsCompleted);
        await task;
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        var task = _app.StopAsync();

        Assert.True(task.IsCompleted);
        await task;
    }

    [Fact]
    public async Task RunAsync_WithCancellationToken_CompletesImmediately()
    {
        using var cts = new CancellationTokenSource();
        await _app.RunAsync(cts.Token);

        Assert.True(_app.IsRunning);
    }
}
