using System.CommandLine;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

public class PhaseCommandTests
{
    private readonly FakeSessionManager _fakeSessionManager = new();
    private readonly FakeModuleScanner _fakeModuleScanner = new();
    private readonly FakePlanManager _fakePlanManager = new();

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

    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(_fakeSessionManager);
        services.AddSingleton<IModuleScanner>(_fakeModuleScanner);
        services.AddSingleton<IPlanManager>(_fakePlanManager);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.Add(PhaseCommands.CreateSpec(provider, output, error));
        root.Add(PhaseCommands.CreatePlan(provider, output, error));
        root.Add(PhaseCommands.CreateBuild(provider, output, error));

        var config = new CommandLineConfiguration(root);
        return (config, output, error);
    }

    // ==================== SPEC TESTS ====================

    [Fact]
    public async Task Spec_Succeeds()
    {
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== PLAN TESTS ====================

    [Fact]
    public async Task Plan_NoSession_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No active session", error.ToString());
    }

    [Fact]
    public async Task Plan_NoSpec_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        // Module scanner has no modules
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No specification found", error.ToString());
        Assert.Contains("auth", error.ToString());
    }

    [Fact]
    public async Task Plan_WithSpec_Succeeds()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        _fakeModuleScanner.AddModule("auth", hasSpec: true);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("planning phase", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plan_SpecWithoutContent_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        _fakeModuleScanner.AddModule("auth", hasSpec: false);
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No specification found", error.ToString());
    }

    // ==================== BUILD TESTS ====================

    [Fact]
    public async Task Build_NoSession_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No active session", error.ToString());
    }

    [Fact]
    public async Task Build_NoSpec_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No specification found", error.ToString());
    }

    [Fact]
    public async Task Build_NoPlan_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        _fakeModuleScanner.AddModule("auth", hasSpec: true);
        // No plan added
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No plan found", error.ToString());
        Assert.Contains("auth", error.ToString());
    }

    [Fact]
    public async Task Build_WithSpecAndPlan_Succeeds()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        _fakeModuleScanner.AddModule("auth", hasSpec: true);
        _fakePlanManager.AddPlan("auth", "# Plan\n- [x] Task 1\n- [ ] Task 2");
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("building phase", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== HEADLESS + PROMPT TESTS (AC-19) ====================

    [Theory]
    [InlineData("spec")]
    [InlineData("plan")]
    [InlineData("build")]
    public async Task Headless_NoPrompt_NoSession_ReturnsExitCode1(string command)
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync([command, "--headless"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Headless mode requires --prompt", error.ToString());
    }

    [Fact]
    public async Task Spec_Headless_WithPrompt_Succeeds()
    {
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--headless", "--prompt", "Build an auth module"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Spec_Headless_WithActiveSession_Succeeds()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--headless"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Spec_Quiet_Alias_Works()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "-q"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Headless mode requires --prompt", error.ToString());
    }

    // ==================== EXIT CODE CONSTANTS ====================

    [Fact]
    public void ExitCodes_AreCorrect()
    {
        Assert.Equal(0, ExitCodes.Success);
        Assert.Equal(1, ExitCodes.Failure);
        Assert.Equal(2, ExitCodes.UserInterventionRequired);
    }

    // ==================== SESSION RESUME TESTS (JOB-037) ====================

    [Fact]
    public async Task Spec_Resume_SpecificSession_Succeeds()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--resume", Session1.ToString()]);

        Assert.Equal(0, exitCode);
        Assert.Contains($"Resuming session: {Session1}", output.ToString());
        Assert.True(_fakeSessionManager.SetLatestCalled);
    }

    [Fact]
    public async Task Spec_Resume_InvalidFormat_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--resume", "not-valid-id"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid session ID format", error.ToString());
    }

    [Fact]
    public async Task Spec_Resume_NotFound_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--resume", "auth-20260214-99"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Session not found", error.ToString());
    }

    [Fact]
    public async Task Spec_Resume_CompletedSession_ReturnsExitCode1()
    {
        var completedState = new SessionState
        {
            SessionId = Session1.ToString(),
            Phase = "complete",
            Step = "done",
            Module = "auth",
            CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
        };
        _fakeSessionManager.AddSession(Session1, completedState);
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--resume", Session1.ToString()]);

        Assert.Equal(1, exitCode);
        Assert.Contains("already complete", error.ToString());
    }

    [Fact]
    public async Task Spec_NoResume_StartsFresh()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec", "--no-resume"]);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Resuming session", output.ToString());
    }

    [Fact]
    public async Task Spec_AutoResume_LatestActiveSession()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec"]);

        Assert.Equal(0, exitCode);
        Assert.Contains($"Resuming session: {Session1}", output.ToString());
    }

    [Fact]
    public async Task Spec_AutoResume_SkipsCompletedSession()
    {
        var completedState = new SessionState
        {
            SessionId = Session1.ToString(),
            Phase = "complete",
            Step = "done",
            Module = "auth",
            CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
        };
        _fakeSessionManager.AddSession(Session1, completedState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["spec"]);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Resuming session", output.ToString());
    }
}
