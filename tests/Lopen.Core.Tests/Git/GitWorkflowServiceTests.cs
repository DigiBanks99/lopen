using Lopen.Configuration;
using Lopen.Core.Git;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Git;

public sealed class GitWorkflowServiceTests
{
    private static GitWorkflowService CreateService(
        IGitService? gitService = null,
        GitOptions? gitOptions = null)
    {
        return new GitWorkflowService(
            gitService ?? new FakeGitService(),
            gitOptions ?? new GitOptions(),
            NullLogger<GitWorkflowService>.Instance);
    }

    // --- Constructor null guards ---

    [Fact]
    public void Constructor_NullGitService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GitWorkflowService(null!, new GitOptions(), NullLogger<GitWorkflowService>.Instance));
    }

    [Fact]
    public void Constructor_NullGitOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GitWorkflowService(new FakeGitService(), null!, NullLogger<GitWorkflowService>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GitWorkflowService(new FakeGitService(), new GitOptions(), null!));
    }

    // --- EnsureModuleBranchAsync ---

    [Fact]
    public async Task EnsureModuleBranch_CreatesBranchWithPrefix()
    {
        var git = new FakeGitService();
        var service = CreateService(git);

        var result = await service.EnsureModuleBranchAsync("auth");

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("lopen/auth", git.LastBranchCreated);
    }

    [Fact]
    public async Task EnsureModuleBranch_GitDisabled_ReturnsNull()
    {
        var git = new FakeGitService();
        var options = new GitOptions { Enabled = false };
        var service = CreateService(git, options);

        var result = await service.EnsureModuleBranchAsync("auth");

        Assert.Null(result);
        Assert.Null(git.LastBranchCreated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnsureModuleBranch_InvalidModuleName_Throws(string? moduleName)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.EnsureModuleBranchAsync(moduleName!));
    }

    [Fact]
    public async Task EnsureModuleBranch_GitThrows_ReturnsErrorResult()
    {
        var git = new ThrowingGitService();
        var service = CreateService(git);

        var result = await service.EnsureModuleBranchAsync("auth");

        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    // --- CommitTaskCompletionAsync ---

    [Fact]
    public async Task CommitTaskCompletion_AutoCommitEnabled_Commits()
    {
        var git = new FakeGitService();
        var service = CreateService(git);

        var result = await service.CommitTaskCompletionAsync("auth", "login", "implement-jwt");

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(git.LastCommitMessage);
        Assert.Contains("auth", git.LastCommitMessage);
        Assert.Contains("implement-jwt", git.LastCommitMessage);
    }

    [Fact]
    public async Task CommitTaskCompletion_GitDisabled_ReturnsNull()
    {
        var git = new FakeGitService();
        var options = new GitOptions { Enabled = false };
        var service = CreateService(git, options);

        var result = await service.CommitTaskCompletionAsync("auth", "login", "implement-jwt");

        Assert.Null(result);
        Assert.Null(git.LastCommitMessage);
    }

    [Fact]
    public async Task CommitTaskCompletion_AutoCommitDisabled_ReturnsNull()
    {
        var git = new FakeGitService();
        var options = new GitOptions { AutoCommit = false };
        var service = CreateService(git, options);

        var result = await service.CommitTaskCompletionAsync("auth", "login", "implement-jwt");

        Assert.Null(result);
        Assert.Null(git.LastCommitMessage);
    }

    [Theory]
    [InlineData(null, "component", "task")]
    [InlineData("module", null, "task")]
    [InlineData("module", "component", null)]
    [InlineData("", "component", "task")]
    [InlineData("module", "", "task")]
    [InlineData("module", "component", "")]
    public async Task CommitTaskCompletion_InvalidArgs_Throws(
        string? module, string? component, string? task)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.CommitTaskCompletionAsync(module!, component!, task!));
    }

    [Fact]
    public async Task CommitTaskCompletion_GitThrows_ReturnsErrorResult()
    {
        var git = new ThrowingGitService();
        var service = CreateService(git);

        var result = await service.CommitTaskCompletionAsync("auth", "login", "task");

        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    // --- FormatCommitMessage ---

    [Fact]
    public void FormatCommitMessage_Conventional_ReturnsConventionalFormat()
    {
        var service = CreateService();

        var message = service.FormatCommitMessage("auth", "login", "implement-jwt");

        Assert.Equal("feat(auth): complete implement-jwt in login", message);
    }

    [Fact]
    public void FormatCommitMessage_NonConventional_ReturnsBracketFormat()
    {
        var options = new GitOptions { Convention = "simple" };
        var service = CreateService(gitOptions: options);

        var message = service.FormatCommitMessage("auth", "login", "implement-jwt");

        Assert.Equal("[auth] Complete implement-jwt in login", message);
    }

    [Theory]
    [InlineData(null, "component", "task")]
    [InlineData("module", null, "task")]
    [InlineData("module", "component", null)]
    public void FormatCommitMessage_NullArgs_Throws(
        string? module, string? component, string? task)
    {
        var service = CreateService();

        Assert.ThrowsAny<ArgumentException>(
            () => service.FormatCommitMessage(module!, component!, task!));
    }

    // --- Branch prefix ---

    [Fact]
    public void BranchPrefix_IsLopen()
    {
        Assert.Equal("lopen/", GitWorkflowService.BranchPrefix);
    }

    // --- Test doubles ---

    private sealed class FakeGitService : IGitService
    {
        public string? LastCommitMessage { get; private set; }
        public string? LastBranchCreated { get; private set; }
        public string? LastResetSha { get; private set; }

        public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
        {
            LastCommitMessage = message;
            return Task.FromResult(new GitResult(0, "committed", ""));
        }

        public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
        {
            LastBranchCreated = branchName;
            return Task.FromResult(new GitResult(0, "branch created", ""));
        }

        public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
        {
            LastResetSha = commitSha;
            return Task.FromResult(new GitResult(0, "reset done", ""));
        }

        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("diff output");

        public Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("abc123def456");

        public Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("main");
    }

    private sealed class ThrowingGitService : IGitService
    {
        public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
            => throw new GitException("commit failed", "git commit", 1, "error");

        public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
            => throw new GitException("branch failed", "git checkout -b", 1, "error");

        public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
            => throw new GitException("reset failed", "git reset", 1, "error");

        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
            => throw new GitException("log failed", "git log", 1, "error");

        public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
            => throw new GitException("diff failed", "git diff", 1, "error");

        public Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
            => throw new GitException("rev-parse failed", "git rev-parse", 1, "error");

        public Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
            => throw new GitException("branch failed", "git branch", 1, "error");
    }
}
