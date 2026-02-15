using Microsoft.Extensions.Logging;

namespace Lopen.Core.Git;

/// <summary>
/// Git service implementation that executes git commands via the CLI.
/// </summary>
internal sealed class GitCliService : IGitService
{
    private readonly ILogger<GitCliService> _logger;

    public GitCliService(ILogger<GitCliService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Git commit: {Message}", message);
        throw new GitException("Git CLI integration pending.", "git commit");
    }

    /// <inheritdoc />
    public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Git create branch: {BranchName}", branchName);
        throw new GitException("Git CLI integration pending.", "git checkout -b");
    }

    /// <inheritdoc />
    public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Git reset to: {CommitSha}", commitSha);
        throw new GitException("Git CLI integration pending.", "git reset");
    }

    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
    {
        throw new GitException("Git CLI integration pending.", "git log");
    }

    /// <inheritdoc />
    public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
    {
        throw new GitException("Git CLI integration pending.", "git diff");
    }
}
