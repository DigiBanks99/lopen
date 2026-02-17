using System.CommandLine;
using Lopen.Auth;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Lopen.Tui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lopen.Cli.Tests;

/// <summary>
/// Acceptance-criteria traceability tests for the CLI module (CLI-01 through CLI-28).
/// Each test maps to exactly one AC and proves the criterion is met with the minimum viable assertion.
/// </summary>
public class CliAcceptanceCriteriaTests
{
    // ==================== Shared helpers ====================

    private static readonly SessionId TestSession = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);

    private static readonly SessionState ActiveState = new()
    {
        SessionId = TestSession.ToString(),
        Phase = "building",
        Step = "execute-task",
        Module = "auth",
        CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
        LastTaskCompletionCommitSha = "abc123def456",
    };

    /// <summary>
    /// Creates a root-command config with the FakeTuiApplication for interactive (TUI) tests.
    /// </summary>
    private static (CommandLineConfiguration config, StringWriter output, StringWriter error, FakeTuiApplication tui) CreateRootConfig(
        IWorkflowOrchestrator? orchestrator = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);

        if (orchestrator is not null)
            builder.Services.AddSingleton(orchestrator);

        var host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();

        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        return (new CommandLineConfiguration(rootCommand), output, error, fakeTui);
    }

    /// <summary>
    /// Creates a phase-command config (spec, plan, build) with fakes.
    /// </summary>
    private static (CommandLineConfiguration config, StringWriter output, StringWriter error,
        FakeSessionManager sessions, FakeModuleScanner modules, FakePlanManager plans, FakeWorkflowOrchestrator orchestrator)
        CreatePhaseConfig()
    {
        var sessions = new FakeSessionManager();
        var modules = new FakeModuleScanner();
        var plans = new FakePlanManager();
        var orchestrator = new FakeWorkflowOrchestrator();

        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessions);
        services.AddSingleton<IModuleScanner>(modules);
        services.AddSingleton<IPlanManager>(plans);
        services.AddSingleton<IWorkflowOrchestrator>(orchestrator);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        GlobalOptions.AddTo(root);
        root.Add(PhaseCommands.CreateSpec(provider, output, error));
        root.Add(PhaseCommands.CreatePlan(provider, output, error));
        root.Add(PhaseCommands.CreateBuild(provider, output, error));

        return (new CommandLineConfiguration(root), output, error, sessions, modules, plans, orchestrator);
    }

    // ==================== CLI-01: Root starts TUI ====================

    [Fact]
    public async Task AC01_Root_NoArgs_LaunchesTui()
    {
        var (config, _, _, tui) = CreateRootConfig();

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.RunWasCalled, "Root command with no args must launch TUI");
    }

    // ==================== CLI-02: --headless runs without TUI ====================

    [Fact]
    public async Task AC02_Headless_DoesNotLaunchTui()
    {
        var (config, _, _, tui) = CreateRootConfig();

        await config.InvokeAsync(["--headless", "--prompt", "Build auth"]);

        Assert.False(tui.RunWasCalled, "TUI must not launch in headless mode");
    }

    // ==================== CLI-03: lopen spec invokes orchestrator with RequirementGathering ====================

    [Fact]
    public async Task AC03_Spec_InvokesOrchestrator()
    {
        var (config, output, _, sessions, _, _, orchestrator) = CreatePhaseConfig();
        sessions.AddSession(TestSession, ActiveState);
        sessions.SetLatestSessionId(TestSession);

        var exitCode = await config.InvokeAsync(["spec"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("auth", orchestrator.LastModule);
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== CLI-04: lopen plan errors without spec ====================

    [Fact]
    public async Task AC04_Plan_WithoutSpec_ReturnsError()
    {
        var (config, _, error, sessions, _, _, _) = CreatePhaseConfig();
        sessions.AddSession(TestSession, ActiveState);
        sessions.SetLatestSessionId(TestSession);

        var exitCode = await config.InvokeAsync(["plan"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No specification found", error.ToString());
    }

    // ==================== CLI-05: lopen build errors without spec/plan ====================

    [Fact]
    public async Task AC05_Build_WithoutSpecOrPlan_ReturnsError()
    {
        var (config, _, error, sessions, _, _, _) = CreatePhaseConfig();
        sessions.AddSession(TestSession, ActiveState);
        sessions.SetLatestSessionId(TestSession);

        var exitCode = await config.InvokeAsync(["build"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No specification found", error.ToString());
    }

    // ==================== CLI-06: lopen auth login calls LoginAsync ====================

    [Fact]
    public async Task AC06_AuthLogin_CallsLoginAsync()
    {
        var fakeAuth = new FakeAuthService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthService>(fakeAuth);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(AuthCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["auth", "login"]);

        Assert.Equal(0, exitCode);
        Assert.True(fakeAuth.LoginCalled);
    }

    // ==================== CLI-07: lopen auth status shows state ====================

    [Fact]
    public async Task AC07_AuthStatus_ShowsAuthState()
    {
        var fakeAuth = new FakeAuthService
        {
            StatusResult = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.SdkCredentials, "testuser"),
        };
        var services = new ServiceCollection();
        services.AddSingleton<IAuthService>(fakeAuth);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(AuthCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["auth", "status"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Authenticated", output.ToString());
    }

    // ==================== CLI-08: lopen auth logout calls LogoutAsync ====================

    [Fact]
    public async Task AC08_AuthLogout_CallsLogoutAsync()
    {
        var fakeAuth = new FakeAuthService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthService>(fakeAuth);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(AuthCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["auth", "logout"]);

        Assert.Equal(0, exitCode);
        Assert.True(fakeAuth.LogoutCalled);
    }

    // ==================== CLI-09: lopen session list ====================

    [Fact]
    public async Task AC09_SessionList_ListsSessions()
    {
        var sessionMgr = new FakeSessionManager();
        sessionMgr.AddSession(TestSession, ActiveState);
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionMgr);
        services.AddSingleton(new LopenOptions());
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["session", "list"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("auth-20260214-1", output.ToString());
    }

    // ==================== CLI-10: lopen session show ====================

    [Fact]
    public async Task AC10_SessionShow_DisplaysDetails()
    {
        var sessionMgr = new FakeSessionManager();
        sessionMgr.AddSession(TestSession, ActiveState);
        sessionMgr.SetLatestSessionId(TestSession);
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionMgr);
        services.AddSingleton(new LopenOptions());
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["session", "show"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("auth-20260214-1", output.ToString());
        Assert.Contains("building", output.ToString());
    }

    // ==================== CLI-11: lopen session resume ====================

    [Fact]
    public async Task AC11_SessionResume_ResumesSession()
    {
        var sessionMgr = new FakeSessionManager();
        sessionMgr.AddSession(TestSession, ActiveState);
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionMgr);
        services.AddSingleton(new LopenOptions());
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["session", "resume", "auth-20260214-1"]);

        Assert.Equal(0, exitCode);
        Assert.True(sessionMgr.SetLatestCalled);
        Assert.Equal(TestSession, sessionMgr.LastSetLatestSessionId);
    }

    // ==================== CLI-12: lopen session delete ====================

    [Fact]
    public async Task AC12_SessionDelete_DeletesSession()
    {
        var sessionMgr = new FakeSessionManager();
        sessionMgr.AddSession(TestSession, ActiveState);
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionMgr);
        services.AddSingleton(new LopenOptions());
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["session", "delete", "auth-20260214-1"]);

        Assert.Equal(0, exitCode);
        Assert.True(sessionMgr.DeleteCalled);
        Assert.Equal(TestSession, sessionMgr.LastDeletedSessionId);
    }

    // ==================== CLI-13: lopen session prune ====================

    [Fact]
    public async Task AC13_SessionPrune_PrunesSessions()
    {
        var sessionMgr = new FakeSessionManager();
        sessionMgr.PruneResult = 2;
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionMgr);
        services.AddSingleton(new LopenOptions());
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["session", "prune"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Pruned 2 session(s)", output.ToString());
    }

    // ==================== CLI-14: lopen config show ====================

    [Fact]
    public async Task AC14_ConfigShow_DisplaysConfig()
    {
        var configRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Lopen:Models:Primary"] = "gpt-5" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationRoot>(configRoot);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(ConfigCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["config", "show"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("gpt-5", output.ToString());
    }

    // ==================== CLI-15: lopen revert rolls back ====================

    [Fact]
    public async Task AC15_Revert_RollsBackToLastCommit()
    {
        var sessionMgr = new FakeSessionManager();
        sessionMgr.AddSession(TestSession, ActiveState);
        sessionMgr.SetLatestSessionId(TestSession);
        var revertSvc = new FakeRevertService();

        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(sessionMgr);
        services.AddSingleton<IRevertService>(revertSvc);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(RevertCommand.Create(provider, output, error));

        var exitCode = await new CommandLineConfiguration(root).InvokeAsync(["revert"]);

        Assert.Equal(0, exitCode);
        Assert.True(revertSvc.RevertCalled);
        Assert.Equal("abc123def456", revertSvc.LastCommitSha);
    }

    // ==================== CLI-16: --headless aliases (-q, --quiet) ====================

    [Fact]
    public void AC16_Headless_HasAliases()
    {
        var aliases = GlobalOptions.Headless.Aliases;
        Assert.Contains("-q", aliases);
        Assert.Contains("--quiet", aliases);
    }

    // ==================== CLI-17: --prompt passes to orchestrator ====================

    [Fact]
    public async Task AC17_Prompt_PassedToOrchestrator()
    {
        var (config, _, _, sessions, modules, _, orchestrator) = CreatePhaseConfig();
        sessions.AddSession(TestSession, ActiveState);
        sessions.SetLatestSessionId(TestSession);
        modules.AddModule("auth", hasSpec: true);

        var exitCode = await config.InvokeAsync(["spec", "--prompt", "Focus on auth"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("Focus on auth", orchestrator.LastPrompt);
    }

    // ==================== CLI-18: --prompt populates TUI input ====================

    [Fact]
    public async Task AC18_Prompt_PopulatesTuiInput()
    {
        var (config, _, _, tui) = CreateRootConfig();

        await config.InvokeAsync(["--prompt", "Focus on auth"]);

        Assert.True(tui.RunWasCalled);
        Assert.Equal("Focus on auth", tui.InitialPrompt);
    }

    // ==================== CLI-19: Headless without prompt/session errors ====================

    [Fact]
    public async Task AC19_Headless_NoPrompt_NoSession_ReturnsError()
    {
        var (config, _, error, tui) = CreateRootConfig();

        var exitCode = await config.InvokeAsync(["--headless"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--prompt", error.ToString());
        Assert.False(tui.RunWasCalled);
    }

    // ==================== CLI-20: Exit codes (0, 1, 2) ====================

    [Fact]
    public void AC20_ExitCodes_CorrectValues()
    {
        Assert.Equal(0, ExitCodes.Success);
        Assert.Equal(1, ExitCodes.Failure);
        Assert.Equal(2, ExitCodes.UserInterventionRequired);
    }

    // ==================== CLI-21: --help and --version work ====================

    [Fact]
    public async Task AC21_Help_And_Version_ReturnSuccess()
    {
        var root = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(root);
        root.SetAction((ParseResult _) => 0);
        var config = new CommandLineConfiguration(root);

        Assert.Equal(0, await config.InvokeAsync(["--help"]));
        Assert.Equal(0, await config.InvokeAsync(["--version"]));
    }

    // ==================== CLI-22: dotnet build succeeds ====================

    /// <summary>CLI-22: dotnet build must succeed. Verified by CI pipeline.</summary>
    [Fact(Skip = "CI-only build verification")]
    public void AC22_DotnetBuild_Succeeds()
    {
        // This AC is verified by the CI pipeline running `dotnet build` successfully.
    }

    // ==================== CLI-23: dotnet test succeeds ====================

    /// <summary>CLI-23: dotnet test must pass. Verified by CI pipeline.</summary>
    [Fact(Skip = "CI-only build verification")]
    public void AC23_DotnetTest_Succeeds()
    {
        // This AC is verified by the CI pipeline running `dotnet test` successfully.
    }

    // ==================== CLI-24: dotnet format passes ====================

    /// <summary>CLI-24: dotnet format must pass. Verified by CI pipeline.</summary>
    [Fact(Skip = "CI-only build verification")]
    public void AC24_DotnetFormat_Passes()
    {
        // This AC is verified by the CI pipeline running `dotnet format --verify-no-changes`.
    }

    // ==================== CLI-25: DI hosting resolves services ====================

    [Fact]
    public void AC25_DI_ResolvesServices()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        builder.Services.AddLopenTui();

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<IAuthService>());
        Assert.NotNull(host.Services.GetService<IFileSystem>());
    }

    // ==================== CLI-26: Project root discovery works ====================

    [Fact]
    public void AC26_ProjectRootDiscovery_FindsLopenDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"lopen-ac26-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, ".lopen"));

            var result = ProjectRootDiscovery.FindProjectRoot(tempDir);

            Assert.Equal(tempDir, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ==================== CLI-27: --no-welcome suppresses landing page ====================

    [Fact]
    public async Task AC27_NoWelcome_SuppressesLandingPage()
    {
        var (config, _, _, tui) = CreateRootConfig();

        var exitCode = await config.InvokeAsync(["--no-welcome"]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.LandingPageSuppressed);
        Assert.True(tui.RunWasCalled);
    }

    // ==================== CLI-28: FizzBuzz workflow (spec→plan→build succeeds) ====================

    [Fact]
    public async Task AC28_FizzBuzz_SpecPlanBuild_AllSucceed()
    {
        var fizzModule = "fizzbuzz";
        var fizzSession = SessionId.Generate(fizzModule, new DateOnly(2026, 2, 17), 1);
        var fizzState = new SessionState
        {
            SessionId = fizzSession.ToString(),
            Phase = "requirement-gathering",
            Step = "draft-specification",
            Module = fizzModule,
            CreatedAt = new DateTimeOffset(2026, 2, 17, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 2, 17, 10, 0, 0, TimeSpan.Zero),
        };

        // Phase 1: Spec
        var (specConfig, specOut, _, specSessions, specModules, _, specOrch) = CreatePhaseConfig();
        specSessions.AddSession(fizzSession, fizzState);
        specSessions.SetLatestSessionId(fizzSession);
        specModules.AddModule(fizzModule, hasSpec: false);
        var specExit = await specConfig.InvokeAsync(["spec", "--prompt", "Build fizzbuzz"]);
        Assert.Equal(0, specExit);
        Assert.Equal(fizzModule, specOrch.LastModule);

        // Phase 2: Plan (spec now exists)
        var (planConfig, _, _, planSessions, planModules, _, planOrch) = CreatePhaseConfig();
        planSessions.AddSession(fizzSession, fizzState);
        planSessions.SetLatestSessionId(fizzSession);
        planModules.AddModule(fizzModule, hasSpec: true);
        var planExit = await planConfig.InvokeAsync(["plan"]);
        Assert.Equal(0, planExit);
        Assert.Equal(fizzModule, planOrch.LastModule);

        // Phase 3: Build (spec + plan exist)
        var (buildConfig, _, _, buildSessions, buildModules, buildPlans, buildOrch) = CreatePhaseConfig();
        buildSessions.AddSession(fizzSession, fizzState);
        buildSessions.SetLatestSessionId(fizzSession);
        buildModules.AddModule(fizzModule, hasSpec: true);
        buildPlans.AddPlan(fizzModule, "# Plan\n- [ ] Implement FizzBuzz");
        var buildExit = await buildConfig.InvokeAsync(["build"]);
        Assert.Equal(0, buildExit);
        Assert.Equal(fizzModule, buildOrch.LastModule);
    }

    // ==================== Test-local TUI fake ====================

    private sealed class FakeTuiApplication : ITuiApplication
    {
        public bool RunWasCalled { get; private set; }
        public bool IsRunning { get; private set; }
        public string? InitialPrompt { get; private set; }
        public bool LandingPageSuppressed { get; private set; }

        public Task RunAsync(string? initialPrompt = null, CancellationToken cancellationToken = default)
        {
            RunWasCalled = true;
            IsRunning = true;
            InitialPrompt = initialPrompt;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public void SuppressLandingPage() => LandingPageSuppressed = true;
    }
}
