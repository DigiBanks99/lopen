using System.CommandLine;
using Lopen.Commands;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

/// <summary>
/// End-to-end integration tests simulating the fizz-buzz workflow through CLI phases.
/// Covers CLI-28: Run the CLI workflow to create a fizz-buzz application with tests.
/// </summary>
public class FizzBuzzWorkflowTests
{
    private const string FizzBuzzModule = "fizzbuzz";
    private const string FizzBuzzPrompt = "Build a fizz-buzz application that prints numbers 1-100, replacing multiples of 3 with Fizz, multiples of 5 with Buzz, and multiples of both with FizzBuzz";
    private static readonly SessionId FizzBuzzSession = SessionId.Generate(FizzBuzzModule, new DateOnly(2026, 2, 17), 1);

    private static readonly SessionState FizzBuzzState = new()
    {
        SessionId = FizzBuzzSession.ToString(),
        Phase = "requirement-gathering",
        Step = "draft-specification",
        Module = FizzBuzzModule,
        CreatedAt = new DateTimeOffset(2026, 2, 17, 10, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 17, 10, 0, 0, TimeSpan.Zero),
    };

    // ==================== CLI-28: Spec Phase ====================

    [Fact]
    public async Task Spec_WithFizzBuzzPrompt_PassesPromptToOrchestrator()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, output, _) = CreateConfig(orchestrator, hasSpec: false, hasPlan: false);

        var exitCode = await config.InvokeAsync(["spec", "--prompt", FizzBuzzPrompt]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(FizzBuzzModule, orchestrator.LastModule);
        Assert.Equal(FizzBuzzPrompt, orchestrator.LastPrompt);
    }

    [Fact]
    public async Task Spec_Headless_WithFizzBuzzPrompt_CompletesSuccessfully()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, output, _) = CreateConfig(orchestrator, hasSpec: false, hasPlan: false);

        var exitCode = await config.InvokeAsync(["spec", "--headless", "--prompt", FizzBuzzPrompt]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(FizzBuzzModule, orchestrator.LastModule);
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== CLI-28: Plan Phase ====================

    [Fact]
    public async Task Plan_AfterSpec_PassesModuleToOrchestrator()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, _, _) = CreateConfig(orchestrator, hasSpec: true, hasPlan: false);

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(FizzBuzzModule, orchestrator.LastModule);
    }

    [Fact]
    public async Task Plan_WithoutSpec_ReturnsFailure()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, _, error) = CreateConfig(orchestrator, hasSpec: false, hasPlan: false);

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("specification", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== CLI-28: Build Phase ====================

    [Fact]
    public async Task Build_AfterSpecAndPlan_PassesModuleToOrchestrator()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, _, _) = CreateConfig(orchestrator, hasSpec: true, hasPlan: true);

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(FizzBuzzModule, orchestrator.LastModule);
    }

    [Fact]
    public async Task Build_WithoutPlan_ReturnsFailure()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, _, error) = CreateConfig(orchestrator, hasSpec: true, hasPlan: false);

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("plan", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Build_WithoutSpec_ReturnsFailure()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, _, error) = CreateConfig(orchestrator, hasSpec: false, hasPlan: false);

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("specification", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== CLI-28: Full Sequential Workflow ====================

    [Fact]
    public async Task FullWorkflow_SpecThenPlanThenBuild_AllSucceed()
    {
        var specOrchestrator = new TrackingOrchestrator();
        var planOrchestrator = new TrackingOrchestrator();
        var buildOrchestrator = new TrackingOrchestrator();

        // Phase 1: Spec — no spec or plan exists yet
        var (specConfig, specOutput, _) = CreateConfig(specOrchestrator, hasSpec: false, hasPlan: false);
        var specExit = await specConfig.InvokeAsync(["spec", "--prompt", FizzBuzzPrompt]);
        Assert.Equal(ExitCodes.Success, specExit);
        Assert.Equal(FizzBuzzModule, specOrchestrator.LastModule);
        Assert.Equal(FizzBuzzPrompt, specOrchestrator.LastPrompt);

        // Phase 2: Plan — spec now exists
        var (planConfig, planOutput, _) = CreateConfig(planOrchestrator, hasSpec: true, hasPlan: false);
        var planExit = await planConfig.InvokeAsync(["plan"]);
        Assert.Equal(ExitCodes.Success, planExit);
        Assert.Equal(FizzBuzzModule, planOrchestrator.LastModule);

        // Phase 3: Build — spec and plan exist
        var (buildConfig, buildOutput, _) = CreateConfig(buildOrchestrator, hasSpec: true, hasPlan: true);
        var buildExit = await buildConfig.InvokeAsync(["build"]);
        Assert.Equal(ExitCodes.Success, buildExit);
        Assert.Equal(FizzBuzzModule, buildOrchestrator.LastModule);
    }

    [Fact]
    public async Task FullWorkflow_Headless_SpecThenPlanThenBuild_AllSucceed()
    {
        var specOrchestrator = new TrackingOrchestrator();
        var planOrchestrator = new TrackingOrchestrator();
        var buildOrchestrator = new TrackingOrchestrator();

        // Phase 1: Spec headless with prompt
        var (specConfig, specOutput, _) = CreateConfig(specOrchestrator, hasSpec: false, hasPlan: false);
        var specExit = await specConfig.InvokeAsync(["spec", "--headless", "--prompt", FizzBuzzPrompt]);
        Assert.Equal(ExitCodes.Success, specExit);
        Assert.Contains("requirement gathering", specOutput.ToString(), StringComparison.OrdinalIgnoreCase);

        // Phase 2: Plan headless with prompt
        var (planConfig, planOutput, _) = CreateConfig(planOrchestrator, hasSpec: true, hasPlan: false);
        var planExit = await planConfig.InvokeAsync(["plan", "--headless", "--prompt", "Generate a plan for the fizz-buzz module"]);
        Assert.Equal(ExitCodes.Success, planExit);
        Assert.Contains("planning", planOutput.ToString(), StringComparison.OrdinalIgnoreCase);

        // Phase 3: Build headless with prompt
        var (buildConfig, buildOutput, _) = CreateConfig(buildOrchestrator, hasSpec: true, hasPlan: true);
        var buildExit = await buildConfig.InvokeAsync(["build", "--headless", "--prompt", "Build the fizz-buzz application with tests"]);
        Assert.Equal(ExitCodes.Success, buildExit);
        Assert.Contains("building", buildOutput.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullWorkflow_BuildWithPrompt_PassesFizzBuzzInstructions()
    {
        var orchestrator = new TrackingOrchestrator();
        var (config, _, _) = CreateConfig(orchestrator, hasSpec: true, hasPlan: true);
        const string buildPrompt = "Implement FizzBuzz: for numbers 1-100, print Fizz for multiples of 3, Buzz for multiples of 5, FizzBuzz for both";

        var exitCode = await config.InvokeAsync(["build", "--prompt", buildPrompt]);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(FizzBuzzModule, orchestrator.LastModule);
        Assert.Equal(buildPrompt, orchestrator.LastPrompt);
    }

    // ==================== Helper ====================

    private static (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig(
        TrackingOrchestrator orchestrator,
        bool hasSpec,
        bool hasPlan)
    {
        var sessionManager = new Fakes.FakeSessionManager();
        var moduleScanner = new Fakes.FakeModuleScanner();
        var planManager = new Fakes.FakePlanManager();

        sessionManager.AddSession(FizzBuzzSession, FizzBuzzState);
        sessionManager.SetLatestSessionId(FizzBuzzSession);

        if (hasSpec)
            moduleScanner.AddModule(FizzBuzzModule, hasSpec: true);
        else
            moduleScanner.AddModule(FizzBuzzModule, hasSpec: false);

        if (hasPlan)
            planManager.AddPlan(FizzBuzzModule, "# FizzBuzz Plan\n- [ ] Implement FizzBuzz logic\n- [ ] Add unit tests");

        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionManager);
        services.AddSingleton<IModuleScanner>(moduleScanner);
        services.AddSingleton<IPlanManager>(planManager);
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

    // ==================== Test-local fake ====================

    /// <summary>
    /// Orchestrator that tracks calls and returns completed result.
    /// </summary>
    private sealed class TrackingOrchestrator : IWorkflowOrchestrator
    {
        public string? LastModule { get; private set; }
        public string? LastPrompt { get; private set; }
        public int CallCount { get; private set; }

        public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
        {
            LastModule = moduleName;
            LastPrompt = userPrompt;
            CallCount++;
            return Task.FromResult(OrchestrationResult.Completed(1, WorkflowStep.DraftSpecification, "Completed"));
        }

        public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
        {
            LastModule = moduleName;
            LastPrompt = userPrompt;
            CallCount++;
            return Task.FromResult(StepResult.Succeeded(WorkflowTrigger.Assess, "Step complete"));
        }
    }
}
