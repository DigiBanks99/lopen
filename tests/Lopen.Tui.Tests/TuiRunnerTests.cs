using Lopen.Core.Workflow;
using Lopen.Tui.Commands;
using NSubstitute;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class TuiRunnerTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        (IAnsiConsole _, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(null!, editor, queue, renderer, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLineEditor()
    {
        (IAnsiConsole? console, LopenLineEditor _, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, null!, queue, renderer, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPromptQueue()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue _, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, null!, renderer, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRenderer()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer _, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, queue, null!, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullCommandRegistry()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry _) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, queue, renderer, null!));
    }

    [Fact]
    public async Task RunAsync_ExitsOnCancellation()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        TuiRunner runner = new(console, editor, queue, renderer, registry);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        int exitCode = await runner.RunAsync(cts.Token);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ProcessTurnAsync_WithNoOrchestrator_Completes()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        TuiRunner runner = new(console, editor, queue, renderer, registry);

        await runner.ProcessTurnAsync(CancellationToken.None);

        // Should complete without throwing — the "(Prompt enqueued)" path executes
    }

    [Fact]
    public async Task ProcessTurnAsync_WithMockOrchestrator_CallsRunStepAsync()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        IWorkflowOrchestrator orchestrator = NSubstitute.Substitute.For<Core.Workflow.IWorkflowOrchestrator>();
        orchestrator.RunStepAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Core.Workflow.StepResult.Succeeded(Core.Workflow.WorkflowTrigger.Assess));
        TuiRunner runner = new(console, editor, queue, renderer, registry, orchestrator);

        await runner.ProcessTurnAsync(CancellationToken.None);

        await orchestrator.Received(1).RunStepAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTurnAsync_WithPreCancelledToken_ThrowsOrCancels()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        IWorkflowOrchestrator orchestrator = NSubstitute.Substitute.For<Core.Workflow.IWorkflowOrchestrator>();
        orchestrator.RunStepAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Core.Workflow.StepResult.Succeeded(Core.Workflow.WorkflowTrigger.Assess);
            });
        TuiRunner runner = new(console, editor, queue, renderer, registry, orchestrator);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        // With a pre-cancelled global token, the linked turn CTS is also cancelled.
        // The OperationCanceledException is caught internally because turnCts is cancelled
        // but globalToken is also cancelled — so the catch filter does NOT match and it propagates.
        await Assert.ThrowsAsync<OperationCanceledException>(() => runner.ProcessTurnAsync(cts.Token));
    }

    [Fact]
    public async Task ProcessTurnAsync_OrchestratorReceivesCancellableToken()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        CancellationToken receivedToken = default;
        IWorkflowOrchestrator orchestrator = NSubstitute.Substitute.For<Core.Workflow.IWorkflowOrchestrator>();
        orchestrator.RunStepAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                receivedToken = callInfo.Arg<CancellationToken>();
                return Core.Workflow.StepResult.Succeeded(Core.Workflow.WorkflowTrigger.Assess);
            });
        TuiRunner runner = new(console, editor, queue, renderer, registry, orchestrator);

        await runner.ProcessTurnAsync(CancellationToken.None);

        // The orchestrator receives a linked token that supports per-turn cancellation
        Assert.True(receivedToken.CanBeCanceled);
    }

    [Fact]
    public void HandleCancelDuringProcessing_FirstCall_CancelsTurnCts()
    {
        using CancellationTokenSource turnCts = new();

        TuiRunner.HandleCancelDuringProcessing(turnCts);

        Assert.True(turnCts.IsCancellationRequested);
    }

    [Fact]
    public void HandleCancelDuringProcessing_SecondCall_DoesNotThrow()
    {
        using CancellationTokenSource turnCts = new();

        // First call cancels the turn CTS
        TuiRunner.HandleCancelDuringProcessing(turnCts);
        Assert.True(turnCts.IsCancellationRequested);

        // Second call returns without throwing (lets SIGINT propagate for force-exit)
        TuiRunner.HandleCancelDuringProcessing(turnCts);

        // No exception — the second Ctrl+C allows process termination
    }

    [Fact]
    public async Task ExecuteTurnAsync_WhenTurnCancelledNotGlobal_ShowsCancelledMessage()
    {
        var writer = new StringWriter();
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(writer),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        LopenLineEditor editor = new(console, history);
        TuiUserPromptQueue queue = new();
        TuiOutputRenderer renderer = new(console, editor);
        SlashCommandRegistry registry = new(console, []);

        var started = new TaskCompletionSource();
        IWorkflowOrchestrator orchestrator = Substitute.For<Core.Workflow.IWorkflowOrchestrator>();
        orchestrator.RunStepAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                CancellationToken ct = callInfo.Arg<CancellationToken>();
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return Core.Workflow.StepResult.Succeeded(Core.Workflow.WorkflowTrigger.Assess);
            });

        TuiRunner runner = new(console, editor, queue, renderer, registry, orchestrator);

        // Create a per-turn CTS (not linked to any global) so globalToken is non-cancelled
        using var turnCts = new CancellationTokenSource();

        var executeTask = Task.Run(async () =>
        {
            await runner.ExecuteTurnAsync(turnCts, CancellationToken.None);
        });

        await started.Task;

        // Simulate first Ctrl+C: cancel the turn CTS
        TuiRunner.HandleCancelDuringProcessing(turnCts);

        await executeTask.WaitAsync(TimeSpan.FromSeconds(5));

        string output = writer.ToString();
        Assert.Contains("Cancelled", output);
    }

    private static (IAnsiConsole console, LopenLineEditor editor, TuiUserPromptQueue queue, Lopen.Core.IOutputRenderer renderer, SlashCommandRegistry registry) CreateDependencies()
    {
        // RadLine requires ANSI support
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        LopenLineEditor editor = new(console, history);
        TuiUserPromptQueue queue = new();
        TuiOutputRenderer renderer = new(console, editor);
        SlashCommandRegistry registry = new(console, []);
        return (console, editor, queue, renderer, registry);
    }
}
