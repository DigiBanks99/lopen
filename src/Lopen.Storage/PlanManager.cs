using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Lopen.Storage;

/// <summary>
/// Manages plan markdown files with checkbox task hierarchies.
/// Uses line-based text manipulation for reliable checkbox toggling.
/// </summary>
public sealed partial class PlanManager : IPlanManager
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PlanManager> _logger;
    private readonly string _projectRoot;

    [GeneratedRegex(@"^(\s*)- \[([ xX])\] (.+)$")]
    private static partial Regex CheckboxPattern();

    public PlanManager(IFileSystem fileSystem, ILogger<PlanManager> logger, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        _fileSystem = fileSystem;
        _logger = logger;
        _projectRoot = projectRoot;
    }

    public async Task WritePlanAsync(string module, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentNullException.ThrowIfNull(content);

        var planPath = StoragePaths.GetModulePlanPath(_projectRoot, module);
        var directory = Path.GetDirectoryName(planPath)!;

        try
        {
            if (!_fileSystem.DirectoryExists(directory))
                _fileSystem.CreateDirectory(directory);

            var tempPath = planPath + ".tmp";
            await _fileSystem.WriteAllTextAsync(tempPath, content, cancellationToken);
            _fileSystem.MoveFile(tempPath, planPath);

            _logger.LogDebug("Plan written for module {Module}", module);
        }
        catch (IOException ex)
        {
            throw new StorageException($"Failed to write plan for module '{module}'.", planPath, ex);
        }
    }

    public async Task<string?> ReadPlanAsync(string module, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var planPath = StoragePaths.GetModulePlanPath(_projectRoot, module);

        if (!_fileSystem.FileExists(planPath))
            return null;

        try
        {
            return await _fileSystem.ReadAllTextAsync(planPath, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new StorageException($"Failed to read plan for module '{module}'.", planPath, ex);
        }
    }

    public Task<bool> PlanExistsAsync(string module, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var planPath = StoragePaths.GetModulePlanPath(_projectRoot, module);
        return Task.FromResult(_fileSystem.FileExists(planPath));
    }

    public async Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskText);

        var content = await ReadPlanAsync(module, cancellationToken);
        if (content is null)
            return false;

        var lines = content.Split('\n');
        var found = false;
        var regex = CheckboxPattern();

        for (var i = 0; i < lines.Length; i++)
        {
            var match = regex.Match(lines[i]);
            if (!match.Success)
                continue;

            var text = match.Groups[3].Value;
            if (!string.Equals(text.Trim(), taskText.Trim(), StringComparison.Ordinal))
                continue;

            var indent = match.Groups[1].Value;
            var marker = completed ? "x" : " ";
            lines[i] = $"{indent}- [{marker}] {text}";
            found = true;
            break;
        }

        if (!found)
            return false;

        var updated = string.Join('\n', lines);
        await WritePlanAsync(module, updated, cancellationToken);

        _logger.LogDebug("Checkbox updated for task '{TaskText}' in module {Module}: {Completed}", taskText, module, completed);
        return true;
    }

    public async Task<IReadOnlyList<PlanTask>> ReadTasksAsync(string module, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var content = await ReadPlanAsync(module, cancellationToken);
        if (content is null)
            return [];

        var tasks = new List<PlanTask>();
        var regex = CheckboxPattern();

        foreach (var line in content.Split('\n'))
        {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var indent = match.Groups[1].Value;
            var isChecked = match.Groups[2].Value is "x" or "X";
            var text = match.Groups[3].Value;

            // Calculate level: 2 spaces or 1 tab = 1 level
            var level = indent.Length >= 2 ? indent.Length / 2 : 0;

            tasks.Add(new PlanTask
            {
                Text = text,
                IsCompleted = isChecked,
                Level = level,
            });
        }

        return tasks;
    }
}
