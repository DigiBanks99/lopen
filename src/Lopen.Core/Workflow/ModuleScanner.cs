using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Scans docs/requirements/ subfolders for module specifications.
/// Each subfolder is a module; a module is valid if it contains a SPECIFICATION.md file.
/// </summary>
internal sealed class ModuleScanner : IModuleScanner
{
    internal const string RequirementsRelativePath = "docs/requirements";
    internal const string SpecificationFileName = "SPECIFICATION.md";

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ModuleScanner> _logger;
    private readonly string _projectRoot;

    public ModuleScanner(IFileSystem fileSystem, ILogger<ModuleScanner> logger, string projectRoot)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = projectRoot;
    }

    public IReadOnlyList<ModuleInfo> ScanModules()
    {
        var requirementsPath = Path.Combine(_projectRoot, RequirementsRelativePath);

        if (!_fileSystem.DirectoryExists(requirementsPath))
        {
            _logger.LogWarning("Requirements directory not found: {Path}", requirementsPath);
            return [];
        }

        var modules = new List<ModuleInfo>();

        foreach (var dir in _fileSystem.GetDirectories(requirementsPath))
        {
            var moduleName = Path.GetFileName(dir);
            var specPath = Path.Combine(dir, SpecificationFileName);
            var hasSpec = _fileSystem.FileExists(specPath);

            modules.Add(new ModuleInfo(moduleName, specPath, hasSpec));

            if (!hasSpec)
            {
                _logger.LogWarning("Module '{Module}' has no {File}", moduleName, SpecificationFileName);
            }
        }

        _logger.LogInformation("Discovered {Count} module(s) in {Path}", modules.Count, requirementsPath);
        return modules.AsReadOnly();
    }
}
