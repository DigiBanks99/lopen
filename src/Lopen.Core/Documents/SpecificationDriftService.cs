using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Documents;

/// <summary>
/// Detects specification drift for a module by reading the current spec,
/// retrieving cached sections, and delegating to <see cref="IDriftDetector"/>.
/// </summary>
internal sealed class SpecificationDriftService : ISpecificationDriftService
{
    private readonly IDriftDetector _driftDetector;
    private readonly ISpecificationParser _parser;
    private readonly IContentHasher _hasher;
    private readonly IModuleScanner _moduleScanner;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SpecificationDriftService> _logger;

    // In-memory cache of section hashes from the previous iteration.
    // Keyed by (filePath, header) → CachedSection.
    private readonly Dictionary<(string, string), CachedSection> _sectionCache = new();

    public SpecificationDriftService(
        IDriftDetector driftDetector,
        ISpecificationParser parser,
        IContentHasher hasher,
        IModuleScanner moduleScanner,
        IFileSystem fileSystem,
        ILogger<SpecificationDriftService> logger)
    {
        _driftDetector = driftDetector ?? throw new ArgumentNullException(nameof(driftDetector));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _moduleScanner = moduleScanner ?? throw new ArgumentNullException(nameof(moduleScanner));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DriftResult>> CheckDriftAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var specPath = ResolveSpecPath(moduleName);
        if (specPath is null)
        {
            _logger.LogDebug("No specification found for module {Module}, skipping drift check", moduleName);
            return [];
        }

        if (!_fileSystem.FileExists(specPath))
        {
            _logger.LogDebug("Specification file not found at {Path}, skipping drift check", specPath);
            return [];
        }

        var currentContent = await _fileSystem.ReadAllTextAsync(specPath, cancellationToken);

        // Get the previously cached sections for this spec
        var cachedSections = _sectionCache
            .Where(kv => kv.Key.Item1 == specPath)
            .Select(kv => kv.Value)
            .ToList();

        // Detect drift against cached hashes
        var driftResults = _driftDetector.DetectDrift(specPath, currentContent, cachedSections);

        if (driftResults.Count > 0)
        {
            _logger.LogInformation(
                "Specification drift detected in {Module}: {Count} section(s) changed",
                moduleName, driftResults.Count);
        }

        // Update the in-memory cache with current sections
        RefreshCache(specPath, currentContent);

        return driftResults;
    }

    private string? ResolveSpecPath(string moduleName)
    {
        var modules = _moduleScanner.ScanModules();
        var module = modules.FirstOrDefault(m =>
            string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        return module?.HasSpecification == true ? module.SpecificationPath : null;
    }

    private void RefreshCache(string specPath, string content)
    {
        // Remove old entries for this file
        var keysToRemove = _sectionCache.Keys.Where(k => k.Item1 == specPath).ToList();
        foreach (var key in keysToRemove)
        {
            _sectionCache.Remove(key);
        }

        // Parse and cache current sections
        var sections = _parser.ExtractSections(content);
        foreach (var section in sections)
        {
            var hash = _hasher.ComputeHash(section.Content);
            _sectionCache[(specPath, section.Header)] = new CachedSection(
                specPath,
                section.Header,
                section.Content,
                hash,
                DateTimeOffset.UtcNow);
        }
    }
}
