namespace Lopen.Core.Git;

/// <summary>
/// Exception thrown when a git operation fails.
/// </summary>
public sealed class GitException : Exception
{
    /// <summary>The git command that was executed.</summary>
    public string Command { get; }

    /// <summary>The process exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Standard error output from the git command.</summary>
    public string StdErr { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="GitException"/>.
    /// </summary>
    public GitException(string message, string command)
        : base(message)
    {
        Command = command;
        StdErr = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GitException"/> with exit code and stderr.
    /// </summary>
    public GitException(string message, string command, int exitCode, string stdErr)
        : base(message)
    {
        Command = command;
        ExitCode = exitCode;
        StdErr = stdErr;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GitException"/> with an inner exception.
    /// </summary>
    public GitException(string message, string command, Exception innerException)
        : base(message, innerException)
    {
        Command = command;
        StdErr = string.Empty;
    }
}
