using System.CommandLine;
using Lopen.Commands;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

/// <summary>
/// Integration tests for phase commands (spec, plan, build) covering orchestrator
/// failure paths, interruption handling, and prompt passthrough.
/// Covers CLI-03 (spec), CLI-04 (plan), CLI-05 (build).
/// </summary>
public class PhaseCommandIntegrationTests
{
    private static readonly SessionId Session1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);

    private static readonly SessionState ActiveState = new()
    {
        SessionId = Session1.ToString(),
        Phase = "building",
        Step = "execute-task",
        Module = "auth",
        CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
    };

    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig(
        IWorkflowOrchestrator? orchestrator = null,
        bool registerOrchestrator = true,
        bool addSession = true,
        bool addSpec = true,
        bool addPlan = true)
    {
        var sessionManager = new Fakes.FakeSessionManager();
        var moduleScanner = new Fakes.FakeModuleScanner();
        var planManager = new Fakes.FakePlanManager();

        if (addSession)
        {
            sessionManager.AddSession(Session1, ActiveState);
            sessionManager.SetLatestSessionId(Session1);
        }

        if (addSpec)
            moduleScanner.AddModule("auth", hasSpec: true);

        if (addPlan)
            planManager.AddPlan("auth", "# Plan\n- [ ] Task 1");

        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionManager);
        services.AddSingleton<IModuleScanner>(moduleScanner);
        services.AddSingleton<IPlanManager>(planManager);

        if (registerOrchestrator && orchestrator is not null)
            services.AddSingleton<IWorkflowOrchestrator>(orchestrator);

        var provider = services.BuildServiceProvider();
        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.Add(PhaseCommands.CreateSpec(provider, output, error));
        root.Add(PhaseCommands.CreatePlan(provider, output, error));
        root.Add(PhaseCommands.CreateBuild(provider, output, error));

        return (new CommandLineConfiguration(root), output, error);
    }

    // ==================== CLI-03: Spec — Orchestrator Failures ====================

    [Fact]
    public async Task Spec_OrchestratorFailure_ReturnsErrorExitCode()
    {
        var orchestrator = new ThrowingOrchestrator(new InvalidOperationException("LLM service unavailable"));
        var (config, _, error) = CreateConfig(orchestrator);

        var exitCode = await config.InvokeAsync(["spec"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("LLM service unavailable", error.ToString());
    }

    [Fact]
    public async Task Spec_OrchestratorInterrupted_ReturnsInterventionRequired()
    {
        var orchestrator = new ConfigurableOrchestrator(
            OrchestrationResult.Interrupted(1, WorkflowStep.DraftSpecification, "User input needed"));
        var (config, output, _) = CreateConfig(orchestrator);

        var exitCode = await config.InvokeAsync(["spec", "--headless", "--prompt", "test"]);

        Assert.Equal(ExitCodes.UserInterventionRequired, exitCode);
        Assert.Contains("interrupted", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== CLI-04: Plan — Prompt Passthrough & Failures ====================

    [Fact]
    public async Task Plan_WithPrompt_PassesPromptToOrchestrator()
    {
        var orchestrator = new ConfigurableOrchestrator(
            OrchestrationResult.Completed(1, WorkflowStep.DraftSpecification, "Done"));
        var (config, _, _) = CreateConfig(orchestrator);

        await config.InvokeAsync(["plan", "--prompt", "Focus on security"]);

        Assert.Equal("Focus on security", orchestrator.LastPrompt);
        Assert.Equal("auth", orchestrator.LastModule);
    }

    [Fact]
    public async Task Plan_OrchestratorFailure_ReturnsErrorExitCode()
    {
        var orchestrator = new ThrowingOrchestrator(new InvalidOperationException("Plan generation failed"));
        var (config, _, error) = CreateConfig(orchestrator);

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("Plan generation failed", error.ToString());
    }

    // ==================== CLI-05: Build — Prompt Passthrough & Failures ====================

    [Fact]
    public async Task Build_WithPrompt_PassesPromptToOrchestrator()
    {
        var orchestrator = new ConfigurableOrchestrator(
            OrchestrationResult.Completed(1, WorkflowStep.DraftSpecification, "Done"));
        var (config, _, _) = CreateConfig(orchestrator);

        await config.InvokeAsync(["build", "--prompt", "Skip tests"]);

        Assert.Equal("Skip tests", orchestrator.LastPrompt);
        Assert.Equal("auth", orchestrator.LastModule);
    }

    [Fact]
    public async Task Build_OrchestratorFailure_ReturnsErrorExitCode()
    {
        var orchestrator = new ThrowingOrchestrator(new InvalidOperationException("Build step crashed"));
        var (config, _, error) = CreateConfig(orchestrator);

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("Build step crashed", error.ToString());
    }

    // ==================== Cross-cutting: Null orchestrator ====================

    [Theory]
    [InlineData("spec")]
    [InlineData("plan")]
    [InlineData("build")]
    public async Task AllPhaseCommands_NullOrchestrator_ReturnsSuccess(string command)
    {
        var (config, _, _) = CreateConfig(orchestrator: null, registerOrchestrator: false);

        var exitCode = await config.InvokeAsync([command]);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    // ==================== Test-local fakes ====================

    /// <summary>
    /// Orchestrator that always throws the configured exception.
    /// </summary>
    private sealed class ThrowingOrchestrator : IWorkflowOrchestrator
    {
        private readonly Exception _exception;

        public ThrowingOrchestrator(Exception exception) => _exception = exception;

        public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
            => throw _exception;

        public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
            => throw _exception;
    }

    /// <summary>
    /// Orchestrator that returns a preconfigured result and captures call arguments.
    /// </summary>
    private sealed class ConfigurableOrchestrator : IWorkflowOrchestrator
    {
        private readonly OrchestrationResult _result;

        public string? LastModule { get; private set; }
        public string? LastPrompt { get; private set; }

        public ConfigurableOrchestrator(OrchestrationResult result) => _result = result;

        public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
        {
            LastModule = moduleName;
            LastPrompt = userPrompt;
            return Task.FromResult(_result);
        }

        public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
        {
            LastModule = moduleName;
            LastPrompt = userPrompt;
            return Task.FromResult(StepResult.Succeeded(WorkflowTrigger.Assess, "Done"));
        }
    }
}
