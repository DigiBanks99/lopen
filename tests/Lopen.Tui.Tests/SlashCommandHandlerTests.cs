using Lopen.Auth;
using Lopen.Configuration;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for domain handlers registered by SlashCommandExecutor when an IServiceProvider is provided.
/// Covers JOB-101 (TUI-38) acceptance criteria.
/// </summary>
public class SlashCommandHandlerTests
{
    private static readonly DateOnly TestDate = new(2025, 1, 15);
    private static readonly SessionId TestSessionId = SessionId.Generate("testmod", TestDate, 1);
    private static readonly DateTimeOffset TestTimestamp = new(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static SessionState CreateTestState(bool isComplete = false, string? commitSha = null) => new()
    {
        SessionId = TestSessionId.ToString(),
        Phase = "building",
        Step = "IterateThroughTasks",
        Module = "testmod",
        CreatedAt = TestTimestamp,
        UpdatedAt = TestTimestamp,
        IsComplete = isComplete,
        LastTaskCompletionCommitSha = commitSha
    };

    private static SlashCommandExecutor CreateExecutor(Action<ServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        var sp = services.BuildServiceProvider();

        return new SlashCommandExecutor(
            SlashCommandRegistry.CreateDefault(),
            NullLogger<SlashCommandExecutor>.Instance,
            sp);
    }

    private static SlashCommandExecutor CreateExecutorWithoutProvider()
    {
        return new SlashCommandExecutor(
            SlashCommandRegistry.CreateDefault(),
            NullLogger<SlashCommandExecutor>.Instance);
    }

    // ==================== Phase Commands (/spec, /plan, /build) ====================

    [Theory]
    [InlineData("/spec")]
    [InlineData("/plan")]
    [InlineData("/build")]
    public async Task PhaseCommand_NoOrchestrator_ReturnsError(string command)
    {
        var executor = CreateExecutor();
        var result = await executor.ExecuteAsync(command);
        Assert.False(result.IsSuccess);
        Assert.Contains("orchestrator not available", result.ErrorMessage);
    }

    [Theory]
    [InlineData("/spec")]
    [InlineData("/plan")]
    [InlineData("/build")]
    public async Task PhaseCommand_NoModuleNoSession_ReturnsError(string command)
    {
        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<IWorkflowOrchestrator>(new StubOrchestrator());
            s.AddSingleton<ISessionManager>(new StubSessionManager());
        });

