using Microsoft.Extensions.Logging;

namespace Lopen.Core.Documents;

/// <summary>
/// Detects specification drift by comparing section hashes.
/// Used to flag when spec sections change between workflow iterations.
/// </summary>
internal sealed class DriftDetector : IDriftDetector
{
    private readonly ISpecificationParser _parser;
    private readonly IContentHasher _hasher;
    private readonly ILogger<DriftDetector> _logger;

    public DriftDetector(ISpecificationParser parser, IContentHasher hasher, ILogger<DriftDetector> logger)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<DriftResult> DetectDrift(
        string specificationPath,
        string currentContent,
        IReadOnlyList<CachedSection> cachedSections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationPath);
        ArgumentNullException.ThrowIfNull(currentContent);
        ArgumentNullException.ThrowIfNull(cachedSections);

        var currentSections = _parser.ExtractSections(currentContent);
        var cachedByHeader = cachedSections
            .Where(c => c.FilePath == specificationPath)
            .ToDictionary(c => c.Header, c => c, StringComparer.OrdinalIgnoreCase);

        var results = new List<DriftResult>();

        foreach (var section in currentSections)
        {
            var currentHash = _hasher.ComputeHash(section.Content);

            if (cachedByHeader.TryGetValue(section.Header, out var cached))
            {
                if (_hasher.HasDrifted(section.Content, cached.ContentHash))
                {
                    _logger.LogWarning("Drift detected in '{Header}' of {Path}", section.Header, specificationPath);
                    results.Add(new DriftResult(section.Header, cached.ContentHash, currentHash, IsNew: false, IsRemoved: false));
                }
                cachedByHeader.Remove(section.Header);
            }
            else
            {
                _logger.LogInformation("New section '{Header}' in {Path}", section.Header, specificationPath);
                results.Add(new DriftResult(section.Header, PreviousHash: null, currentHash, IsNew: true, IsRemoved: false));
            }
        }

        // Remaining cached sections were removed
        foreach (var removed in cachedByHeader.Values)
        {
            _logger.LogWarning("Section '{Header}' removed from {Path}", removed.Header, specificationPath);
            results.Add(new DriftResult(removed.Header, removed.ContentHash, CurrentHash: null, IsNew: false, IsRemoved: true));
        }

        return results.AsReadOnly();
    }
}
