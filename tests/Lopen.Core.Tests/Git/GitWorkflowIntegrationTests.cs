using Lopen.Configuration;
using Lopen.Core.Git;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Git;

/// <summary>
/// Integration tests that exercise GitWorkflowService with a real git CLI
/// against a temporary repository on disk.
/// </summary>
public sealed class GitWorkflowIntegrationTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task CommitTaskCompletion_WithRealGit_CreatesCommit()
    {
        var repoDir = CreateTempGitRepo();

        // Make a file change so there's something to commit
        await File.WriteAllTextAsync(Path.Combine(repoDir, "hello.txt"), "world");

        var (workflow, _) = CreateWorkflowService(repoDir);

        var result = await workflow.CommitTaskCompletionAsync("auth", "login", "implement-jwt");

        Assert.NotNull(result);
        Assert.True(result!.Success);

        // Verify git log contains the conventional commit message
        var logOutput = await RunGitAsync(repoDir, "log --oneline -1");
        Assert.Contains("feat(auth): complete implement-jwt in login", logOutput);
    }

    [Fact]
    public async Task CommitTaskCompletion_AutoCommitDisabled_NoCommit()
    {
        var repoDir = CreateTempGitRepo();

        await File.WriteAllTextAsync(Path.Combine(repoDir, "hello.txt"), "world");

        var options = new GitOptions { Enabled = true, AutoCommit = false };
        var (workflow, _) = CreateWorkflowService(repoDir, options);

        var result = await workflow.CommitTaskCompletionAsync("auth", "login", "implement-jwt");

        Assert.Null(result);

        // Only the initial commit should exist
        var logOutput = await RunGitAsync(repoDir, "rev-list --count HEAD");
        Assert.Equal("1", logOutput.Trim());
    }

    [Fact]
    public async Task CommitTaskCompletion_NothingToCommit_ReturnsGracefully()
    {
        var repoDir = CreateTempGitRepo();

        // No file changes — working tree is clean
        var (workflow, _) = CreateWorkflowService(repoDir);

        var result = await workflow.CommitTaskCompletionAsync("auth", "login", "implement-jwt");

        // Should return a result (not null, since auto-commit is enabled) but git commit
        // exits non-zero when there's nothing to commit.
        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    // --- Helpers ---

    private string CreateTempGitRepo()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);

        RunGitSync(dir, "init");
        RunGitSync(dir, "config user.email \"test@lopen.dev\"");
        RunGitSync(dir, "config user.name \"Lopen Test\"");

        // Create an initial commit so HEAD exists
        File.WriteAllText(Path.Combine(dir, "README.md"), "# test");
        RunGitSync(dir, "add -A");
        RunGitSync(dir, "commit -m \"initial commit\"");

        return dir;
    }

    private static (GitWorkflowService workflow, GitCliService git) CreateWorkflowService(
        string workingDirectory,
        GitOptions? options = null)
    {
        var gitService = new GitCliService(
            NullLogger<GitCliService>.Instance,
            workingDirectory);

        var gitOptions = options ?? new GitOptions();

        var workflow = new GitWorkflowService(
            gitService,
            gitOptions,
            NullLogger<GitWorkflowService>.Instance);

        return (workflow, gitService);
    }

    private static void RunGitSync(string workingDirectory, string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git {arguments} failed: {stderr}");
        }
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }
}
