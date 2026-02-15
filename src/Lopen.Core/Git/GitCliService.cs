using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Git;

/// <summary>
/// Git service implementation that executes git commands via the CLI.
/// </summary>
internal sealed class GitCliService : IGitService
{
    private readonly ILogger<GitCliService> _logger;
    private readonly string _workingDirectory;

    public GitCliService(ILogger<GitCliService> logger, string workingDirectory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        _workingDirectory = workingDirectory;
    }

    /// <inheritdoc />
    public async Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var addResult = await RunGitAsync("add -A", cancellationToken).ConfigureAwait(false);
        if (!addResult.Success)
            return addResult;

        return await RunGitAsync($"commit -m \"{message.Replace("\"", "\\\"")}\"", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        return await RunGitAsync($"checkout -b {branchName}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        return await RunGitAsync($"reset --hard {commitSha}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync("log -1 --format=%aI", cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
            return null;

        return DateTimeOffset.TryParse(result.StdOut.Trim(), out var date) ? date : null;
    }

    /// <inheritdoc />
    public async Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync("diff", cancellationToken).ConfigureAwait(false);
        return result.StdOut;
    }

    /// <inheritdoc />
    public async Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync("rev-parse HEAD", cancellationToken).ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : null;
    }

    private async Task<GitResult> RunGitAsync(string arguments, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Running: git {Arguments}", arguments);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new GitException("Failed to start git process.", $"git {arguments}");

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var result = new GitResult(process.ExitCode, stdout, stderr);

            if (!result.Success)
            {
                _logger.LogWarning("git {Arguments} failed (exit {Code}): {StdErr}", arguments, result.ExitCode, stderr);
            }

            return result;
        }
        catch (Exception ex) when (ex is not GitException and not OperationCanceledException)
        {
            throw new GitException($"Failed to execute git {arguments}", $"git {arguments}", ex);
        }
    }
}
