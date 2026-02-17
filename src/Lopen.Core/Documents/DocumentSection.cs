namespace Lopen.Core.Documents;

/// <summary>
/// A section extracted from a markdown document.
/// </summary>
/// <param name="Header">The section header text.</param>
/// <param name="Level">The heading level (1-6).</param>
/// <param name="Content">The section content (excluding the header).</param>
public sealed record DocumentSection(string Header, int Level, string Content);
