namespace Lopen.Storage;

/// <summary>
/// Caches extracted document sections keyed by file path + section header + file modification timestamp.
/// Cache is invalidated when the source file changes.
/// </summary>
public interface ISectionCache
{
    /// <summary>
    /// Gets a cached section if it exists and is still valid (file has not been modified).
    /// Returns null if not cached or invalidated.
    /// </summary>
    Task<SectionCacheEntry?> GetAsync(string filePath, string sectionHeader, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a section in the cache with the current file modification timestamp.
    /// </summary>
    Task SetAsync(string filePath, string sectionHeader, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cached entries for a specific file path.
    /// </summary>
    Task InvalidateFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire section cache.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
