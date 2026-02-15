namespace Lopen.Core.Git;

/// <summary>
/// Result of a git command execution.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StdOut">Standard output content.</param>
/// <param name="StdErr">Standard error content.</param>
public sealed record GitResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>Whether the command completed successfully.</summary>
    public bool Success => ExitCode == 0;
}
