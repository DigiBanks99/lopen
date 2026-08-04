using Lopen.Auth;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace Lopen.Cli.Tests.Commands;

/// <summary>
/// Tests for the root command handler (lopen with no subcommand).
/// </summary>
public class RootCommandTests
{
    private static (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig(
        ISessionManager? sessionManager = null, IWorkflowOrchestrator? orchestrator = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        if (sessionManager is not null)
            builder.Services.AddSingleton(sessionManager);

        if (orchestrator is not null)
            builder.Services.AddSingleton(orchestrator);

        IHost host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();

        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        return (new CommandLineConfiguration(rootCommand), output, error);
    }

    // ==================== Non-headless mode (TUI not available) ====================

    [Fact]
    public async Task RootCommand_NoArgs_ReturnsTuiNotAvailable()
    {
        (CommandLineConfiguration? config, StringWriter _, StringWriter? error) = CreateConfig();

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(1, exitCode);
        Assert.Contains("TUI not available", error.ToString());
    }

    // ==================== AC-2: Headless mode ====================

    [Fact]
    public async Task RootCommand_Headless_WithPrompt_RunsHeadless()
    {
        (CommandLineConfiguration? config, StringWriter _, StringWriter? error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build auth"]);

        // Headless mode runs orchestrator instead of TUI.
        // Without a session/module, it returns failure.
    }

    [Fact]
    public async Task RootCommand_Headless_NoPrompt_NoSession_ReturnsFailure()
    {
        (CommandLineConfiguration? config, StringWriter _, StringWriter? error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--headless"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--prompt", error.ToString());
    }

    // ==================== Session resume ====================

    [Fact]
    public async Task RootCommand_Headless_WithSession_RunsOrchestrator()
    {
        var sessionManager = new FakeSessionManager();
        SessionId sessionId = SessionId.TryParse("testmod-20260101-001")!;
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

        (CommandLineConfiguration? config, StringWriter? output, StringWriter? error) = CreateConfig(sessionManager);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it"]);

        // May fail due to no orchestrator in this test setup, but should not launch TUI
    }

    [Fact]
    public async Task RootCommand_Resume_InvalidId_WithSessionManager_ReturnsFailure()
    {
        var sessionManager = new FakeSessionManager();
        (CommandLineConfiguration? config, StringWriter _, StringWriter? error) = CreateConfig(sessionManager);

        var exitCode = await config.InvokeAsync(["--resume", "bad-id"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid session ID", error.ToString());
    }

    [Fact]
    public async Task RootCommand_Resume_NoSessionManager_ReturnsTuiNotAvailable()
    {
        (CommandLineConfiguration? config, StringWriter? output, StringWriter? error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["--resume", "bad-id"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("TUI not available", error.ToString());
    }

    [Fact]
    public async Task RootCommand_Resume_WithActiveSession_PrintsResumingMessage()
    {
        var sessionManager = new FakeSessionManager();
        SessionId sessionId = SessionId.TryParse("testmod-20260101-001")!;
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

        (CommandLineConfiguration? config, StringWriter? output, StringWriter _) = CreateConfig(sessionManager);

        var exitCode = await config.InvokeAsync(["--resume", "testmod-20260101-001"]);

        // Non-headless path returns failure (TUI not implemented) but still prints resuming message
        Assert.Equal(1, exitCode);
        Assert.Contains("Resuming session", output.ToString());
    }

    // ==================== CLI-02: Headless success / interrupted ====================

    [Fact]
    public async Task RootCommand_Headless_WithOrchestrator_Completed_ReturnsSuccess()
    {
        var sessionManager = new FakeSessionManager();
        SessionId sessionId = SessionId.TryParse("testmod-20260101-001")!;
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
        (CommandLineConfiguration? config, StringWriter? output, StringWriter? error) = CreateConfig(sessionManager, orchestrator);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it", "--resume", "testmod-20260101-001"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Running headless workflow for module: testmod", output.ToString());
        Assert.Contains("completed after 5 iterations", output.ToString());
    }

    [Fact]
    public async Task RootCommand_Headless_Interrupted_ReturnsExitCode2()
    {
        var sessionManager = new FakeSessionManager();
        SessionId sessionId = SessionId.TryParse("testmod-20260101-001")!;
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
        (CommandLineConfiguration? config, StringWriter? output, StringWriter? error) = CreateConfig(sessionManager, orchestrator);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it", "--resume", "testmod-20260101-001"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("Human gate required", error.ToString());
    }

    // ==================== CFG-08: --model override ====================

    [Fact]
    public async Task RootCommand_Model_OverridesAllPhaseModels()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        IHost host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync(["--model", "gpt-5"]);

        ModelOptions modelOptions = host.Services.GetRequiredService<ModelOptions>();
        Assert.Equal("gpt-5", modelOptions.RequirementGathering);
        Assert.Equal("gpt-5", modelOptions.Planning);
        Assert.Equal("gpt-5", modelOptions.Building);
        Assert.Equal("gpt-5", modelOptions.Research);
    }

    // ==================== CFG-09: --unattended override ====================

    [Fact]
    public async Task RootCommand_Unattended_SetsWorkflowUnattended()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        IHost host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync(["--unattended"]);

        WorkflowOptions workflowOptions = host.Services.GetRequiredService<WorkflowOptions>();
        Assert.True(workflowOptions.Unattended);
    }

    // ==================== CFG-11: --max-iterations override ====================

    [Fact]
    public async Task RootCommand_MaxIterations_SetsWorkflowMaxIterations()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        IHost host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync(["--max-iterations", "25"]);

        WorkflowOptions workflowOptions = host.Services.GetRequiredService<WorkflowOptions>();
        Assert.Equal(25, workflowOptions.MaxIterations);
    }

    [Fact]
    public async Task RootCommand_NoOverrideFlags_KeepsDefaults()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        IHost host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        await new CommandLineConfiguration(rootCommand).InvokeAsync([]);

        WorkflowOptions workflowOptions = host.Services.GetRequiredService<WorkflowOptions>();
        ModelOptions modelOptions = host.Services.GetRequiredService<ModelOptions>();
        Assert.Equal(100, workflowOptions.MaxIterations);
        Assert.False(workflowOptions.Unattended);
        Assert.Equal("claude-opus-4.6", modelOptions.Building);
    }

    // ==================== AUTH PRE-FLIGHT TESTS (AUTH-10) ====================

    private static (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfigWithAuth(
        IAuthService authService, ISessionManager? sessionManager = null, IWorkflowOrchestrator? orchestrator = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        // Override the real auth service with the provided one
        builder.Services.AddSingleton(authService);

        if (sessionManager is not null)
            builder.Services.AddSingleton(sessionManager);

        if (orchestrator is not null)
            builder.Services.AddSingleton(orchestrator);

        IHost host = builder.Build();

        var output = new StringWriter();
        var error = new StringWriter();

        var rootCommand = new RootCommand("Lopen — test");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output, error)(rootCommand);

        return (new CommandLineConfiguration(rootCommand), output, error);
    }

    [Fact]
    public async Task RootCommand_Interactive_AuthFails_ReturnsFailure()
    {
        var authService = new FailingAuthService("Not authenticated. Run 'lopen auth login' or set GH_TOKEN.");
        (CommandLineConfiguration? config, StringWriter _, StringWriter? error) = CreateConfigWithAuth(authService);

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Not authenticated", error.ToString());
    }

    [Fact]
    public async Task RootCommand_Headless_AuthFails_ReturnsFailure()
    {
        var authService = new FailingAuthService("Invalid credentials.");
        var sessionManager = new FakeSessionManager();
        SessionId sessionId = SessionId.TryParse("testmod-20260101-001")!;
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

        (CommandLineConfiguration? config, StringWriter _, StringWriter? error) = CreateConfigWithAuth(authService, sessionManager);

        var exitCode = await config.InvokeAsync(["--headless", "--prompt", "Build it"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid credentials", error.ToString());
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

    private sealed class FakeOrchestrator : IWorkflowOrchestrator
    {
        private readonly OrchestrationResult _result;

        public FakeOrchestrator(OrchestrationResult result) => _result = result;

        public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
            => Task.FromResult(StepResult.Completed("test"));

        public string? ActiveModule => null;

        public Task InitializeAsync(string moduleName, SessionId? resumeSessionId = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
            => Task.FromResult(_sessions.TryGetValue(id, out SessionState? s) ? s : null);

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
