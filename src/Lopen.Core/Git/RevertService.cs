using Lopen.Configuration;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Git;

/// <summary>
/// Rolls back the working tree to the last task-completion commit.
/// </summary>
internal sealed class RevertService : IRevertService
{
    private readonly IGitService _gitService;
    private readonly GitOptions _gitOptions;
    private readonly ILogger<RevertService> _logger;

    public RevertService(
        IGitService gitService,
        GitOptions gitOptions,
        ILogger<RevertService> logger)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _gitOptions = gitOptions ?? throw new ArgumentNullException(nameof(gitOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RevertResult> RevertToCommitAsync(
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        if (!_gitOptions.Enabled)
        {
            _logger.LogWarning("Git disabled, cannot revert");
            return new RevertResult(false, null, "Git is disabled in configuration");
        }

        _logger.LogInformation("Reverting to commit {CommitSha}", commitSha);

        try
        {
            var result = await _gitService.ResetToCommitAsync(commitSha, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Successfully reverted to commit {CommitSha}", commitSha);
                return new RevertResult(true, commitSha, $"Reverted to commit {commitSha}");
            }

            _logger.LogWarning("Revert to {CommitSha} failed: {StdErr}", commitSha, result.StdErr);
            return new RevertResult(false, null, $"Git reset failed: {result.StdErr}");
        }
        catch (GitException ex)
        {
            _logger.LogError(ex, "Revert to {CommitSha} threw exception", commitSha);
            return new RevertResult(false, null, $"Revert failed: {ex.Message}");
        }
    }
}
