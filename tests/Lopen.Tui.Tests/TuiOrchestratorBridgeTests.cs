using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for TUI-52: TUI application bridges to WorkflowOrchestrator on background thread.
/// </summary>
public class TuiOrchestratorBridgeTests
{
    private static TuiApplication CreateApp(
        IWorkflowOrchestrator? orchestrator = null,
        IActivityPanelDataProvider? activityProvider = null,
        bool showLandingPage = false)
    {
        return new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            activityPanelDataProvider: activityProvider,
            orchestrator: orchestrator,
            showLandingPage: showLandingPage);
    }

    [Fact]
    public void Constructor_WithOrchestrator_DoesNotThrow()
    {
        var orchestrator = new StubOrchestrator();
        var app = CreateApp(orchestrator: orchestrator);
        Assert.NotNull(app);
    }

    [Fact]
    public void Constructor_WithoutOrchestrator_DoesNotThrow()
    {
        var app = CreateApp(orchestrator: null);
        Assert.NotNull(app);
    }

    [Fact]
    public void SetOrchestratorModule_SetsModuleName()
    {
        var app = CreateApp();
        app.SetOrchestratorModule("auth");
        // No exception = success; module name stored internally
    }

    [Fact]
    public async Task RunAsync_WithOrchestrator_LaunchesOrchestratorOnBackground()
    {
        var orchestrator = new StubOrchestrator();
        var app = CreateApp(orchestrator: orchestrator, showLandingPage: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await app.RunAsync(cancellationToken: cts.Token);

        Assert.True(orchestrator.RunAsyncCalled, "Orchestrator.RunAsync should be called");
    }

    [Fact]
    public async Task RunAsync_WithOrchestrator_PassesInitialPrompt()
    {
        var orchestrator = new StubOrchestrator();
        var app = CreateApp(orchestrator: orchestrator, showLandingPage: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await app.RunAsync("build the auth module", cts.Token);

        Assert.Equal("build the auth module", orchestrator.ReceivedPrompt);
    }

    [Fact]
    public async Task RunAsync_WithOrchestrator_UsesModuleName()
    {
        var orchestrator = new StubOrchestrator();
        var app = CreateApp(orchestrator: orchestrator, showLandingPage: false);
        app.SetOrchestratorModule("storage");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await app.RunAsync(cancellationToken: cts.Token);

        Assert.Equal("storage", orchestrator.ReceivedModuleName);
    }

    [Fact]
    public async Task RunAsync_WithoutOrchestrator_DoesNotThrow()
    {
        var app = CreateApp(orchestrator: null, showLandingPage: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await app.RunAsync(cancellationToken: cts.Token);
        // Should not throw
    }

    [Fact]
    public async Task RunAsync_OrchestratorCompletes_AddsCompletionEntry()
    {
        var activityProvider = new ActivityPanelDataProvider();
        var orchestrator = new StubOrchestrator
        {
            Result = OrchestrationResult.Completed(5, WorkflowStep.Repeat, "All done")
        };
        var app = CreateApp(orchestrator: orchestrator, activityProvider: activityProvider, showLandingPage: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await app.RunAsync(cancellationToken: cts.Token);

        // Give background task time to complete
        var data = activityProvider.GetCurrentData();
        // Should have at least the completion entry
        Assert.Contains(data.Entries, e => e.Summary.Contains("complete", StringComparison.OrdinalIgnoreCase)
            || e.Summary.Contains("All done", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_OrchestratorFails_AddsErrorEntry()
    {
        var activityProvider = new ActivityPanelDataProvider();
        var orchestrator = new StubOrchestrator
        {
            ShouldThrow = true,
            ExceptionToThrow = new InvalidOperationException("Test failure")
        };
        var app = CreateApp(orchestrator: orchestrator, activityProvider: activityProvider, showLandingPage: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await app.RunAsync(cancellationToken: cts.Token);

        var data = activityProvider.GetCurrentData();
        Assert.Contains(data.Entries, e =>
            e.Kind == ActivityEntryKind.Error ||
            e.Summary.Contains("Test failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_OrchestratorInterrupted_AddsInterruptionEntry()
    {
        var activityProvider = new ActivityPanelDataProvider();
        var orchestrator = new StubOrchestrator
        {
            Result = OrchestrationResult.Interrupted(3, WorkflowStep.IterateThroughTasks, "User paused")
        };
        var app = CreateApp(orchestrator: orchestrator, activityProvider: activityProvider, showLandingPage: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await app.RunAsync(cancellationToken: cts.Token);

        var data = activityProvider.GetCurrentData();
        Assert.Contains(data.Entries, e =>
            e.Summary.Contains("interrupt", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("User paused", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_WithLandingPage_DoesNotLaunchOrchestratorImmediately()
    {
        var orchestrator = new StubOrchestrator { Delay = TimeSpan.FromSeconds(5) };
        var app = CreateApp(orchestrator: orchestrator, showLandingPage: true);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await app.RunAsync(cancellationToken: cts.Token);

        // With landing page shown and short timeout, orchestrator should not be called
        // because the modal prevents launch
        Assert.False(orchestrator.RunAsyncCalled);
    }

    /// <summary>
    /// Stub orchestrator for testing TUI bridge behavior.
    /// </summary>
    private sealed class StubOrchestrator : IWorkflowOrchestrator
    {
        public bool RunAsyncCalled { get; private set; }
        public string? ReceivedModuleName { get; private set; }
        public string? ReceivedPrompt { get; private set; }
        public OrchestrationResult? Result { get; set; }
        public bool ShouldThrow { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public async Task<OrchestrationResult> RunAsync(
            string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
        {
            RunAsyncCalled = true;
            ReceivedModuleName = moduleName;
            ReceivedPrompt = userPrompt;

            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, cancellationToken);

            if (ShouldThrow)
                throw ExceptionToThrow ?? new InvalidOperationException("Stub failure");

            return Result ?? OrchestrationResult.Completed(1, WorkflowStep.Repeat, "Stub complete");
        }

        public Task<StepResult> RunStepAsync(
            string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
