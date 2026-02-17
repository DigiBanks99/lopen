namespace Lopen.Core.Documents;

/// <summary>
/// Hashes document content for drift detection.
/// </summary>
public interface IContentHasher
{
    /// <summary>
    /// Computes a hash of the given content.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>A hex string hash of the content.</returns>
    string ComputeHash(string content);

    /// <summary>
    /// Checks whether the content has drifted from the expected hash.
    /// </summary>
    /// <param name="content">The current content.</param>
    /// <param name="expectedHash">The previously computed hash.</param>
    /// <returns>True if the content has changed.</returns>
    bool HasDrifted(string content, string expectedHash);
}
