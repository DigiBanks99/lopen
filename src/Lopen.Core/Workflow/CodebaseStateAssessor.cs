using Lopen.Core.Documents;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Assesses the current workflow step from actual codebase state rather than stale session data.
/// Implements re-entrant assessment: session state is a hint, not ground truth.
/// </summary>
internal sealed class CodebaseStateAssessor : IStateAssessor
{
    private readonly IFileSystem _fileSystem;
    private readonly IModuleScanner _moduleScanner;
    private readonly ILogger<CodebaseStateAssessor> _logger;
    private readonly Dictionary<string, WorkflowStep> _persistedSteps = new(StringComparer.OrdinalIgnoreCase);

    public CodebaseStateAssessor(
        IFileSystem fileSystem,
        IModuleScanner moduleScanner,
        ILogger<CodebaseStateAssessor> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _moduleScanner = moduleScanner ?? throw new ArgumentNullException(nameof(moduleScanner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<WorkflowStep> GetCurrentStepAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        // Check if spec exists — if not, still at drafting
        var modules = _moduleScanner.ScanModules();
        var module = modules.FirstOrDefault(m =>
            string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        if (module is null || !module.HasSpecification)
        {
            _logger.LogInformation("Module {Module}: no specification found, at DraftSpecification", moduleName);
            return Task.FromResult(WorkflowStep.DraftSpecification);
        }

        // Read spec content and check checkbox state
        string content;
        try
        {
            content = _fileSystem.ReadAllTextAsync(module.SpecificationPath).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read spec for module {Module}, defaulting to DraftSpecification", moduleName);
            return Task.FromResult(WorkflowStep.DraftSpecification);
        }

        var (total, completed) = MarkdownUpdater.CountCheckboxes(content);

        // If all criteria are met, the module is complete (Repeat step)
        if (total > 0 && completed == total)
        {
            _logger.LogInformation("Module {Module}: all {Total} ACs complete", moduleName, total);
            return Task.FromResult(WorkflowStep.Repeat);
        }

        // If some work has been done, we're in the build cycle
        if (completed > 0)
        {
            _logger.LogInformation(
                "Module {Module}: {Completed}/{Total} ACs complete, at IterateThroughTasks",
                moduleName, completed, total);
            return Task.FromResult(WorkflowStep.IterateThroughTasks);
        }

        // If persisted state is available, use it as a hint
        if (_persistedSteps.TryGetValue(moduleName, out var persisted))
        {
            _logger.LogInformation("Module {Module}: using persisted step {Step}", moduleName, persisted);
            return Task.FromResult(persisted);
        }

        // Spec exists but no progress — check if we're past requirement gathering
        // If the spec has content, assume requirements are gathered
        if (content.Length > 100)
        {
            _logger.LogInformation("Module {Module}: spec exists with content, at DetermineDependencies", moduleName);
            return Task.FromResult(WorkflowStep.DetermineDependencies);
        }

        return Task.FromResult(WorkflowStep.DraftSpecification);
    }

    public Task PersistStepAsync(string moduleName, WorkflowStep step, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        _persistedSteps[moduleName] = step;
        _logger.LogDebug("Persisted step {Step} for module {Module}", step, moduleName);
        return Task.CompletedTask;
    }

    public Task<bool> IsSpecReadyAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var modules = _moduleScanner.ScanModules();
        var module = modules.FirstOrDefault(m =>
            string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(module is { HasSpecification: true });
    }

    public Task<bool> HasMoreComponentsAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var modules = _moduleScanner.ScanModules();
        var module = modules.FirstOrDefault(m =>
            string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        if (module is null || !module.HasSpecification)
            return Task.FromResult(false);

        try
        {
            var content = _fileSystem.ReadAllTextAsync(module.SpecificationPath).GetAwaiter().GetResult();
            var (total, completed) = MarkdownUpdater.CountCheckboxes(content);
            return Task.FromResult(total > 0 && completed < total);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read spec for {Module}", moduleName);
            return Task.FromResult(false);
        }
    }
}
