using Lopen.Core.Workflow;
using Lopen.Storage;
using Lopen.Tui.Commands;
using NSubstitute;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class ResumeCommandTests
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

    private static SessionState CreateSessionState(
        string sessionId, string module = "tui", string step = "implement",
        string phase = "building", bool isComplete = false)
    {
        return new SessionState
        {
            SessionId = sessionId,
            Module = module,
            Phase = phase,
            Step = step,
            IsComplete = isComplete,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
    }

    [Fact]
    public async Task NoSessionManager_ShowsWarning()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        var command = new ResumeCommand(console, sessionManager: null);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("Session management not available", writer.ToString());
    }

    [Fact]
    public async Task NoIncompleteSessions_ShowsMessage()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();
        sessionManager.GetLatestSessionIdAsync(Arg.Any<CancellationToken>())
            .Returns((SessionId?)null);

        var command = new ResumeCommand(console, sessionManager);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("No incomplete sessions found", writer.ToString());
    }

    [Fact]
    public async Task ResumesLatestSession_WhenNoArgsProvided()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();
        IWorkflowOrchestrator orchestrator = Substitute.For<IWorkflowOrchestrator>();

        SessionId sessionId = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.GetLatestSessionIdAsync(Arg.Any<CancellationToken>())
            .Returns(sessionId);
        sessionManager.LoadSessionStateAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(CreateSessionState(sessionId.ToString()));

        var command = new ResumeCommand(console, sessionManager, orchestrator);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        await orchestrator.Received(1).InitializeAsync(
            "tui", sessionId, Arg.Any<CancellationToken>());
        await sessionManager.Received(1).SetLatestAsync(sessionId, Arg.Any<CancellationToken>());

        string output = writer.ToString();
        Assert.Contains("Resuming session tui-20250308-1", output);
        Assert.Contains("building/implement", output);
    }

    [Fact]
    public async Task ResumesSpecificSession_WhenArgsContainSessionId()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();
        IWorkflowOrchestrator orchestrator = Substitute.For<IWorkflowOrchestrator>();

        SessionId sessionId = SessionId.TryParse("core-20250315-2")!;
        sessionManager.LoadSessionStateAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(CreateSessionState(sessionId.ToString(), module: "core", step: "plan"));

        var command = new ResumeCommand(console, sessionManager, orchestrator);

        SlashCommandResult result = await command.ExecuteAsync("core-20250315-2");

        Assert.Equal(SlashCommandResult.Handled, result);
        await orchestrator.Received(1).InitializeAsync(
            "core", sessionId, Arg.Any<CancellationToken>());

        string output = writer.ToString();
        Assert.Contains("Resuming session core-20250315-2", output);
        Assert.Contains("building/plan", output);
    }

    [Fact]
    public async Task InvalidSessionIdInArgs_ShowsError()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        var command = new ResumeCommand(console, sessionManager);

        SlashCommandResult result = await command.ExecuteAsync("not-a-valid-id");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("Invalid session ID", writer.ToString());
    }

    [Fact]
    public async Task SpecifiedSessionNotFound_ShowsError()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId sessionId = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.LoadSessionStateAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((SessionState?)null);

        var command = new ResumeCommand(console, sessionManager);

        SlashCommandResult result = await command.ExecuteAsync("tui-20250308-1");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("not found", writer.ToString());
    }

    [Fact]
    public async Task SpecifiedSessionIsComplete_ShowsError()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId sessionId = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.LoadSessionStateAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(CreateSessionState(sessionId.ToString(), isComplete: true));

        var command = new ResumeCommand(console, sessionManager);

        SlashCommandResult result = await command.ExecuteAsync("tui-20250308-1");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.Contains("already complete", writer.ToString());
    }

    [Fact]
    public async Task RendersResumptionMessage_WithSessionIdAndStep()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();
        IWorkflowOrchestrator orchestrator = Substitute.For<IWorkflowOrchestrator>();

        SessionId sessionId = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.GetLatestSessionIdAsync(Arg.Any<CancellationToken>())
            .Returns(sessionId);
        sessionManager.LoadSessionStateAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(CreateSessionState(sessionId.ToString(), step: "DraftSpecification"));

        var command = new ResumeCommand(console, sessionManager, orchestrator);

        await command.ExecuteAsync("");

        string output = writer.ToString();
        Assert.Contains("Resuming session tui-20250308-1 at building/DraftSpecification", output);
    }

    [Fact]
    public async Task NoOrchestrator_StillShowsMessage_DoesNotCrash()
    {
        var writer = new StringWriter();
        IAnsiConsole console = CreateTestConsole(writer);
        ISessionManager sessionManager = Substitute.For<ISessionManager>();

        SessionId sessionId = SessionId.TryParse("tui-20250308-1")!;
        sessionManager.GetLatestSessionIdAsync(Arg.Any<CancellationToken>())
            .Returns(sessionId);
        sessionManager.LoadSessionStateAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(CreateSessionState(sessionId.ToString()));

        var command = new ResumeCommand(console, sessionManager, orchestrator: null);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
        string output = writer.ToString();
        Assert.Contains("Cannot resume: orchestrator not available", output);
        await sessionManager.DidNotReceive().SetLatestAsync(Arg.Any<SessionId>(), Arg.Any<CancellationToken>());
    }
}
