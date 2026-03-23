using Lopen.Core.Documents;
using Lopen.Storage;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lopen.Core.Workflow;

/// <summary>
/// Assesses workflow state from actual codebase state (spec, checkboxes, on-disk hint).
/// Also records step hints to disk so planning-phase steps survive process restarts.
/// Implements re-entrant assessment: filesystem truth overrides any hint when it can be derived.
/// </summary>
internal sealed class CodebaseStateAssessor : IStateAssessor, IStepRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IFileSystem _fileSystem;
    private readonly IModuleScanner _moduleScanner;
    private readonly string _projectRoot;
    private readonly ILogger<CodebaseStateAssessor> _logger;

    public CodebaseStateAssessor(
        IFileSystem fileSystem,
        IModuleScanner moduleScanner,
        string projectRoot,
        ILogger<CodebaseStateAssessor> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _moduleScanner = moduleScanner ?? throw new ArgumentNullException(nameof(moduleScanner));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = projectRoot;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkflowAssessment> AssessAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        IReadOnlyList<ModuleInfo> modules = _moduleScanner.ScanModules();
        ModuleInfo? module = modules.FirstOrDefault(m =>
            string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        if (module is null || !module.HasSpecification)
        {
            _logger.LogInformation("Module {Module}: no specification found, at DraftSpecification", moduleName);
            return new WorkflowAssessment(WorkflowStep.DraftSpecification, IsSpecReady: false, HasMoreComponents: false);
        }

        string content;
        try
        {
            content = await _fileSystem.ReadAllTextAsync(module.SpecificationPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read spec for module {Module}, defaulting to DraftSpecification", moduleName);
            return new WorkflowAssessment(WorkflowStep.DraftSpecification, IsSpecReady: false, HasMoreComponents: false);
        }

        (int total, int completed) = MarkdownUpdater.CountCheckboxes(content);

        // Filesystem truth — these branches need no hint
        if (total > 0 && completed == total)
        {
            _logger.LogInformation("Module {Module}: all {Total} ACs complete", moduleName, total);
            return new WorkflowAssessment(WorkflowStep.Repeat, IsSpecReady: true, HasMoreComponents: false);
        }

        if (completed > 0)
        {
            _logger.LogInformation(
                "Module {Module}: {Completed}/{Total} ACs complete, at IterateThroughTasks",
                moduleName, completed, total);
            return new WorkflowAssessment(WorkflowStep.IterateThroughTasks, IsSpecReady: true, HasMoreComponents: true);
        }

        // Ambiguous zone: spec exists, no checkboxes ticked yet — read on-disk hint
        WorkflowStep step = await TryReadHintAsync(moduleName, cancellationToken)
            ?? (content.Length > 100 ? WorkflowStep.DetermineDependencies : WorkflowStep.DraftSpecification);

        bool hasMore = total > 0;

        _logger.LogInformation("Module {Module}: at {Step} (hint or content-based)", moduleName, step);
        return new WorkflowAssessment(step, IsSpecReady: true, HasMoreComponents: hasMore);
    }

    public async Task RecordStepAsync(
        string moduleName,
        WorkflowStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var hintPath = StoragePaths.GetModuleAssessmentHintPath(_projectRoot, moduleName);
        var dir = Path.GetDirectoryName(hintPath)!;

        try
        {
            if (!_fileSystem.DirectoryExists(dir))
                _fileSystem.CreateDirectory(dir);

            var hint = new StepHint { Step = step };
            var json = JsonSerializer.Serialize(hint, JsonOptions);
            var tempPath = hintPath + ".tmp";
            await _fileSystem.WriteAllTextAsync(tempPath, json, cancellationToken);
            _fileSystem.MoveFile(tempPath, hintPath);

            _logger.LogDebug("Recorded step hint {Step} for module {Module}", step, moduleName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to write step hint for module {Module} — session resume may fall back to DetermineDependencies", moduleName);
        }
    }

    private async Task<WorkflowStep?> TryReadHintAsync(string moduleName, CancellationToken cancellationToken)
    {
        var hintPath = StoragePaths.GetModuleAssessmentHintPath(_projectRoot, moduleName);
        if (!_fileSystem.FileExists(hintPath))
            return null;

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(hintPath, cancellationToken);
            StepHint? hint = JsonSerializer.Deserialize<StepHint>(json, JsonOptions);
            if (hint is not null)
            {
                _logger.LogInformation("Module {Module}: using on-disk step hint {Step}", moduleName, hint.Step);
                return hint.Step;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Discarding unreadable step hint for module {Module}", moduleName);
        }

        return null;
    }

    private sealed class StepHint
    {
        public WorkflowStep Step { get; set; }
    }
}
