namespace Lopen.Core.Documents;

/// <summary>
/// Tracks active document resources for a module, providing the list of
/// currently relevant documents (specifications, research, plans) for display
/// in the context panel.
/// </summary>
public interface IResourceTracker
{
    /// <summary>
    /// Returns the list of active resources for the specified module.
    /// Discovers SPECIFICATION.md, RESEARCH.md, RESEARCH-*.md, and plan.md files.
    /// </summary>
    Task<IReadOnlyList<TrackedResource>> GetActiveResourcesAsync(
        string moduleName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A tracked document resource with label and optional content preview.
/// </summary>
/// <param name="Label">Display label (e.g., "SPECIFICATION.md § Authentication").</param>
/// <param name="FilePath">Full path to the resource file.</param>
/// <param name="Content">Optional content of the resource (loaded on demand).</param>
public sealed record TrackedResource(string Label, string FilePath, string? Content = null);
