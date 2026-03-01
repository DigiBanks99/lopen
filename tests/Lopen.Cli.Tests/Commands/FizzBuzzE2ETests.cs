using System.CommandLine;
using Lopen.Auth;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lopen.Cli.Tests.Commands;

/// <summary>
/// E2E integration tests for CLI-28 (JOB-002) that exercise the REAL WorkflowOrchestrator
/// with a ScriptedLlmService fake returning pre-canned responses.
/// These tests wire up the full DI container with real services and a temp directory.
/// </summary>
public class FizzBuzzE2ETests : IDisposable
{
    private const string FizzBuzzModule = "fizzbuzz";
    private const string FizzBuzzPrompt = "Build a fizz-buzz application that prints numbers 1-100";

    private readonly string _tempDir;

    public FizzBuzzE2ETests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lopen-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    // ==================== Test 1: Spec Phase — Human gate returns Interrupted ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task SpecPhase_WithRealOrchestrator_InvokesLlmAndReturnsInterrupted()
    {
        // Arrange: create module directory without SPECIFICATION.md
        CreateModuleDirectory(FizzBuzzModule, createSpec: false);

        var scriptedLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("# FizzBuzz Specification\n\nDrafted by LLM."));

        var (config, output, error, host, sessionId) = await CreateE2EConfigAsync(scriptedLlm);

        // Act
        var exitCode = await config.InvokeAsync(["spec", "--headless", "--prompt", FizzBuzzPrompt, "--resume", sessionId.ToString()]);

        // Assert: LLM was called at least once for spec drafting
        Assert.True(scriptedLlm.InvokeCount >= 1, $"Expected at least 1 LLM invocation, got {scriptedLlm.InvokeCount}");

