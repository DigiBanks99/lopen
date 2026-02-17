using Lopen.Configuration;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Git;

/// <summary>
/// Implements workflow-aware git operations: auto-commit and branch-per-module.
/// </summary>
internal sealed class GitWorkflowService : IGitWorkflowService
{
    internal const string BranchPrefix = "lopen/";

    private readonly IGitService _gitService;
    private readonly GitOptions _gitOptions;
    private readonly ILogger<GitWorkflowService> _logger;

    public GitWorkflowService(
        IGitService gitService,
        GitOptions gitOptions,
        ILogger<GitWorkflowService> logger)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _gitOptions = gitOptions ?? throw new ArgumentNullException(nameof(gitOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GitResult?> EnsureModuleBranchAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        if (!_gitOptions.Enabled)
        {
            _logger.LogDebug("Git disabled, skipping branch creation for {Module}", moduleName);
            return null;
        }

        var branchName = $"{BranchPrefix}{moduleName}";
        _logger.LogInformation("Creating module branch {Branch}", branchName);

        try
        {
            return await _gitService.CreateBranchAsync(branchName, cancellationToken);
        }
        catch (GitException ex)
        {
            _logger.LogWarning(ex, "Failed to create branch {Branch}, may already exist", branchName);
            return new GitResult(ex.ExitCode, string.Empty, ex.StdErr ?? ex.Message);
        }
    }

    public async Task<GitResult?> CommitTaskCompletionAsync(
        string moduleName,
        string componentName,
        string taskName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);

        if (!_gitOptions.Enabled || !_gitOptions.AutoCommit)
        {
            _logger.LogDebug(
                "Auto-commit disabled, skipping commit for task {Task}", taskName);
            return null;
        }

        var message = FormatCommitMessage(moduleName, componentName, taskName);
        _logger.LogInformation("Auto-committing task completion: {Message}", message);

        try
        {
            return await _gitService.CommitAllAsync(message, cancellationToken);
        }
        catch (GitException ex)
        {
            _logger.LogWarning(ex, "Auto-commit failed for task {Task}", taskName);
            return new GitResult(ex.ExitCode, string.Empty, ex.StdErr ?? ex.Message);
        }
    }

    public string FormatCommitMessage(string moduleName, string componentName, string taskName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);

        return _gitOptions.Convention switch
        {
            "conventional" => $"feat({moduleName}): complete {taskName} in {componentName}",
            _ => $"[{moduleName}] Complete {taskName} in {componentName}",
        };
    }
}
