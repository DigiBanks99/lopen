namespace Lopen.Core.Git;

/// <summary>
/// Rolls back the working tree to the last task-completion commit.
/// </summary>
public interface IRevertService
{
    /// <summary>
    /// Reverts to the specified commit SHA (typically the last task-completion commit).
    /// </summary>
    /// <param name="commitSha">The commit SHA to revert to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the revert operation.</returns>
    Task<RevertResult> RevertToCommitAsync(string commitSha, CancellationToken cancellationToken = default);
}
