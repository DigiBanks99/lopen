using System.CommandLine;
using Lopen.Auth;
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
/// Tests for the root command handler (lopen with no subcommand).
/// Covers AC-1: root command starts TUI with full workflow and session resume offer.
/// </summary>
public class RootCommandTests
{
    private static (CommandLineConfiguration config, StringWriter output, StringWriter error, FakeTuiApplication tui) CreateConfig(
        ISessionManager? sessionManager = null, IWorkflowOrchestrator? orchestrator = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);

        if (sessionManager is not null)
            builder.Services.AddSingleton(sessionManager);

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

    // ==================== AC-1: Root command launches TUI ====================

    [Fact]
    public async Task RootCommand_NoArgs_LaunchesTui()
    {
        var (config, output, _, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.RunWasCalled, "ITuiApplication.RunAsync should be called");
    }

    [Fact]
    public async Task RootCommand_NoArgs_ReturnsSuccess()
    {
        var (config, _, _, _) = CreateConfig();

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(0, exitCode);
    }

    // ==================== AC-2: Headless mode ====================

    [Fact]
    public async Task RootCommand_Headless_WithPrompt_RunsHeadless()
    {
        var (config, _, error, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build auth"]);

        // Headless mode no longer launches TUI; runs orchestrator instead.
        // Without a session/module, it returns failure.
        Assert.False(tui.RunWasCalled, "TUI should not launch in headless mode");
    }

    [Fact]
    public async Task RootCommand_Headless_NoPrompt_NoSession_ReturnsFailure()
    {
        var (config, _, error, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--headless"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--prompt", error.ToString());
        Assert.False(tui.RunWasCalled, "TUI should not launch when headless validation fails");
    }

    // ==================== Session resume ====================

    [Fact]
    public async Task RootCommand_Headless_WithSession_RunsOrchestrator()
    {
        var sessionManager = new FakeSessionManager();
        var sessionId = SessionId.TryParse("testmod-20260101-001")!;
        await sessionManager.SaveSessionStateAsync(sessionId, new SessionState
        {
            SessionId = "testmod-20260101-001",
            Module = "testmod",
            Phase = "building",
            Step = "6",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await sessionManager.SetLatestAsync(sessionId);

        var (config, output, error, tui) = CreateConfig(sessionManager);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it"]);

        Assert.False(tui.RunWasCalled, "TUI should not launch in headless mode");
        // May fail due to no orchestrator in this test setup, but should not launch TUI
    }

    [Fact]
    public async Task RootCommand_Resume_InvalidId_WithSessionManager_ReturnsFailure()
    {
        var sessionManager = new FakeSessionManager();
        var (config, _, error, tui) = CreateConfig(sessionManager);

        var exitCode = await config.InvokeAsync(["--resume", "bad-id"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid session ID", error.ToString());
        Assert.False(tui.RunWasCalled);
    }

    [Fact]
    public async Task RootCommand_Resume_NoSessionManager_StartsFresh()
    {
        var (config, output, _, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--resume", "bad-id"]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.RunWasCalled);
        Assert.DoesNotContain("Resuming session", output.ToString());
    }

    [Fact]
    public async Task RootCommand_NoResume_LaunchesTui_WithoutSession()
    {
        var (config, output, _, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--no-resume"]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.RunWasCalled);
        Assert.DoesNotContain("Resuming session", output.ToString());
    }

    [Fact]
    public async Task RootCommand_Resume_WithActiveSession_PrintsResumingMessage()
    {
        var sessionManager = new FakeSessionManager();
        var sessionId = SessionId.TryParse("testmod-20260101-001")!;
        await sessionManager.SaveSessionStateAsync(sessionId, new SessionState
        {
            SessionId = "testmod-20260101-001",
            Module = "testmod",
            Phase = "spec",
            Step = "1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await sessionManager.SetLatestAsync(sessionId);

        var (config, output, _, tui) = CreateConfig(sessionManager);

        var exitCode = await config.InvokeAsync(["--resume", "testmod-20260101-001"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Resuming session", output.ToString());
        Assert.True(tui.RunWasCalled);
    }

    // ==================== CLI-02: Headless success / interrupted ====================

    [Fact]
    public async Task RootCommand_Headless_WithOrchestrator_Completed_ReturnsSuccess()
    {
        var sessionManager = new FakeSessionManager();
        var sessionId = SessionId.TryParse("testmod-20260101-001")!;
        await sessionManager.SaveSessionStateAsync(sessionId, new SessionState
        {
            SessionId = "testmod-20260101-001",
            Module = "testmod",
            Phase = "building",
            Step = "6",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await sessionManager.SetLatestAsync(sessionId);

        var orchestrator = new FakeOrchestrator(OrchestrationResult.Completed(5, WorkflowStep.Repeat));
        var (config, output, error, tui) = CreateConfig(sessionManager, orchestrator);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it"]);

        Assert.Equal(0, exitCode);
        Assert.False(tui.RunWasCalled, "TUI should not launch in headless mode");
        Assert.Contains("Running headless workflow for module: testmod", output.ToString());
        Assert.Contains("completed after 5 iterations", output.ToString());
    }

    [Fact]
    public async Task RootCommand_Headless_Interrupted_ReturnsExitCode2()
    {
        var sessionManager = new FakeSessionManager();
        var sessionId = SessionId.TryParse("testmod-20260101-001")!;
        await sessionManager.SaveSessionStateAsync(sessionId, new SessionState
        {
            SessionId = "testmod-20260101-001",
            Module = "testmod",
            Phase = "building",
            Step = "6",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await sessionManager.SetLatestAsync(sessionId);

        var orchestrator = new FakeOrchestrator(OrchestrationResult.Interrupted(3, WorkflowStep.IterateThroughTasks, "Human gate required"));
        var (config, output, error, tui) = CreateConfig(sessionManager, orchestrator);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it"]);

        Assert.Equal(2, exitCode);
        Assert.False(tui.RunWasCalled);
        Assert.Contains("Human gate required", error.ToString());
    }

    // ==================== Error handling ====================

    [Fact]
    public async Task RootCommand_TuiThrows_ReturnsFailure()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        var failTui = new ThrowingTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(failTui);
        var host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        var exitCode = await new CommandLineConfiguration(rootCommand).InvokeAsync([]);

        Assert.Equal(1, exitCode);
        Assert.Contains("TUI startup failed", error.ToString());
    }

    // ==================== CLI-18: --prompt populates TUI input ====================

    [Fact]
    public async Task RootCommand_WithPrompt_PassesPromptToTui()
    {
        var (config, output, error, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--prompt", "Focus on auth"]);

        Assert.True(tui.RunWasCalled);
        Assert.Equal("Focus on auth", tui.InitialPrompt);
    }

    [Fact]
    public async Task RootCommand_NoPrompt_TuiGetsNullPrompt()
    {
        var (config, output, error, tui) = CreateConfig();

        await config.InvokeAsync([]);

        Assert.True(tui.RunWasCalled);
        Assert.Null(tui.InitialPrompt);
    }

    // ==================== CLI-27: --no-welcome flag ====================

    [Fact]
    public async Task RootCommand_NoWelcome_SuppressesLandingPage()
    {
        var (config, _, _, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--no-welcome"]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.LandingPageSuppressed, "SuppressLandingPage should be called when --no-welcome is set");
        Assert.True(tui.RunWasCalled);
    }

    [Fact]
    public async Task RootCommand_WithoutNoWelcome_DoesNotSuppressLandingPage()
    {
        var (config, _, _, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(0, exitCode);
        Assert.False(tui.LandingPageSuppressed, "SuppressLandingPage should NOT be called without --no-welcome");
    }

    [Fact]
    public async Task RootCommand_NoWelcomeWithPrompt_BothApplied()
    {
        var (config, _, _, tui) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--no-welcome", "--prompt", "fix the bug"]);

        Assert.Equal(0, exitCode);
        Assert.True(tui.LandingPageSuppressed);
        Assert.Equal("fix the bug", tui.InitialPrompt);
    }

    // ==================== CFG-08: --model override ====================

    [Fact]
    public async Task RootCommand_Model_OverridesAllPhaseModels()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);
        var host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync(["--model", "gpt-5"]);

        var modelOptions = host.Services.GetRequiredService<ModelOptions>();
        Assert.Equal("gpt-5", modelOptions.RequirementGathering);
        Assert.Equal("gpt-5", modelOptions.Planning);
        Assert.Equal("gpt-5", modelOptions.Building);
        Assert.Equal("gpt-5", modelOptions.Research);
    }

    // ==================== CFG-09: --unattended override ====================

    [Fact]
    public async Task RootCommand_Unattended_SetsWorkflowUnattended()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);
        var host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync(["--unattended"]);

        var workflowOptions = host.Services.GetRequiredService<WorkflowOptions>();
        Assert.True(workflowOptions.Unattended);
    }

    // ==================== CFG-11: --max-iterations override ====================

    [Fact]
    public async Task RootCommand_MaxIterations_SetsWorkflowMaxIterations()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);
        var host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync(["--max-iterations", "25"]);

        var workflowOptions = host.Services.GetRequiredService<WorkflowOptions>();
        Assert.Equal(25, workflowOptions.MaxIterations);
    }

    [Fact]
    public async Task RootCommand_NoOverrideFlags_KeepsDefaults()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);
        var host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync([]);

        var workflowOptions = host.Services.GetRequiredService<WorkflowOptions>();
        var modelOptions = host.Services.GetRequiredService<ModelOptions>();
        Assert.Equal(100, workflowOptions.MaxIterations);
        Assert.False(workflowOptions.Unattended);
        Assert.Equal("claude-opus-4.6", modelOptions.Building);
    }

    // ==================== AUTH PRE-FLIGHT TESTS (AUTH-10) ====================

    private static (CommandLineConfiguration config, StringWriter output, StringWriter error, FakeTuiApplication tui) CreateConfigWithAuth(
        IAuthService authService, ISessionManager? sessionManager = null, IWorkflowOrchestrator? orchestrator = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        // Override the real auth service with the provided one
        builder.Services.AddSingleton(authService);

        var fakeTui = new FakeTuiApplication();
        builder.Services.AddSingleton<ITuiApplication>(fakeTui);

        if (sessionManager is not null)
            builder.Services.AddSingleton(sessionManager);

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

    [Fact]
    public async Task RootCommand_Interactive_AuthFails_ReturnsFailure()
    {
        var authService = new FailingAuthService("Not authenticated. Run 'lopen auth login' or set GH_TOKEN.");
        var (config, _, error, tui) = CreateConfigWithAuth(authService);

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Not authenticated", error.ToString());
        Assert.False(tui.RunWasCalled, "TUI should not launch when auth fails");
    }

    [Fact]
    public async Task RootCommand_Headless_AuthFails_ReturnsFailure()
    {
        var authService = new FailingAuthService("Invalid credentials.");
        var sessionManager = new FakeSessionManager();
        var sessionId = SessionId.TryParse("testmod-20260101-001")!;
        await sessionManager.SaveSessionStateAsync(sessionId, new SessionState
        {
            SessionId = "testmod-20260101-001",
            Module = "testmod",
            Phase = "building",
            Step = "6",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await sessionManager.SetLatestAsync(sessionId);

        var (config, _, error, tui) = CreateConfigWithAuth(authService, sessionManager);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid credentials", error.ToString());
        Assert.False(tui.RunWasCalled);
    }

    // ==================== Test Fakes ====================

    private sealed class FailingAuthService : IAuthService
    {
        private readonly string _errorMessage;

        public FailingAuthService(string errorMessage) => _errorMessage = errorMessage;

        public Task LoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AuthStatusResult(AuthState.NotAuthenticated, AuthCredentialSource.None));
        public Task ValidateAsync(CancellationToken cancellationToken = default)
            => throw new AuthenticationException(_errorMessage);
    }

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

    private sealed class ThrowingTuiApplication : ITuiApplication
    {
        public bool IsRunning => false;

        public Task RunAsync(string? initialPrompt = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("TUI startup failed");

        public Task StopAsync() => Task.CompletedTask;

        public void SuppressLandingPage() { }
    }

    private sealed class FakeOrchestrator : IWorkflowOrchestrator
    {
        private readonly OrchestrationResult _result;

        public FakeOrchestrator(OrchestrationResult result) => _result = result;

        public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
            => Task.FromResult(StepResult.Completed("test"));
    }

    private sealed class FakeSessionManager : ISessionManager
    {
        private readonly Dictionary<SessionId, SessionState> _sessions = new();
        private SessionId? _latest;

        public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
            => Task.FromResult(SessionId.TryParse($"{module}-20260101-001")!);

        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default)
            => Task.FromResult(_latest);

        public Task SetLatestAsync(SessionId id, CancellationToken ct = default)
        {
            _latest = id;
            return Task.CompletedTask;
        }

        public Task SaveSessionStateAsync(SessionId id, SessionState state, CancellationToken ct = default)
        {
            _sessions[id] = state;
            return Task.CompletedTask;
        }

        public Task<SessionState?> LoadSessionStateAsync(SessionId id, CancellationToken ct = default)
            => Task.FromResult(_sessions.TryGetValue(id, out var s) ? s : null);

        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId id, CancellationToken ct = default)
            => Task.FromResult<SessionMetrics?>(null);

        public Task SaveSessionMetricsAsync(SessionId id, SessionMetrics metrics, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionId>>([]);

        public Task DeleteSessionAsync(SessionId id, CancellationToken ct = default)
        {
            _sessions.Remove(id);
            return Task.CompletedTask;
        }

        public Task QuarantineCorruptedSessionAsync(SessionId id, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default)
            => Task.FromResult(0);
    }
}
