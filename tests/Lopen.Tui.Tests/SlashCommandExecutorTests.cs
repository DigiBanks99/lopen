using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for SlashCommandExecutor and TuiApplication slash command wiring.
/// Covers JOB-036 (TUI-38, TUI-39) acceptance criteria.
/// </summary>
public class SlashCommandExecutorTests
{
    private static SlashCommandExecutor CreateExecutor(SlashCommandRegistry? registry = null)
    {
        return new SlashCommandExecutor(
            registry ?? SlashCommandRegistry.CreateDefault(),
            NullLogger<SlashCommandExecutor>.Instance);
    }

    // ==================== Parsing ====================

    [Fact]
    public async Task ExecuteAsync_EmptyInput_ReturnsError()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync(string.Empty);
        Assert.False(result.IsSuccess);
        Assert.Contains("Empty command", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceInput_ReturnsError()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("   ");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_NonSlashInput_ReturnsError()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("hello world");
        Assert.False(result.IsSuccess);
        Assert.Contains("Not a slash command", result.ErrorMessage);
    }

    // ==================== Known Commands (TUI-38) ====================

    [Theory]
    [InlineData("/help")]
    [InlineData("/spec")]
    [InlineData("/plan")]
    [InlineData("/build")]
    [InlineData("/session")]
    [InlineData("/config")]
    [InlineData("/revert")]
    [InlineData("/auth")]
    public async Task ExecuteAsync_KnownCommand_ReturnsSuccess(string command)
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync(command);
        Assert.True(result.IsSuccess);
        Assert.Equal(command, result.Command);
    }

    [Fact]
    public async Task ExecuteAsync_KnownCommand_CaseInsensitive()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/HELP");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_HelpCommand_ListsAllCommands()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/help");
        Assert.True(result.IsSuccess);
        Assert.Contains("/help", result.OutputMessage);
        Assert.Contains("/spec", result.OutputMessage);
        Assert.Contains("/plan", result.OutputMessage);
        Assert.Contains("/build", result.OutputMessage);
        Assert.Contains("/session", result.OutputMessage);
        Assert.Contains("/config", result.OutputMessage);
        Assert.Contains("/revert", result.OutputMessage);
        Assert.Contains("/auth", result.OutputMessage);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithArguments_ParsesArgs()
    {
        var executor = CreateExecutor();
        string? receivedArgs = null;
        executor.RegisterHandler("/session", (args, _) =>
        {
            receivedArgs = args;
            return Task.FromResult(SlashCommandResult.Success("/session", "ok", args));
        });

        var result = await executor.ExecuteAsync("/session list");
        Assert.True(result.IsSuccess);
        Assert.Equal("list", receivedArgs);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithMultipleArgs_ParsesFullArgs()
    {
        var executor = CreateExecutor();
        string? receivedArgs = null;
        executor.RegisterHandler("/config", (args, _) =>
        {
            receivedArgs = args;
            return Task.FromResult(SlashCommandResult.Success("/config", "ok", args));
        });

        var result = await executor.ExecuteAsync("/config show --json");
        Assert.True(result.IsSuccess);
        Assert.Equal("show --json", receivedArgs);
    }

    [Fact]
    public async Task ExecuteAsync_CommandWithNoArgs_PassesNullArgs()
    {
        var executor = CreateExecutor();
        string? receivedArgs = "not-null";
        executor.RegisterHandler("/revert", (args, _) =>
        {
            receivedArgs = args;
            return Task.FromResult(SlashCommandResult.Success("/revert", "ok"));
        });

        var result = await executor.ExecuteAsync("/revert");
        Assert.Null(receivedArgs);
    }

    // ==================== Unknown Commands (TUI-39) ====================

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ReturnsFailure()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/unknown");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ShowsCommandInError()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/unknown");
        Assert.Contains("/unknown", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ShowsValidCommandList()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/unknown");
        Assert.Contains("/help", result.ErrorMessage);
        Assert.Contains("/spec", result.ErrorMessage);
        Assert.Contains("/plan", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommandWithArgs_ShowsCorrectCommand()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/foo bar");
        Assert.Contains("/foo", result.ErrorMessage);
    }

    // ==================== Handler Registration ====================

    [Fact]
    public async Task RegisterHandler_OverridesDefaultHandler()
    {
        var executor = CreateExecutor();
        executor.RegisterHandler("/help", (_, _) =>
            Task.FromResult(SlashCommandResult.Success("/help", "custom help")));

        var result = await executor.ExecuteAsync("/help");
        Assert.Equal("custom help", result.OutputMessage);
    }

    [Fact]
    public async Task RegisterHandler_CustomCommand_IsInvoked()
    {
        var registry = SlashCommandRegistry.CreateDefault();
        registry.Register("/test", "Test command");
        var executor = CreateExecutor(registry);
        executor.RegisterHandler("/test", (_, _) =>
            Task.FromResult(SlashCommandResult.Success("/test", "test executed")));

        var result = await executor.ExecuteAsync("/test");
        Assert.True(result.IsSuccess);
        Assert.Equal("test executed", result.OutputMessage);
    }

    [Fact]
    public void RegisterHandler_NullHandler_ThrowsArgumentNull()
    {
        var executor = CreateExecutor();
        Assert.Throws<ArgumentNullException>(() =>
            executor.RegisterHandler("/test", null!));
    }

    // ==================== Error Handling ====================

    [Fact]
    public async Task ExecuteAsync_HandlerThrows_ReturnsError()
    {
        var executor = CreateExecutor();
        executor.RegisterHandler("/spec", (_, _) =>
            throw new InvalidOperationException("something broke"));

        var result = await executor.ExecuteAsync("/spec");
        Assert.False(result.IsSuccess);
        Assert.Contains("something broke", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerThrowsOperationCanceled_Propagates()
    {
        var executor = CreateExecutor();
        executor.RegisterHandler("/spec", (_, _) =>
            throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => executor.ExecuteAsync("/spec"));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationToken_PassedToHandler()
    {
        var executor = CreateExecutor();
        CancellationToken received = default;
        executor.RegisterHandler("/spec", (_, ct) =>
        {
            received = ct;
            return Task.FromResult(SlashCommandResult.Success("/spec", "ok"));
        });

        using var cts = new CancellationTokenSource();
        await executor.ExecuteAsync("/spec", cts.Token);
        Assert.Equal(cts.Token, received);
    }

    // ==================== SlashCommandResult ====================

    [Fact]
    public void SlashCommandResult_Success_SetsProperties()
    {
        var result = SlashCommandResult.Success("/test", "output", "args");
        Assert.True(result.IsSuccess);
        Assert.Equal("/test", result.Command);
        Assert.Equal("output", result.OutputMessage);
        Assert.Equal("args", result.Arguments);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SlashCommandResult_Error_SetsProperties()
    {
        var result = SlashCommandResult.Error("/test", "failed");
        Assert.False(result.IsSuccess);
        Assert.Equal("/test", result.Command);
        Assert.Equal("failed", result.ErrorMessage);
    }

    [Fact]
    public void SlashCommandResult_UnknownCommand_ContainsValidCommands()
    {
        var commands = SlashCommandRegistry.CreateDefault().GetAll();
        var result = SlashCommandResult.UnknownCommand("/bad", commands);
        Assert.False(result.IsSuccess);
        Assert.Contains("/bad", result.ErrorMessage!);
        Assert.Contains("/help", result.ErrorMessage!);
    }

    // ==================== DI Registration ====================

    [Fact]
    public void AddLopenTui_RegistersSlashCommandRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetService<SlashCommandRegistry>();
        Assert.NotNull(registry);
        Assert.True(registry.GetAll().Count >= 8);
    }

    [Fact]
    public void AddLopenTui_RegistersSlashCommandExecutor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var sp = services.BuildServiceProvider();
        var executor = sp.GetService<ISlashCommandExecutor>();
        Assert.NotNull(executor);
    }

    [Fact]
    public void AddLopenTui_SlashCommandExecutor_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetService<ISlashCommandExecutor>();
        var b = sp.GetService<ISlashCommandExecutor>();
        Assert.Same(a, b);
    }

    // ==================== TuiApplication Integration ====================

    [Fact]
    public void TuiApplication_AcceptsSlashCommandExecutor()
    {
        var executor = CreateExecutor();
        var app = new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            slashCommandExecutor: executor);

        Assert.NotNull(app);
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void TuiApplication_WorksWithoutSlashCommandExecutor()
    {
        var app = new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance);

        Assert.NotNull(app);
    }

    // ==================== Default Handlers (TUI-38) ====================

    [Fact]
    public async Task ExecuteAsync_KnownCommandWithoutHandler_ReturnsSuccessWithDescription()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/spec");
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputMessage);
        Assert.Contains("requirement", result.OutputMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_BuildCommand_ReturnsSuccess()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/build");
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputMessage);
    }

    [Fact]
    public async Task ExecuteAsync_SessionWithArgs_ReturnsSuccess()
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync("/session list");
        Assert.True(result.IsSuccess);
    }
}
