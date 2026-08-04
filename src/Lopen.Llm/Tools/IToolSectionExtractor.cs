namespace Lopen.Llm.Tools;

/// <summary>
/// Adapter interface for section extraction, avoiding a circular Lopen.Core → Lopen.Llm dependency.
/// Lopen.Core provides the concrete adapter bridging ISectionExtractor to this interface.
/// </summary>
public interface IToolSectionExtractor
{
    /// <summary>
    /// Extracts sections relevant to the given headers from document content.
    /// </summary>
    IReadOnlyList<ToolExtractedSection> ExtractRelevantSections(string content, IReadOnlyList<string> headers);
}

/// <summary>
/// Lightweight extracted section for tool operations.
/// </summary>
public sealed record ToolExtractedSection(string Header, string Content);