        var result = await executor.ExecuteAsync(command);
        Assert.False(result.IsSuccess);
        Assert.Contains("No module specified", result.ErrorMessage);
    }

    [Theory]
    [InlineData("/spec")]
    [InlineData("/plan")]
    [InlineData("/build")]
    public async Task PhaseCommand_ResolvesModuleFromActiveSession(string command)
    {
        var orchestrator = new StubOrchestrator();
        var sessionManager = new StubSessionManager
        {
            LatestSessionId = TestSessionId,
            SessionStateToReturn = CreateTestState()
        };

        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<IWorkflowOrchestrator>(orchestrator);
            s.AddSingleton<ISessionManager>(sessionManager);
        });

        var result = await executor.ExecuteAsync(command);
        Assert.True(result.IsSuccess);
        Assert.Equal("testmod", orchestrator.LastModuleName);
    }

    [Theory]
    [InlineData("/spec")]
    [InlineData("/plan")]
    [InlineData("/build")]
    public async Task PhaseCommand_UsesModuleFromArgs(string command)
    {
        var orchestrator = new StubOrchestrator();

        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<IWorkflowOrchestrator>(orchestrator);
        });

        var result = await executor.ExecuteAsync($"{command} mymodule");
        Assert.True(result.IsSuccess);
        Assert.Equal("mymodule", orchestrator.LastModuleName);
    }

    [Theory]
    [InlineData("/spec")]
    [InlineData("/plan")]
    [InlineData("/build")]
    public async Task PhaseCommand_ReturnsSuccessWithSummary(string command)
    {
        var orchestrator = new StubOrchestrator
        {
            ResultToReturn = OrchestrationResult.Completed(3, WorkflowStep.Repeat, "All tasks done")
        };

        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<IWorkflowOrchestrator>(orchestrator);
        });

        var result = await executor.ExecuteAsync($"{command} testmod");
        Assert.True(result.IsSuccess);
        Assert.Equal("All tasks done", result.OutputMessage);
    }

    // ==================== Session Command ====================

    [Fact]
    public async Task SessionList_ReturnsSessions()
    {
        var sessionManager = new StubSessionManager
        {
            Sessions = [TestSessionId],
            LatestSessionId = TestSessionId
        };

        var executor = CreateExecutor(s => s.AddSingleton<ISessionManager>(sessionManager));
        var result = await executor.ExecuteAsync("/session list");
        Assert.True(result.IsSuccess);
        Assert.Contains("Sessions (1)", result.OutputMessage);
        Assert.Contains(TestSessionId.ToString(), result.OutputMessage);
    }

    [Fact]
    public async Task SessionList_Empty_ReturnsNoSessionsFound()
    {
        var executor = CreateExecutor(s =>
            s.AddSingleton<ISessionManager>(new StubSessionManager()));

        var result = await executor.ExecuteAsync("/session list");
        Assert.True(result.IsSuccess);
        Assert.Contains("No sessions found", result.OutputMessage);
    }

    [Fact]
    public async Task SessionShow_ReturnsSessionDetails()
    {
        var sessionManager = new StubSessionManager
        {
            LatestSessionId = TestSessionId,
            SessionStateToReturn = CreateTestState(),
            SessionMetricsToReturn = new SessionMetrics
            {
                SessionId = TestSessionId.ToString(),
                IterationCount = 5,
                CumulativeInputTokens = 1000,
                CumulativeOutputTokens = 500,
                UpdatedAt = TestTimestamp
            }
        };

        var executor = CreateExecutor(s => s.AddSingleton<ISessionManager>(sessionManager));
        var result = await executor.ExecuteAsync("/session show");
        Assert.True(result.IsSuccess);
        Assert.Contains("testmod", result.OutputMessage);
        Assert.Contains("building", result.OutputMessage);
        Assert.Contains("Iterations: 5", result.OutputMessage);
        Assert.Contains("Tokens: 1500", result.OutputMessage);
    }

    [Fact]
    public async Task SessionShow_InvalidId_ReturnsError()
    {
        var executor = CreateExecutor(s =>
            s.AddSingleton<ISessionManager>(new StubSessionManager()));

        var result = await executor.ExecuteAsync("/session show bad-id");
        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid session ID", result.ErrorMessage);
    }

    [Fact]
    public async Task SessionResume_ResumesSession()
    {
        var sessionManager = new StubSessionManager
        {
            LatestSessionId = TestSessionId,
            SessionStateToReturn = CreateTestState(isComplete: false)
        };

        var executor = CreateExecutor(s => s.AddSingleton<ISessionManager>(sessionManager));
        var result = await executor.ExecuteAsync($"/session resume {TestSessionId}");
        Assert.True(result.IsSuccess);
        Assert.Contains("Resumed session", result.OutputMessage);
        Assert.Equal(TestSessionId, sessionManager.LastSetLatestId);
    }

    [Fact]
    public async Task SessionResume_CompletedSession_ReturnsError()
    {
        var sessionManager = new StubSessionManager
        {
            SessionStateToReturn = CreateTestState(isComplete: true)
        };

        var executor = CreateExecutor(s => s.AddSingleton<ISessionManager>(sessionManager));
        var result = await executor.ExecuteAsync($"/session resume {TestSessionId}");
        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot resume completed session", result.ErrorMessage);
    }

    [Fact]
    public async Task SessionDelete_DeletesSession()
    {
        var sessionManager = new StubSessionManager();

        var executor = CreateExecutor(s => s.AddSingleton<ISessionManager>(sessionManager));
        var result = await executor.ExecuteAsync($"/session delete {TestSessionId}");
        Assert.True(result.IsSuccess);
        Assert.Contains("Deleted session", result.OutputMessage);
        Assert.Equal(TestSessionId, sessionManager.LastDeletedId);
    }

    [Fact]
    public async Task SessionDelete_NoId_ReturnsError()
    {
        var executor = CreateExecutor(s =>
            s.AddSingleton<ISessionManager>(new StubSessionManager()));

        var result = await executor.ExecuteAsync("/session delete");
        Assert.False(result.IsSuccess);
        Assert.Contains("Usage:", result.ErrorMessage);
    }

    [Fact]
    public async Task SessionPrune_PrunesSessions()
    {
        var sessionManager = new StubSessionManager { PruneCount = 3 };

        var executor = CreateExecutor(s => s.AddSingleton<ISessionManager>(sessionManager));
        var result = await executor.ExecuteAsync("/session prune");
        Assert.True(result.IsSuccess);
        Assert.Contains("Pruned 3 session(s)", result.OutputMessage);
    }

    [Fact]
    public async Task SessionUnknown_ReturnsError()
    {
        var executor = CreateExecutor(s =>
            s.AddSingleton<ISessionManager>(new StubSessionManager()));

        var result = await executor.ExecuteAsync("/session badcmd");
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown subcommand", result.ErrorMessage);
        Assert.Contains("list", result.ErrorMessage);
        Assert.Contains("show", result.ErrorMessage);
        Assert.Contains("resume", result.ErrorMessage);
        Assert.Contains("delete", result.ErrorMessage);
        Assert.Contains("prune", result.ErrorMessage);
    }

    // ==================== Config Command ====================

    [Fact]
    public async Task Config_ShowsFormattedConfig()
    {
        var executor = CreateExecutor(s =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["MyKey"] = "MyValue" })
                .Build();
            s.AddSingleton<IConfigurationRoot>(config);
        });

        var result = await executor.ExecuteAsync("/config");
        Assert.True(result.IsSuccess);
        Assert.Contains("MyKey", result.OutputMessage);
        Assert.Contains("MyValue", result.OutputMessage);
    }

    [Fact]
    public async Task Config_Json_ShowsJsonConfig()
    {
        var executor = CreateExecutor(s =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["TestKey"] = "TestVal" })
                .Build();
            s.AddSingleton<IConfigurationRoot>(config);
        });

        var result = await executor.ExecuteAsync("/config --json");
        Assert.True(result.IsSuccess);
        Assert.Contains("\"key\"", result.OutputMessage);
        Assert.Contains("TestKey", result.OutputMessage);
    }

    [Fact]
    public async Task Config_NoConfigAvailable_ReturnsError()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync("/config");
        Assert.False(result.IsSuccess);
        Assert.Contains("Configuration not available", result.ErrorMessage);
    }

    // ==================== Revert Command ====================

    [Fact]
    public async Task Revert_Success()
    {
        var sessionManager = new StubSessionManager
        {
            LatestSessionId = TestSessionId,
            SessionStateToReturn = CreateTestState(commitSha: "abc123")
        };
        var revertService = new StubRevertService
        {
            ResultToReturn = new RevertResult(true, "abc123", "Reverted to abc123")
        };

        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<ISessionManager>(sessionManager);
            s.AddSingleton<IRevertService>(revertService);
        });

        var result = await executor.ExecuteAsync("/revert");
        Assert.True(result.IsSuccess);
        Assert.Contains("Reverted to abc123", result.OutputMessage);
        Assert.NotNull(sessionManager.LastSavedState);
        Assert.Null(sessionManager.LastSavedState!.LastTaskCompletionCommitSha);
    }

    [Fact]
    public async Task Revert_NoSession_ReturnsError()
    {
        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<IRevertService>(new StubRevertService());
            s.AddSingleton<ISessionManager>(new StubSessionManager());
        });

        var result = await executor.ExecuteAsync("/revert");
        Assert.False(result.IsSuccess);
        Assert.Contains("No active session", result.ErrorMessage);
    }

    [Fact]
    public async Task Revert_NoCheckpoint_ReturnsError()
    {
        var sessionManager = new StubSessionManager
        {
            LatestSessionId = TestSessionId,
            SessionStateToReturn = CreateTestState(commitSha: null)
        };

        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<IRevertService>(new StubRevertService());
            s.AddSingleton<ISessionManager>(sessionManager);
        });

        var result = await executor.ExecuteAsync("/revert");
        Assert.False(result.IsSuccess);
        Assert.Contains("No checkpoint found", result.ErrorMessage);
    }

    [Fact]
    public async Task Revert_RevertFails_ReturnsError()
    {
        var sessionManager = new StubSessionManager
        {
            LatestSessionId = TestSessionId,
            SessionStateToReturn = CreateTestState(commitSha: "abc123")
        };
        var revertService = new StubRevertService
        {
            ResultToReturn = new RevertResult(false, null, "Uncommitted changes block revert")
        };

        var executor = CreateExecutor(s =>
        {
            s.AddSingleton<ISessionManager>(sessionManager);
            s.AddSingleton<IRevertService>(revertService);
        });

        var result = await executor.ExecuteAsync("/revert");
        Assert.False(result.IsSuccess);
        Assert.Contains("Uncommitted changes block revert", result.ErrorMessage);
    }

    // ==================== Auth Command ====================

    [Fact]
    public async Task AuthStatus_ShowsStatus()
    {
        var authService = new StubAuthService
        {
            StatusToReturn = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GhToken, "testuser")
        };

        var executor = CreateExecutor(s => s.AddSingleton<IAuthService>(authService));
        var result = await executor.ExecuteAsync("/auth status");
        Assert.True(result.IsSuccess);
        Assert.Contains("Authenticated", result.OutputMessage);
        Assert.Contains("GhToken", result.OutputMessage);
        Assert.Contains("testuser", result.OutputMessage);
    }

    [Fact]
    public async Task AuthLogin_PerformsLogin()
    {
        var authService = new StubAuthService();

        var executor = CreateExecutor(s => s.AddSingleton<IAuthService>(authService));
        var result = await executor.ExecuteAsync("/auth login");
        Assert.True(result.IsSuccess);
        Assert.Contains("Login successful", result.OutputMessage);
        Assert.True(authService.LoginCalled);
    }

    [Fact]
    public async Task AuthLogout_PerformsLogout()
    {
        var authService = new StubAuthService();

        var executor = CreateExecutor(s => s.AddSingleton<IAuthService>(authService));
        var result = await executor.ExecuteAsync("/auth logout");
        Assert.True(result.IsSuccess);
        Assert.Contains("Logged out", result.OutputMessage);
        Assert.True(authService.LogoutCalled);
    }

    [Fact]
    public async Task AuthUnknown_ReturnsError()
    {
        var executor = CreateExecutor(s =>
            s.AddSingleton<IAuthService>(new StubAuthService()));

        var result = await executor.ExecuteAsync("/auth badcmd");
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown subcommand", result.ErrorMessage);
        Assert.Contains("login", result.ErrorMessage);
        Assert.Contains("status", result.ErrorMessage);
        Assert.Contains("logout", result.ErrorMessage);
    }

    [Fact]
    public async Task Auth_NoService_ReturnsError()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync("/auth status");
        Assert.False(result.IsSuccess);
        Assert.Contains("Authentication service not available", result.ErrorMessage);
    }

    // ==================== Stubs ====================

    private sealed class StubOrchestrator : IWorkflowOrchestrator
    {
        public string? LastModuleName { get; private set; }

        public OrchestrationResult ResultToReturn { get; set; } =
            OrchestrationResult.Completed(1, WorkflowStep.Repeat, "Complete");

        public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null,
            CancellationToken cancellationToken = default)
        {
            LastModuleName = moduleName;
            return Task.FromResult(ResultToReturn);
        }

        public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null,
            CancellationToken cancellationToken = default)
        {
            LastModuleName = moduleName;
            return Task.FromResult(new StepResult { Success = true, Summary = "Step complete" });
        }
    }

    private sealed class StubSessionManager : ISessionManager
    {
        public SessionId? LatestSessionId { get; set; }
        public SessionState? SessionStateToReturn { get; set; }
        public SessionMetrics? SessionMetricsToReturn { get; set; }
        public IReadOnlyList<SessionId> Sessions { get; set; } = [];
        public int PruneCount { get; set; }
        public SessionId? LastSetLatestId { get; private set; }
        public SessionId? LastDeletedId { get; private set; }
        public SessionState? LastSavedState { get; private set; }

        public Task<SessionId> CreateSessionAsync(string module, CancellationToken cancellationToken = default)
            => Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));

        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LatestSessionId);

        public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(SessionStateToReturn);

        public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken cancellationToken = default)
        {
            LastSavedState = state;
            return Task.CompletedTask;
        }

        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(SessionMetricsToReturn);

        public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Sessions);

        public Task SetLatestAsync(SessionId sessionId, CancellationToken cancellationToken = default)
        {
            LastSetLatestId = sessionId;
            return Task.CompletedTask;
        }

        public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken cancellationToken = default)
            => Task.FromResult(PruneCount);

        public Task DeleteSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
        {
            LastDeletedId = sessionId;
            return Task.CompletedTask;
        }
    }

    private sealed class StubAuthService : IAuthService
    {
        public bool LoginCalled { get; private set; }
        public bool LogoutCalled { get; private set; }

        public AuthStatusResult StatusToReturn { get; set; } =
            new(AuthState.NotAuthenticated, AuthCredentialSource.None);

        public Task LoginAsync(CancellationToken cancellationToken = default)
        {
            LoginCalled = true;
            return Task.CompletedTask;
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCalled = true;
            return Task.CompletedTask;
        }

        public Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(StatusToReturn);

        public Task ValidateAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubRevertService : IRevertService
    {
        public RevertResult ResultToReturn { get; set; } = new(true, "abc123", "Reverted successfully");

        public Task<RevertResult> RevertToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultToReturn);
    }
}
