namespace Lopen.Core.Git;

/// <summary>
/// Workflow-aware git operations: auto-commit on task completion and branch-per-module.
/// </summary>
public interface IGitWorkflowService
{
    /// <summary>
    /// Creates and checks out a module-specific branch (lopen/{moduleName}).
    /// No-op if git is disabled.
    /// </summary>
    Task<GitResult?> EnsureModuleBranchAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-commits all changes after a task completes using conventional commit format.
    /// No-op if git is disabled or auto-commit is off.
    /// </summary>
    Task<GitResult?> CommitTaskCompletionAsync(
        string moduleName,
        string componentName,
        string taskName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Formats a conventional commit message for a task completion.
    /// </summary>
    string FormatCommitMessage(string moduleName, string componentName, string taskName);
}
