using Lopen.Configuration;
using Lopen.Core.Git;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Git;

public sealed class RevertServiceTests
{
    private static RevertService CreateService(
        IGitService? gitService = null,
        GitOptions? gitOptions = null)
    {
        return new RevertService(
            gitService ?? new FakeGitService(),
            gitOptions ?? new GitOptions(),
            NullLogger<RevertService>.Instance);
    }

    // --- Constructor null guards ---

    [Fact]
    public void Constructor_NullGitService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RevertService(null!, new GitOptions(), NullLogger<RevertService>.Instance));
    }

    [Fact]
    public void Constructor_NullGitOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RevertService(new FakeGitService(), null!, NullLogger<RevertService>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RevertService(new FakeGitService(), new GitOptions(), null!));
    }

    // --- RevertToCommitAsync ---

    [Fact]
    public async Task RevertToCommit_ValidSha_ResetsSuccessfully()
    {
        var git = new FakeGitService();
        var service = CreateService(git);

        var result = await service.RevertToCommitAsync("abc123");

        Assert.True(result.Success);
        Assert.Equal("abc123", result.RevertedToCommitSha);
        Assert.Contains("abc123", result.Message);
        Assert.Equal("abc123", git.LastResetSha);
    }

    [Fact]
    public async Task RevertToCommit_GitDisabled_ReturnsFalse()
    {
        var git = new FakeGitService();
        var options = new GitOptions { Enabled = false };
        var service = CreateService(git, options);

        var result = await service.RevertToCommitAsync("abc123");

        Assert.False(result.Success);
        Assert.Null(result.RevertedToCommitSha);
        Assert.Contains("disabled", result.Message);
        Assert.Null(git.LastResetSha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevertToCommit_InvalidSha_Throws(string? sha)
    {
        var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.RevertToCommitAsync(sha!));
    }

    [Fact]
    public async Task RevertToCommit_GitResetFails_ReturnsFalse()
    {
        var git = new FailingResetGitService();
        var service = CreateService(git);

        var result = await service.RevertToCommitAsync("abc123");

        Assert.False(result.Success);
        Assert.Null(result.RevertedToCommitSha);
        Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevertToCommit_GitThrowsException_ReturnsFalse()
    {
        var git = new ThrowingGitService();
        var service = CreateService(git);

        var result = await service.RevertToCommitAsync("abc123");

        Assert.False(result.Success);
        Assert.Null(result.RevertedToCommitSha);
        Assert.Contains("Revert failed", result.Message);
    }

    [Fact]
    public async Task RevertToCommit_ImplementsIRevertService()
    {
        IRevertService service = CreateService();

        var result = await service.RevertToCommitAsync("abc123");

        Assert.True(result.Success);
    }

    // --- Test doubles ---

    private sealed class FakeGitService : IGitService
    {
        public string? LastResetSha { get; private set; }

        public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "committed", ""));

        public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "branch created", ""));

        public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
        {
            LastResetSha = commitSha;
            return Task.FromResult(new GitResult(0, $"HEAD is now at {commitSha}", ""));
        }

        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("");

        public Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("abc123");

        public Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("main");
    }

    private sealed class FailingResetGitService : IGitService
    {
        public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "", ""));

        public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "", ""));

        public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(1, "", "fatal: Could not parse object 'abc123'"));

        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("");

        public Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class ThrowingGitService : IGitService
    {
        public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
            => throw new GitException("failed", "git commit", 1, "error");

        public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
            => throw new GitException("failed", "git checkout", 1, "error");

        public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
            => throw new GitException("reset explosion", "git reset", 1, "fatal error");

        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
            => throw new GitException("failed", "git log", 1, "error");

        public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
            => throw new GitException("failed", "git diff", 1, "error");

        public Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
            => throw new GitException("failed", "git rev-parse", 1, "error");

        public Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
            => throw new GitException("failed", "git branch", 1, "error");
    }
}
