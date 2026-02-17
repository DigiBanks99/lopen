namespace Lopen.Core.Documents;

/// <summary>
/// Parses specification documents and extracts sections.
/// </summary>
public interface ISpecificationParser
{
    /// <summary>
    /// Extracts all sections from a markdown specification document.
    /// </summary>
    /// <param name="content">The markdown content to parse.</param>
    /// <returns>A list of extracted sections with headers and content.</returns>
    IReadOnlyList<DocumentSection> ExtractSections(string content);

    /// <summary>
    /// Extracts a specific section by header from a markdown document.
    /// </summary>
    /// <param name="content">The markdown content to parse.</param>
    /// <param name="header">The section header to find.</param>
    /// <returns>The section content, or null if not found.</returns>
    string? ExtractSection(string content, string header);
}
