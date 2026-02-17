namespace Lopen.Storage;

/// <summary>
/// Represents a cached assessment result with file timestamps for scope-based invalidation.
/// </summary>
public sealed record AssessmentCacheEntry
{
    /// <summary>The cached assessment result as serialized content.</summary>
    public required string Content { get; init; }

    /// <summary>When this assessment was cached.</summary>
    public required DateTime CachedAtUtc { get; init; }

    /// <summary>
    /// File timestamps at the time of caching, keyed by file path.
    /// Used for invalidation: if any file's current mtime differs, the entry is invalid.
    /// </summary>
    public required IReadOnlyDictionary<string, DateTime> FileTimestamps { get; init; }
}
