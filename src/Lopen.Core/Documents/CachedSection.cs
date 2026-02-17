namespace Lopen.Core.Documents;

/// <summary>
/// A cached section extracted from a specification or document.
/// </summary>
/// <param name="FilePath">Path to the source file.</param>
/// <param name="Header">The section header text.</param>
/// <param name="Content">The section content.</param>
/// <param name="ContentHash">Hash of the content for drift detection.</param>
/// <param name="Timestamp">When this section was last cached.</param>
public sealed record CachedSection(
    string FilePath,
    string Header,
    string Content,
    string ContentHash,
    DateTimeOffset Timestamp);
