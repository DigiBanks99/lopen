namespace Lopen.Core.Documents;

/// <summary>
/// Extracts relevant document sections for LLM context based on current task scope.
/// Only sections related to the current work are included, not full documents.
/// </summary>
public interface ISectionExtractor
{
    /// <summary>
    /// Extracts context sections relevant to the given headers from a specification document.
    /// </summary>
    /// <param name="specContent">Full specification markdown content.</param>
    /// <param name="relevantHeaders">Headers of sections relevant to current context (case-insensitive).</param>
    /// <returns>Extracted sections with token estimates.</returns>
    IReadOnlyList<ExtractedSection> ExtractRelevantSections(
        string specContent,
        IReadOnlyList<string> relevantHeaders);

    /// <summary>
    /// Extracts all sections from a specification and returns them with token estimates.
    /// Used when the full spec is relevant (e.g., during requirement gathering).
    /// </summary>
    /// <param name="specContent">Full specification markdown content.</param>
    /// <returns>All sections with token estimates.</returns>
    IReadOnlyList<ExtractedSection> ExtractAllSections(string specContent);
}
