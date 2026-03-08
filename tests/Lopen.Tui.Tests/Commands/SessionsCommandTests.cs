using Lopen.Storage;
using Lopen.Tui.Commands;
using NSubstitute;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class SessionsCommandTests
{
    private static IAnsiConsole CreateTestConsole(StringWriter writer)
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
    }

    [Fact]
    public async Task NoSessionManager_ShowsWarning()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        var command = new SessionsCommand(console, sessionManager: null);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("Session management not available", writer.ToString());
    }

    [Fact]
    public async Task EmptySessions_ShowsNoSessionsFound()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();
        sessionManager.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SessionId>());

        var command = new SessionsCommand(console, sessionManager);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("No sessions found", writer.ToString());
    }

    [Fact]
    public async Task RendersTable_WithCorrectColumnHeaders()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId session = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { session });
        sessionManager.LoadSessionStateAsync(session, Arg.Any<CancellationToken>())
            .Returns(new SessionState
            {
                SessionId = session.ToString(),
                Module = "tui",
                Phase = "building",
                Step = "implement",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            });

        var command = new SessionsCommand(console, sessionManager);

        await command.ExecuteAsync("");

        string output = writer.ToString();
        Assert.Contains("ID", output);
        Assert.Contains("Module", output);
        Assert.Contains("Step", output);
        Assert.Contains("Updated", output);
    }

    [Fact]
    public async Task RendersTable_WithSessionData()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId session = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { session });
        sessionManager.LoadSessionStateAsync(session, Arg.Any<CancellationToken>())
            .Returns(new SessionState
            {
                SessionId = session.ToString(),
                Module = "tui",
                Phase = "building",
                Step = "implement",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            });

        var command = new SessionsCommand(console, sessionManager);

        await command.ExecuteAsync("");

        string output = writer.ToString();
        Assert.Contains("tui-20250308-1", output);
        Assert.Contains("tui", output);
        Assert.Contains("implement", output);
        Assert.Contains("1h ago", output);
    }

    [Fact]
    public async Task HandlesNullSessionState_ShowsPlaceholders()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId session = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { session });
        sessionManager.LoadSessionStateAsync(session, Arg.Any<CancellationToken>())
            .Returns((SessionState?)null);

        var command = new SessionsCommand(console, sessionManager);

        await command.ExecuteAsync("");

        string output = writer.ToString();
        Assert.Contains("tui-20250308-1", output);
        Assert.Contains("?", output);
    }

    [Fact]
    public async Task NonInteractiveConsole_DoesNotThrow()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId session = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.ListSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { session });
        sessionManager.LoadSessionStateAsync(session, Arg.Any<CancellationToken>())
            .Returns(new SessionState
            {
                SessionId = session.ToString(),
                Module = "tui",
                Phase = "building",
                Step = "implement",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            });

        var command = new SessionsCommand(console, sessionManager);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        await sessionManager.DidNotReceive().SetLatestAsync(Arg.Any<SessionId>(), Arg.Any<CancellationToken>());
    }
}
