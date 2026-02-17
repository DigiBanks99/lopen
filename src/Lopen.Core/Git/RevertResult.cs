namespace Lopen.Core.Git;

/// <summary>
/// Result of a revert operation.
/// </summary>
public sealed record RevertResult(
    bool Success,
    string? RevertedToCommitSha,
    string Message);
