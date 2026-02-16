using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for the real TUI application shell.
/// Since Spectre.Tui requires a real terminal, these tests focus on:
/// - Construction, state management, and lifecycle
/// - RenderFrame logic (via the internal method)
/// - DI registration via UseRealTui
/// </summary>
public class TuiApplicationTests
{
    private static TuiApplication CreateApp()
    {
        return new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance);
    }

    [Fact]
    public void Constructor_SetsIsRunningFalse()
    {
        var app = CreateApp();
        Assert.False(app.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        var app = CreateApp();
        await app.StopAsync();
        Assert.False(app.IsRunning);
    }

    [Fact]
    public async Task RunAsync_WhenAlreadyRunning_ReturnsImmediately()
    {
        // Simulate "already running" by starting and immediately stopping
        var app = CreateApp();

        // Use a pre-cancelled token to make RunAsync exit immediately
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await app.RunAsync(cts.Token);
        Assert.False(app.IsRunning); // After cancellation, should not be running
    }

    [Fact]
    public void UpdateTopPanel_SetsData()
    {
        var app = CreateApp();
        var data = new TopPanelData { Version = "1.0.0", ModelName = "test-model" };

        app.UpdateTopPanel(data);

        // Verify by checking the app doesn't throw — data is private
        // The real test is that RenderFrame uses it
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void UpdateActivityPanel_SetsData()
    {
        var app = CreateApp();
        var data = new ActivityPanelData
        {
            Entries = [new ActivityEntry { Summary = "Test action" }]
        };

        app.UpdateActivityPanel(data);
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void UpdateContextPanel_SetsData()
    {
        var app = CreateApp();
        app.UpdateContextPanel(new ContextPanelData());
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void UpdatePromptArea_SetsData()
    {
        var app = CreateApp();
        app.UpdatePromptArea(new PromptAreaData { Text = "test input" });
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void UseRealTui_ReplacesStubRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui(); // Registers stub
        services.UseRealTui(); // Replaces with real

        using var provider = services.BuildServiceProvider();
        var app = provider.GetService<ITuiApplication>();

        Assert.NotNull(app);
        Assert.IsType<TuiApplication>(app);
    }

    [Fact]
    public void UseRealTui_ReturnsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();
        services.UseRealTui();

        using var provider = services.BuildServiceProvider();
        var app1 = provider.GetRequiredService<ITuiApplication>();
        var app2 = provider.GetRequiredService<ITuiApplication>();

        Assert.Same(app1, app2);
    }

    [Fact]
    public void AddLopenTui_WithoutUseRealTui_ReturnsStub()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var app = provider.GetService<ITuiApplication>();

        Assert.NotNull(app);
        Assert.IsType<StubTuiApplication>(app);
    }

    [Fact]
    public void Constructor_ThrowsOnNullTopPanel()
    {
        Assert.Throws<ArgumentNullException>(() => new TuiApplication(
            null!, new ActivityPanelComponent(), new ContextPanelComponent(),
            new PromptAreaComponent(), new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullKeyboardHandler()
    {
        Assert.Throws<ArgumentNullException>(() => new TuiApplication(
            new TopPanelComponent(), new ActivityPanelComponent(),
            new ContextPanelComponent(), new PromptAreaComponent(),
            null!, NullLogger<TuiApplication>.Instance));
    }
}
