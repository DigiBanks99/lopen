namespace Lopen.Core.Git;

/// <summary>
/// Provides git operations for workflow automation.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Commits all staged and unstaged changes with the given message.
    /// </summary>
    /// <param name="message">Commit message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the git commit operation.</returns>
    Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and checks out a new branch.
    /// </summary>
    /// <param name="branchName">The branch name to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the git operation.</returns>
    Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the working tree to the specified commit.
    /// </summary>
    /// <param name="commitSha">The commit SHA to reset to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the git reset operation.</returns>
    Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the date of the last commit.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The date of the last commit, or null if no commits exist.</returns>
    Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the diff of the working tree.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The diff output.</returns>
    Task<string> GetDiffAsync(CancellationToken cancellationToken = default);
}
