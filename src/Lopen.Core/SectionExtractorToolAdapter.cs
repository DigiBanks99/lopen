using Lopen.Core.Documents;
using Lopen.Llm.Tools;

namespace Lopen.Core;

/// <summary>
/// Adapts <see cref="ISectionExtractor"/> to <see cref="IToolSectionExtractor"/>
/// so ToolCatalog (in Lopen.Llm) can use section extraction without referencing Lopen.Core.
/// </summary>
internal sealed class SectionExtractorToolAdapter : IToolSectionExtractor
{
    private readonly ISectionExtractor _inner;

    public SectionExtractorToolAdapter(ISectionExtractor inner) => _inner = inner;

    public IReadOnlyList<ToolExtractedSection> ExtractRelevantSections(string content, IReadOnlyList<string> headers)
    {
        return _inner.ExtractRelevantSections(content, headers)
            .Select(s => new ToolExtractedSection(s.Header, s.Content))
            .ToList();
    }
}
