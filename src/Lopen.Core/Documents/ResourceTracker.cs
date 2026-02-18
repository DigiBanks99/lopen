using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Documents;

/// <summary>
/// Discovers and tracks document resources (specifications, research, plans)
/// for a given module by scanning well-known paths.
/// </summary>
internal sealed class ResourceTracker : IResourceTracker
{
    private readonly IFileSystem _fileSystem;
    private readonly string _projectRoot;
    private readonly ILogger<ResourceTracker> _logger;

    public ResourceTracker(
        IFileSystem fileSystem,
        string projectRoot,
        ILogger<ResourceTracker> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TrackedResource>> GetActiveResourcesAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var requirementsDir = StoragePaths.GetModuleRequirementsDirectory(_projectRoot, moduleName);
        var resources = new List<TrackedResource>();

        if (_fileSystem.DirectoryExists(requirementsDir))
        {
            await TryAddResourceAsync(
                resources,
                Path.Combine(requirementsDir, "SPECIFICATION.md"),
                "SPECIFICATION.md",
                cancellationToken);

            await TryAddResourceAsync(
                resources,
                Path.Combine(requirementsDir, "RESEARCH.md"),
                "RESEARCH.md",
                cancellationToken);

            // Discover RESEARCH-*.md files
            try
            {
                foreach (var file in _fileSystem.GetFiles(requirementsDir, "RESEARCH-*.md"))
                {
                    var fileName = Path.GetFileName(file);
                    await TryAddResourceAsync(resources, file, fileName, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate research files in {Directory}", requirementsDir);
            }
        }

        // Check for plan.md
        var planPath = StoragePaths.GetModulePlanPath(_projectRoot, moduleName);
        await TryAddResourceAsync(resources, planPath, "plan.md", cancellationToken);

        return resources;
    }

    private async Task TryAddResourceAsync(
        List<TrackedResource> resources,
        string filePath,
        string label,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_fileSystem.FileExists(filePath))
                return;

            var content = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken);
            resources.Add(new TrackedResource(label, filePath, content));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read resource {FilePath}", filePath);
        }
    }
}
