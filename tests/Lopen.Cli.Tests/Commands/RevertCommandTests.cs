using System.CommandLine;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Core.Git;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

public class RevertCommandTests
{
    private readonly FakeSessionManager _fakeSessionManager = new();
    private readonly FakeRevertService _fakeRevert = new();

    private static readonly SessionId Session1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);

    private static readonly SessionState StateWithCommit = new()
    {
        SessionId = Session1.ToString(),
        Phase = "building",
        Step = "execute-task",
        Module = "auth",
        CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
        LastTaskCompletionCommitSha = "abc123def456",
    };

    private static readonly SessionState StateWithoutCommit = new()
    {
        SessionId = Session1.ToString(),
        Phase = "planning",
        Step = "determine-deps",
        Module = "auth",
        CreatedAt = new DateTimeOffset(2026, 2, 14, 10, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, TimeSpan.Zero),
    };

    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionManager>(_fakeSessionManager);
        services.AddSingleton<IRevertService>(_fakeRevert);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        root.Add(RevertCommand.Create(provider, output, error));

        var config = new CommandLineConfiguration(root);
        return (config, output, error);
    }

    [Fact]
    public async Task Revert_Success_OutputsCommitSha()
    {
        _fakeSessionManager.AddSession(Session1, StateWithCommit);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["revert"]);

        Assert.Equal(0, exitCode);
        Assert.True(_fakeRevert.RevertCalled);
        Assert.Equal("abc123def456", _fakeRevert.LastCommitSha);
        Assert.Contains("Reverted to commit", output.ToString());
    }

    [Fact]
    public async Task Revert_Success_ClearsLastCommitShaInSessionState()
    {
        _fakeSessionManager.AddSession(Session1, StateWithCommit);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, _, _) = CreateConfig();

        await config.InvokeAsync(["revert"]);

        var updatedState = await _fakeSessionManager.LoadSessionStateAsync(Session1);
        Assert.NotNull(updatedState);
        Assert.Null(updatedState!.LastTaskCompletionCommitSha);
    }

    [Fact]
    public async Task Revert_NoActiveSession_ReturnsExitCode1()
    {
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["revert"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No active session found", error.ToString());
    }

    [Fact]
    public async Task Revert_NoCommitSha_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, StateWithoutCommit);
        _fakeSessionManager.SetLatestSessionId(Session1);
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["revert"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No task-completion commits found", error.ToString());
    }

    [Fact]
    public async Task Revert_Failure_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, StateWithCommit);
        _fakeSessionManager.SetLatestSessionId(Session1);
        _fakeRevert.Result = new RevertResult(false, null, "Working tree has uncommitted changes.");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["revert"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("uncommitted changes", error.ToString());
    }

    [Fact]
    public async Task Revert_ServiceException_ReturnsExitCode1()
    {
        _fakeSessionManager.AddSession(Session1, StateWithCommit);
        _fakeSessionManager.SetLatestSessionId(Session1);
        _fakeRevert.RevertException = new InvalidOperationException("Git not available");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["revert"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Git not available", error.ToString());
    }

    [Fact]
    public async Task Revert_SessionStateNotFound_ReturnsExitCode1()
    {
        _fakeSessionManager.SetLatestSessionId(Session1);
        // Session1 is set as latest but has no state
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["revert"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Session state not found", error.ToString());
    }
}
