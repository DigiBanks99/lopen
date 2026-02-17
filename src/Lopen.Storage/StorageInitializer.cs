using Microsoft.Extensions.Logging;

namespace Lopen.Storage;

/// <summary>
/// Ensures the .lopen/ directory structure exists in the project root.
/// Called on first workflow run.
/// </summary>
public sealed class StorageInitializer
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<StorageInitializer> _logger;
    private readonly string _projectRoot;

    public StorageInitializer(IFileSystem fileSystem, ILogger<StorageInitializer> logger, string projectRoot)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = projectRoot;
    }

    /// <summary>
    /// Creates the .lopen/ directory structure if it does not already exist.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public void EnsureDirectoryStructure()
    {
        var directories = new[]
        {
            StoragePaths.GetRoot(_projectRoot),
            StoragePaths.GetSessionsDirectory(_projectRoot),
            StoragePaths.GetModulesDirectory(_projectRoot),
            StoragePaths.GetCacheDirectory(_projectRoot),
            StoragePaths.GetSectionsCacheDirectory(_projectRoot),
            StoragePaths.GetAssessmentsCacheDirectory(_projectRoot),
            StoragePaths.GetCorruptedDirectory(_projectRoot),
        };

        foreach (var dir in directories)
        {
            if (!_fileSystem.DirectoryExists(dir))
            {
                _fileSystem.CreateDirectory(dir);
                _logger.LogDebug("Created directory: {Path}", dir);
            }
        }

        _logger.LogInformation("Storage directory structure ensured at {Root}", StoragePaths.GetRoot(_projectRoot));
    }

    /// <summary>
    /// Ensures the .lopen/ entry exists in the project's .gitignore file.
    /// Only modifies an existing .gitignore — does not create one if missing.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public async Task EnsureGitignoreEntryAsync(CancellationToken cancellationToken = default)
    {
        var gitignorePath = Path.Combine(_projectRoot, ".gitignore");

        if (!_fileSystem.FileExists(gitignorePath))
        {
            _logger.LogDebug("No .gitignore found at {Path}, skipping", gitignorePath);
            return;
        }

        var content = await _fileSystem.ReadAllTextAsync(gitignorePath, cancellationToken);

        if (HasLopenEntry(content))
        {
            _logger.LogDebug(".lopen/ already in .gitignore");
            return;
        }

        var newContent = content.Length > 0 && !content.EndsWith('\n')
            ? content + "\n.lopen/\n"
            : content + ".lopen/\n";

        await _fileSystem.WriteAllTextAsync(gitignorePath, newContent, cancellationToken);
        _logger.LogInformation("Added .lopen/ to .gitignore");
    }

    private static bool HasLopenEntry(string content)
    {
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed is ".lopen/" or ".lopen")
                return true;
        }
        return false;
    }
}
