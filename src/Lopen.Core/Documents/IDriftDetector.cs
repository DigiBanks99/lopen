namespace Lopen.Core.Documents;

/// <summary>
/// Detects specification drift by comparing current section hashes against cached hashes.
/// </summary>
public interface IDriftDetector
{
    /// <summary>
    /// Detects sections that have changed since last cached.
    /// </summary>
    /// <param name="specificationPath">Path to the specification file.</param>
    /// <param name="currentContent">Current content of the specification.</param>
    /// <param name="cachedSections">Previously cached sections with hashes.</param>
    /// <returns>List of drift results for sections that have changed.</returns>
    IReadOnlyList<DriftResult> DetectDrift(
        string specificationPath,
        string currentContent,
        IReadOnlyList<CachedSection> cachedSections);
}

/// <summary>
/// Result of drift detection for a single section.
/// </summary>
/// <param name="Header">The section header.</param>
/// <param name="PreviousHash">Hash from the cached version.</param>
/// <param name="CurrentHash">Hash from the current version.</param>
/// <param name="IsNew">True if the section is new (not in cache).</param>
/// <param name="IsRemoved">True if the section was removed (in cache but not in current).</param>
public sealed record DriftResult(
    string Header,
    string? PreviousHash,
    string? CurrentHash,
    bool IsNew,
    bool IsRemoved);
