namespace Lopen.Storage;

/// <summary>
/// Represents a cached section entry with content and file modification timestamp for invalidation.
/// </summary>
public sealed record SectionCacheEntry
{
    /// <summary>The cached section content.</summary>
    public required string Content { get; init; }

    /// <summary>The file modification timestamp at cache time, used for invalidation.</summary>
    public required DateTime FileModifiedUtc { get; init; }

    /// <summary>When this entry was cached.</summary>
    public required DateTime CachedAtUtc { get; init; }
}
