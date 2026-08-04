using Markdig;
using Markdig.Syntax;

namespace Lopen.Core.Documents;

/// <summary>
/// Specification parser implementation using Markdig for markdown AST parsing.
/// </summary>
internal sealed class MarkdigSpecificationParser : ISpecificationParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    /// <inheritdoc />
    public IReadOnlyList<DocumentSection> ExtractSections(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        MarkdownDocument document = Markdown.Parse(content, Pipeline);
        var sections = new List<DocumentSection>();
        var headings = document.OfType<HeadingBlock>().ToList();

        for (var i = 0; i < headings.Count; i++)
        {
            HeadingBlock heading = headings[i];
            var headerText = heading.Inline?.FirstChild?.ToString() ?? string.Empty;
            var level = heading.Level;

            var startOffset = heading.Span.End + 1;
            var endOffset = i + 1 < headings.Count
                ? headings[i + 1].Span.Start - 1
                : content.Length;

            var sectionContent = startOffset < endOffset
                ? content[startOffset..endOffset].Trim()
                : string.Empty;

            sections.Add(new DocumentSection(headerText, level, sectionContent));
        }

        return sections.AsReadOnly();
    }

    /// <inheritdoc />
    public string? ExtractSection(string content, string header)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(header);

        IReadOnlyList<DocumentSection> sections = ExtractSections(content);
        return sections.FirstOrDefault(s =>
            s.Header.Equals(header, StringComparison.OrdinalIgnoreCase))?.Content;
    }
}
