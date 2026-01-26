using Shouldly;
using Spectre.Console.Testing;

namespace Lopen.Core.Tests;

public class LoopServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly MockCopilotService _mockCopilotService;
    private readonly LoopStateManager _stateManager;
    private readonly LoopOutputService _outputService;
    private readonly LoopConfig _config;
    private readonly TestConsole _testConsole;

    public LoopServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        // Create git structure to simulate feature branch
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/feature/test");

        // Create prompt files
        File.WriteAllText(Path.Combine(_testDir, "PLAN.PROMPT.md"), "Plan prompt");
        File.WriteAllText(Path.Combine(_testDir, "BUILD.PROMPT.md"), "Build prompt");

        _mockCopilotService = new MockCopilotService();
        _stateManager = new LoopStateManager(_testDir);
        _testConsole = new TestConsole();
        var consoleOutput = new ConsoleOutput(_testConsole);
        _outputService = new LoopOutputService(consoleOutput);
        _config = new LoopConfig();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_OnMainBranch_ReturnsError()
    {
        // Set up main branch
        var gitDir = Path.Combine(_testDir, ".git");
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main");

        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        var result = await service.RunAsync();

        result.ShouldBe(ExitCodes.GeneralError);
        _testConsole.Output.ShouldContain("Cannot run loop on main/master branch");
    }

    [Fact]
    public async Task RunAsync_WithSkipPlan_OnlyRunsBuild()
    {
        // Create done file immediately to stop build loop
        await _stateManager.CreateDoneFileAsync("done");

        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        var result = await service.RunAsync(skipPlan: true);

        result.ShouldBe(ExitCodes.Success);
        _testConsole.Output.ShouldNotContain("PLAN");
        _testConsole.Output.ShouldContain("All jobs finished");
    }

    [Fact]
    public async Task RunAsync_WithSkipBuild_OnlyRunsPlan()
    {
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        var result = await service.RunAsync(skipBuild: true);

        result.ShouldBe(ExitCodes.Success);
        _testConsole.Output.ShouldContain("PLAN");
    }

    [Fact]
    public async Task RunPlanPhaseAsync_RemovesDoneFile()
    {
        await _stateManager.CreateDoneFileAsync("previous");
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        await service.RunPlanPhaseAsync();

        _stateManager.IsLoopComplete().ShouldBeFalse();
    }

    [Fact]
    public async Task RunPlanPhaseAsync_CreatesSession()
    {
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        await service.RunPlanPhaseAsync();

        _mockCopilotService.SessionsCreated.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RunPlanPhaseAsync_IncrementsIteration()
    {
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        await service.RunPlanPhaseAsync();

        _outputService.IterationCount.ShouldBe(1);
    }

    [Fact]
    public async Task RunPlanPhaseAsync_MissingPrompt_ShowsError()
    {
        File.Delete(Path.Combine(_testDir, "PLAN.PROMPT.md"));
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        await service.RunPlanPhaseAsync();

        _testConsole.Output.ShouldContain("Plan prompt not found");
    }

    [Fact]
    public async Task RunBuildPhaseAsync_ExitsWhenDoneFileExists()
    {
        await _stateManager.CreateDoneFileAsync("done");
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        var result = await service.RunBuildPhaseAsync();

        result.ShouldBe(ExitCodes.Success);
        _testConsole.Output.ShouldContain("All jobs finished");
    }

    [Fact]
    public async Task RunBuildPhaseAsync_MissingPrompt_ReturnsError()
    {
        File.Delete(Path.Combine(_testDir, "BUILD.PROMPT.md"));
        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        var result = await service.RunBuildPhaseAsync();

        result.ShouldBe(ExitCodes.GeneralError);
        _testConsole.Output.ShouldContain("Build prompt not found");
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_ExitsGracefully()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new LoopService(_mockCopilotService, _stateManager, _outputService, _config);

        var result = await service.RunAsync(ct: cts.Token);

        result.ShouldBe(ExitCodes.Success);
        _testConsole.Output.ShouldContain("Loop cancelled");
    }
}