        // Spec phase has a human gate, so headless returns UserInterventionRequired
        Assert.Equal(ExitCodes.UserInterventionRequired, exitCode);
    }

    // ==================== Test 2: Spec Phase — Headless mode also returns Interrupted ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task SpecPhase_Headless_WithRealOrchestrator_InvokesLlm()
    {
        // Arrange
        CreateModuleDirectory(FizzBuzzModule, createSpec: false);

        var scriptedLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("# FizzBuzz Specification\n\nHeadless draft."));

        var (config, output, _, _, sessionId) = await CreateE2EConfigAsync(scriptedLlm);

        // Act
        var exitCode = await config.InvokeAsync(["spec", "--headless", "--prompt", FizzBuzzPrompt, "--resume", sessionId.ToString()]);

        // Assert
        Assert.True(scriptedLlm.InvokeCount >= 1, $"Expected at least 1 LLM invocation, got {scriptedLlm.InvokeCount}");
        // Spec always needs human confirmation, so headless returns UserInterventionRequired
        Assert.Equal(ExitCodes.UserInterventionRequired, exitCode);
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== Test 3: Plan Phase — Multiple LLM calls through planning ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task PlanPhase_WithRealOrchestrator_RunsMultipleLlmCalls()
    {
        // Arrange: create module with a valid SPECIFICATION.md (>100 chars, unchecked checkboxes)
        // Unchecked checkboxes ensure HasMoreComponents returns true, driving multi-step planning.
        var specContent = CreateInProgressSpecContent();
        CreateModuleDirectory(FizzBuzzModule, createSpec: true, specContent: specContent);

        // Provide enough responses for planning steps:
        // DetermineDeps, IdentifyComponents, SelectNextComponent, BreakIntoTasks, etc.
        var scriptedLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("Dependencies determined: none needed"),
            ScriptedLlmService.CreateResponse("Components identified: FizzBuzz logic, Output formatter"),
            ScriptedLlmService.CreateResponse("Selected component: FizzBuzz logic"),
            ScriptedLlmService.CreateResponse("Tasks: 1. Create FizzBuzz function 2. Add tests"),
            ScriptedLlmService.CreateResponse("Task iteration: implementing FizzBuzz"),
            ScriptedLlmService.CreateResponse("Task iteration: tests passing"),
            ScriptedLlmService.CreateResponse("Repeat check"),
            ScriptedLlmService.CreateResponse("All components complete"));

        var (config, output, error, _, sessionId) = await CreateE2EConfigAsync(scriptedLlm, approveSpec: true);

        // Act
        var exitCode = await config.InvokeAsync(["plan", "--headless", "--prompt", "Plan the fizzbuzz module", "--resume", sessionId.ToString()]);

        // Assert: multiple LLM invocations happened (at least 4 for planning steps)
        Assert.True(scriptedLlm.InvokeCount >= 4,
            $"Expected at least 4 LLM invocations for planning, got {scriptedLlm.InvokeCount}");

        // The orchestrator should have progressed through the workflow
        var outputText = output.ToString();
        Assert.Contains("planning", outputText, StringComparison.OrdinalIgnoreCase);
    }

    // ==================== Test 4: Build Phase — LLM invoked and completes when all tasks done ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task BuildPhase_WithRealOrchestrator_InvokesLlmAndCompletes()
    {
        // Arrange: spec with ALL checkboxes checked → state assessor returns Repeat
        // Plan file must exist for ValidatePlanExistsAsync to pass
        var specContent = CreateCompletedSpecContent();
        CreateModuleDirectory(FizzBuzzModule, createSpec: true, specContent: specContent);
        CreatePlanFile(FizzBuzzModule, "# FizzBuzz Plan\n- [x] Implement FizzBuzz logic\n- [x] Add unit tests");

        // Repeat step calls LLM once, then HasMoreComponentsAsync returns false → ModuleComplete
        var scriptedLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("Assessment: all components complete"));

        var (config, output, error, _, sessionId) = await CreateE2EConfigAsync(scriptedLlm, approveSpec: true);

        // Act
        var exitCode = await config.InvokeAsync(["build", "--headless", "--prompt", "Build the fizzbuzz application", "--resume", sessionId.ToString()]);

        // Assert: LLM was called for the Repeat assessment step
        Assert.True(scriptedLlm.InvokeCount >= 1,
            $"Expected at least 1 LLM invocation for build, got {scriptedLlm.InvokeCount}");

        // Build should complete successfully since all checkboxes are checked
        var outputText = output.ToString();
        Assert.Contains("building", outputText, StringComparison.OrdinalIgnoreCase);
    }

    // ==================== Test 5: Build Phase — iterates through tasks with unchecked items ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task BuildPhase_WithUnfinishedTasks_InvokesLlmMultipleTimes()
    {
        // Arrange: spec with some unchecked checkboxes → state assessor returns IterateThroughTasks or DetermineDependencies
        var specContent = CreateInProgressSpecContent();
        CreateModuleDirectory(FizzBuzzModule, createSpec: true, specContent: specContent);
        CreatePlanFile(FizzBuzzModule, "# FizzBuzz Plan\n- [ ] Implement FizzBuzz logic\n- [ ] Add unit tests");

        // Provide responses for multiple iterations
        var scriptedLlm = new ScriptedLlmService(
            Enumerable.Range(0, 20).Select(i =>
                ScriptedLlmService.CreateResponse($"Working on task iteration {i}")).ToArray());

        var (config2, output2, error2, _, sessionId2) = await CreateE2EConfigAsync(scriptedLlm, approveSpec: true);

        // Act
        var exitCode = await config2.InvokeAsync(["build", "--headless", "--prompt", "Build the fizzbuzz application", "--resume", sessionId2.ToString()]);

        // Assert: LLM was invoked multiple times for task iterations
        Assert.True(scriptedLlm.InvokeCount >= 2,
            $"Expected at least 2 LLM invocations for build with unfinished tasks, got {scriptedLlm.InvokeCount}");
    }

    // ==================== Test 6: Full Pipeline — spec → approval → plan → build ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task FullPipeline_SpecThenApprovedThenPlan_TransitionsCorrectly()
    {
        // Phase 1: Spec — no spec exists, should invoke LLM and return interrupted (human gate)
        CreateModuleDirectory(FizzBuzzModule, createSpec: false);

        var specLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("# FizzBuzz Specification\n\nDrafted spec content."));

        var (specConfig, specOutput, _, _, specSessionId) = await CreateE2EConfigAsync(specLlm);

        var specExit = await specConfig.InvokeAsync(["spec", "--headless", "--prompt", FizzBuzzPrompt, "--resume", specSessionId.ToString()]);

        Assert.Equal(ExitCodes.UserInterventionRequired, specExit);
        Assert.True(specLlm.InvokeCount >= 1, "Spec phase should invoke LLM at least once");

        // Phase 2: Simulate spec approval and create the specification file
        var specContent = CreateInProgressSpecContent();
        CreateModuleDirectory(FizzBuzzModule, createSpec: true, specContent: specContent);

        var planLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("Dependencies: none"),
            ScriptedLlmService.CreateResponse("Components: FizzBuzz core"),
            ScriptedLlmService.CreateResponse("Selected: FizzBuzz core"),
            ScriptedLlmService.CreateResponse("Tasks: implement core logic"),
            ScriptedLlmService.CreateResponse("Iterating tasks"),
            ScriptedLlmService.CreateResponse("Complete"));

        var (planConfig, planOutput, _, _, planSessionId) = await CreateE2EConfigAsync(planLlm, approveSpec: true);

        var planExit = await planConfig.InvokeAsync(["plan", "--headless", "--prompt", "Plan the module", "--resume", planSessionId.ToString()]);

        // Plan should have invoked LLM multiple times
        Assert.True(planLlm.InvokeCount >= 1,
            $"Plan phase should invoke LLM, got {planLlm.InvokeCount} invocations");

        // Verify the output mentions planning
        Assert.Contains("planning", planOutput.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== Test 7: Full Pipeline — spec → plan → build completes ====================

    [Fact(Skip = "For now until we can configure the models so we don't use all the tokens")]
    public async Task FullPipeline_SpecThenPlanThenBuild_CompletesSuccessfully()
    {
        // PHASE 1: SPEC — gather requirements (no spec file on disk yet)
        // Headless spec always returns UserInterventionRequired due to the human gate.
        CreateModuleDirectory(FizzBuzzModule, createSpec: false);

        var specLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse(
                "# FizzBuzz Module\n\nA module that prints numbers 1-100, replacing multiples of 3 with Fizz, 5 with Buzz, and both with FizzBuzz.\n\n## Components\n- [ ] FizzBuzz logic\n- [ ] Console output\n- [ ] Unit tests"));

        var (specConfig, specOutput, specError, specHost, specSessionId) = await CreateE2EConfigAsync(specLlm);

        var specExitCode = await specConfig.InvokeAsync(["spec", "--headless", "--prompt", "Create a FizzBuzz module", "--resume", specSessionId.ToString()]);

        Assert.Equal(ExitCodes.UserInterventionRequired, specExitCode);
        Assert.True(specLlm.InvokeCount >= 1, $"Spec phase should invoke LLM at least once, got {specLlm.InvokeCount}");
        specHost.Dispose();

        // PHASE 2: Simulate user approval by writing the completed spec, then run PLAN.
        // Using completed spec (all [x]) so the assessor starts at Repeat and the orchestrator
        // reaches ModuleComplete via HasMoreComponentsAsync → false. This completes the plan
        // phase successfully because IterateThroughTasks has no ComponentComplete exit path.
        CreateModuleDirectory(FizzBuzzModule, createSpec: true, specContent: CreateCompletedSpecContent());

        var planLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("Assessment: all components planned"));

        var (planConfig, planOutput, planError, planHost, planSessionId) = await CreateE2EConfigAsync(planLlm, approveSpec: true);

        var planExitCode = await planConfig.InvokeAsync(["plan", "--headless", "--prompt", "Plan the FizzBuzz module", "--resume", planSessionId.ToString()]);

        Assert.Equal(ExitCodes.Success, planExitCode);
        Assert.True(planLlm.InvokeCount >= 1, $"Plan phase should invoke LLM at least once, got {planLlm.InvokeCount}");
        planHost.Dispose();

        // PHASE 3: Create plan file (required by ValidatePlanExistsAsync), then BUILD.
        // Completed spec → assessor starts at Repeat → ModuleComplete.
        CreatePlanFile(FizzBuzzModule, "# FizzBuzz Plan\n- [x] Implement FizzBuzz logic\n- [x] Add console output\n- [x] Write unit tests");

        var buildLlm = new ScriptedLlmService(
            ScriptedLlmService.CreateResponse("Assessment: all components complete"));

        var (buildConfig, buildOutput, buildError, buildHost, buildSessionId) = await CreateE2EConfigAsync(buildLlm, approveSpec: true);

        var buildExitCode = await buildConfig.InvokeAsync(["build", "--headless", "--prompt", "Build the FizzBuzz module", "--resume", buildSessionId.ToString()]);

        Assert.Equal(ExitCodes.Success, buildExitCode);
        Assert.True(buildLlm.InvokeCount >= 1, $"Build phase should invoke LLM at least once, got {buildLlm.InvokeCount}");
        buildHost.Dispose();
    }

    // ==================== Helpers ====================

    private void CreateModuleDirectory(string moduleName, bool createSpec, string? specContent = null)
    {
        var modulePath = Path.Combine(_tempDir, "docs", "requirements", moduleName);
        Directory.CreateDirectory(modulePath);

        if (createSpec)
        {
            var specPath = Path.Combine(modulePath, "SPECIFICATION.md");
            File.WriteAllText(specPath, specContent ?? CreateInProgressSpecContent());
        }
    }

    private void CreatePlanFile(string moduleName, string planContent)
    {
        var planDir = Path.Combine(_tempDir, ".lopen", "modules", moduleName);
        Directory.CreateDirectory(planDir);
        File.WriteAllText(Path.Combine(planDir, "plan.md"), planContent);
    }

    /// <summary>
    /// Spec with all checkboxes completed — used when the module should appear fully done.
    /// The CodebaseStateAssessor returns Repeat when all ACs are checked.
    /// </summary>
    private static string CreateCompletedSpecContent()
    {
        return """
               # FizzBuzz Module Specification

               ## Overview
               Build a fizz-buzz application that prints numbers from 1 to 100,
               replacing multiples of 3 with "Fizz", multiples of 5 with "Buzz",
               and multiples of both with "FizzBuzz".

               ## Acceptance Criteria
               - [x] Print numbers 1-100
               - [x] Replace multiples of 3 with Fizz
               - [x] Replace multiples of 5 with Buzz
               - [x] Replace multiples of both 3 and 5 with FizzBuzz
               - [x] Include unit tests for all cases

               ## Components
               - FizzBuzz logic engine
               - Console output formatter
               - Unit test suite
               """;
    }

    /// <summary>
    /// Spec with unchecked checkboxes — used for plan phase to force multi-step planning.
    /// The CodebaseStateAssessor returns DetermineDependencies when content >100 chars
    /// and some checkboxes are unchecked (HasMoreComponents returns true).
    /// </summary>
    private static string CreateInProgressSpecContent()
    {
        return """
               # FizzBuzz Module Specification

               ## Overview
               Build a fizz-buzz application that prints numbers from 1 to 100,
               replacing multiples of 3 with "Fizz", multiples of 5 with "Buzz",
               and multiples of both with "FizzBuzz".

               ## Acceptance Criteria
               - [ ] Print numbers 1-100
               - [ ] Replace multiples of 3 with Fizz
               - [ ] Replace multiples of 5 with Buzz
               - [ ] Replace multiples of both 3 and 5 with FizzBuzz
               - [ ] Include unit tests for all cases

               ## Components
               - FizzBuzz logic engine
               - Console output formatter
               - Unit test suite
               """;
    }

    private async Task<(CommandLineConfiguration config, StringWriter output, StringWriter error, IHost host, SessionId sessionId)> CreateE2EConfigAsync(
        ScriptedLlmService scriptedLlm,
        bool approveSpec = false)
    {
        // Configure with a high failure threshold to avoid churn guardrail blocking during multi-step tests
        var options = new LopenOptions { Workflow = new WorkflowOptions { FailureThreshold = 50 } };

        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration(options);
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddLopenStorage(_tempDir);
        builder.Services.AddLopenCore(_tempDir);
        builder.Services.AddLopenLlm();
        builder.Services.AddLopenTui();

        // Replace the real ILlmService with our scripted fake
        var descriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(ILlmService));
        if (descriptor != null)
            builder.Services.Remove(descriptor);
        builder.Services.AddSingleton<ILlmService>(scriptedLlm);

        var host = builder.Build();

        // Create a session so ResolveModuleNameAsync can find the module
        var sessionManager = host.Services.GetRequiredService<ISessionManager>();
        var sessionId = await sessionManager.CreateSessionAsync(FizzBuzzModule);

        // Optionally approve the spec to allow planning transitions
        if (approveSpec)
        {
            var phaseController = host.Services.GetRequiredService<IPhaseTransitionController>();
            phaseController.ApproveSpecification();
        }

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.Add(PhaseCommands.CreateSpec(host.Services, output, error));
        root.Add(PhaseCommands.CreatePlan(host.Services, output, error));
        root.Add(PhaseCommands.CreateBuild(host.Services, output, error));

        return (new CommandLineConfiguration(root), output, error, host, sessionId);
    }
}
