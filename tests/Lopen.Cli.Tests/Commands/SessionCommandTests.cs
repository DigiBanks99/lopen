using System.CommandLine;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

public class SessionCommandTests
{
    private readonly FakeSessionManager _fakeSessionManager = new();
    private readonly LopenOptions _options = new();

    private static readonly SessionId Session1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
    private static readonly SessionId Session2 = SessionId.Generate("core", new DateOnly(2026, 2, 15), 1);

    private static readonly SessionState ActiveState = new()
    {
        SessionId = Session1.ToString(),
        Phase = "building",
        Step = "execute-task",
        Module = "auth",
        Component = "token-renewal",
        CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
        IsComplete = false,
    };

    private static readonly SessionState CompleteState = new()
    {
        SessionId = Session2.ToString(),
        Phase = "building",
        Step = "complete",
        Module = "core",
        CreatedAt = new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 15, 16, 0, 0, TimeSpan.Zero),
        IsComplete = true,
    };

    private static readonly SessionMetrics SampleMetrics = new()
    {
        SessionId = Session1.ToString(),
        CumulativeInputTokens = 5000,
        CumulativeOutputTokens = 3000,
        PremiumRequestCount = 2,
        IterationCount = 10,
        UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
    };

    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(_fakeSessionManager);
        services.AddSingleton(_options);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));

        var config = new CommandLineConfiguration(root);
        return (config, output, error);
    }

    // ==================== LIST TESTS ====================

    [Fact]
    public async Task List_NoSessions_DisplaysMessage()
    {
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "list"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("No sessions found", output.ToString());
    }

    [Fact]
    public async Task List_WithSessions_DisplaysAll()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.AddSession(Session2, CompleteState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "list"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("auth-20260214-1", text);
        Assert.Contains("core-20260215-1", text);
        Assert.Contains("[active]", text);
        Assert.Contains("[complete]", text);
        Assert.Contains("*", text); // latest marker
    }

    [Fact]
    public async Task List_Error_ReturnsExitCode1()
    {
        _fakeSessionManager.ListException = new InvalidOperationException("Storage error");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "list"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Storage error", error.ToString());
    }

    // ==================== SHOW TESTS ====================

    [Fact]
    public async Task Show_LatestSession_DisplaysDetails()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState, SampleMetrics);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("auth-20260214-1", text);
        Assert.Contains("building", text);
        Assert.Contains("execute-task", text);
        Assert.Contains("Active", text);
    }

    [Fact]
    public async Task Show_SpecificSession_DisplaysDetails()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show", "auth-20260214-1"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("auth-20260214-1", output.ToString());
    }

    [Fact]
    public async Task Show_NoLatestSession_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No active session found", error.ToString());
    }

    [Fact]
    public async Task Show_SessionNotFound_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show", "auth-20260214-1"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Session not found", error.ToString());
    }

    [Fact]
    public async Task Show_InvalidSessionId_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show", "invalid-id"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid session ID format", error.ToString());
    }

    [Fact]
    public async Task Show_JsonFormat_ReturnsJson()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState, SampleMetrics);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show", "auth-20260214-1", "--format", "json"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("\"session_id\"", text);
        Assert.Contains("\"phase\"", text);
        Assert.Contains("\"metrics\"", text);
    }

    [Fact]
    public async Task Show_YamlFormat_ReturnsYaml()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "show", "auth-20260214-1", "--format", "yaml"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("session_id: auth-20260214-1", text);
        Assert.Contains("phase: building", text);
    }

    // ==================== RESUME TESTS ====================

    [Fact]
    public async Task Resume_SpecificSession_SetsLatest()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "resume", "auth-20260214-1"]);

        Assert.Equal(0, exitCode);
        Assert.True(_fakeSessionManager.SetLatestCalled);
        Assert.Equal(Session1, _fakeSessionManager.LastSetLatestSessionId);
        Assert.Contains("Resumed session", output.ToString());
    }

    [Fact]
    public async Task Resume_LatestSession_SetsLatest()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "resume"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Resumed session auth-20260214-1", output.ToString());
    }

    [Fact]
    public async Task Resume_NoLatestSession_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "resume"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No active session found", error.ToString());
    }

    [Fact]
    public async Task Resume_CompletedSession_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session2, CompleteState);
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "resume", "core-20260215-1"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("already complete", error.ToString());
    }

    [Fact]
    public async Task Resume_SessionNotFound_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "resume", "auth-20260214-1"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Session not found", error.ToString());
    }

    [Fact]
    public async Task Resume_InvalidSessionId_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "resume", "bad-format"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid session ID format", error.ToString());
    }

    // ==================== DELETE TESTS ====================

    [Fact]
    public async Task Delete_ExistingSession_DeletesIt()
    {
        _fakeSessionManager.AddSession(Session1, ActiveState);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "delete", "auth-20260214-1"]);

        Assert.Equal(0, exitCode);
        Assert.True(_fakeSessionManager.DeleteCalled);
        Assert.Equal(Session1, _fakeSessionManager.LastDeletedSessionId);
        Assert.Contains("Deleted session", output.ToString());
    }

    [Fact]
    public async Task Delete_StorageException_ReturnsExitCode1()
    {
        _fakeSessionManager.DeleteException = new StorageException("Session not found: auth-20260214-99");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "delete", "auth-20260214-99"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Session not found", error.ToString());
    }

    [Fact]
    public async Task Delete_InvalidSessionId_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "delete", "bad-format"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid session ID format", error.ToString());
    }

    // ==================== PRUNE TESTS ====================

    [Fact]
    public async Task Prune_PrunesSessions()
    {
        _fakeSessionManager.PruneResult = 3;
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "prune"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Pruned 3 session(s)", output.ToString());
        Assert.Contains("retention: 10", output.ToString());
    }

    [Fact]
    public async Task Prune_Error_ReturnsExitCode1()
    {
        _fakeSessionManager.PruneException = new InvalidOperationException("Prune failed");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["session", "prune"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Prune failed", error.ToString());
    }

    // ==================== FORMAT TESTS ====================

    [Fact]
    public void FormatSession_Markdown_ContainsHeaders()
    {
        var result = SessionCommand.FormatSession(ActiveState, SampleMetrics, "md");

        Assert.Contains("# Session: auth-20260214-1", result);
        Assert.Contains("**Phase**: building", result);
        Assert.Contains("**Component**: token-renewal", result);
        Assert.Contains("## Metrics", result);
        Assert.Contains("**Iterations**: 10", result);
    }

    [Fact]
    public void FormatSession_Json_ContainsKeys()
    {
        var result = SessionCommand.FormatSession(ActiveState, SampleMetrics, "json");

        Assert.Contains("\"session_id\"", result);
        Assert.Contains("\"auth-20260214-1\"", result);
        Assert.Contains("\"metrics\"", result);
        Assert.Contains("\"iteration_count\"", result);
    }

    [Fact]
    public void FormatSession_Yaml_ContainsKeys()
    {
        var result = SessionCommand.FormatSession(ActiveState, null, "yaml");

        Assert.Contains("session_id: auth-20260214-1", result);
        Assert.Contains("phase: building", result);
        Assert.Contains("component: token-renewal", result);
        Assert.DoesNotContain("metrics:", result);
    }

    [Fact]
    public void FormatSession_Markdown_NoMetrics_OmitsSection()
    {
        var result = SessionCommand.FormatSession(ActiveState, null, "md");

        Assert.Contains("# Session:", result);
        Assert.DoesNotContain("## Metrics", result);
    }

    [Fact]
    public void FormatSession_Markdown_CompleteSession_ShowsComplete()
    {
        var result = SessionCommand.FormatSession(CompleteState, null, "md");

        Assert.Contains("**Status**: Complete", result);
    }

    // ==================== NO SERVICE REGISTERED TESTS ====================

    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfigWithoutServices()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var output = new StringWriter();
        var error = new StringWriter();
        var root = new RootCommand("test");
        root.Add(SessionCommand.Create(provider, output, error));
        var config = new CommandLineConfiguration(root);
        return (config, output, error);
    }

    [Fact]
    public async Task List_NoServiceRegistered_ReturnsFailureWithMessage()
    {
        var (config, _, error) = CreateConfigWithoutServices();

        var exitCode = await config.InvokeAsync(["session", "list"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No project found", error.ToString());
    }

    [Fact]
    public async Task Show_NoServiceRegistered_ReturnsFailureWithMessage()
    {
        var (config, _, error) = CreateConfigWithoutServices();

        var exitCode = await config.InvokeAsync(["session", "show"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No project found", error.ToString());
    }

    [Fact]
    public async Task Resume_NoServiceRegistered_ReturnsFailureWithMessage()
    {
        var (config, _, error) = CreateConfigWithoutServices();

        var exitCode = await config.InvokeAsync(["session", "resume"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No project found", error.ToString());
    }

    [Fact]
    public async Task Delete_NoServiceRegistered_ReturnsFailureWithMessage()
    {
        var (config, _, error) = CreateConfigWithoutServices();

        var exitCode = await config.InvokeAsync(["session", "delete", "test-id"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No project found", error.ToString());
    }

    [Fact]
    public async Task Prune_NoServiceRegistered_ReturnsFailureWithMessage()
    {
        var (config, _, error) = CreateConfigWithoutServices();

        var exitCode = await config.InvokeAsync(["session", "prune"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No project found", error.ToString());
    }
}
