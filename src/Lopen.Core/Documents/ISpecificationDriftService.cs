namespace Lopen.Core.Documents;

/// <summary>
/// High-level service that detects specification drift for a module by
/// reading the spec from disk, comparing against cached section hashes,
/// and returning any drift results.
/// </summary>
public interface ISpecificationDriftService
{
    /// <summary>
    /// Checks the specification for a module and returns any drift results.
    /// Returns an empty list when no drift is detected or the spec cannot be found.
    /// </summary>
    Task<IReadOnlyList<DriftResult>> CheckDriftAsync(
        string moduleName,
        CancellationToken cancellationToken = default);
}
