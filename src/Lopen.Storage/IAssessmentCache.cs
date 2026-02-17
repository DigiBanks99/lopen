namespace Lopen.Storage;

/// <summary>
/// Caches codebase assessment results to avoid redundant LLM calls.
/// Cache is short-lived and invalidated on any file change in the assessed scope.
/// </summary>
public interface IAssessmentCache
{
    /// <summary>
    /// Gets a cached assessment if it exists and all files in its scope are unchanged.
    /// Returns null if not cached or invalidated.
    /// </summary>
    Task<AssessmentCacheEntry?> GetAsync(string scopeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an assessment result with the current file timestamps for the assessed scope.
    /// </summary>
    Task SetAsync(string scopeKey, string content, IReadOnlyDictionary<string, DateTime> fileTimestamps, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific cached assessment.
    /// </summary>
    Task InvalidateAsync(string scopeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire assessment cache.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
