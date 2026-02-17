using Microsoft.Extensions.Logging;

namespace Lopen.Core.Documents;

/// <summary>
/// Extracts section-level document content for LLM context windows.
/// Uses <see cref="ISpecificationParser"/> for markdown parsing.
/// Token estimation uses a 4 chars/token heuristic.
/// </summary>
internal sealed class SectionExtractor : ISectionExtractor
{
    private const int CharsPerToken = 4;

    private readonly ISpecificationParser _parser;
    private readonly ILogger<SectionExtractor> _logger;

    public SectionExtractor(ISpecificationParser parser, ILogger<SectionExtractor> logger)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ExtractedSection> ExtractRelevantSections(
        string specContent,
        IReadOnlyList<string> relevantHeaders)
    {
        ArgumentNullException.ThrowIfNull(specContent);
        ArgumentNullException.ThrowIfNull(relevantHeaders);

        if (relevantHeaders.Count == 0)
            return [];

        var allSections = _parser.ExtractSections(specContent);
        var headerSet = new HashSet<string>(relevantHeaders, StringComparer.OrdinalIgnoreCase);

        var result = new List<ExtractedSection>();

        foreach (var section in allSections)
        {
            if (headerSet.Contains(section.Header))
            {
                result.Add(ToExtracted(section));
            }
        }

        _logger.LogDebug(
            "Extracted {Count}/{Total} relevant sections from spec",
            result.Count, allSections.Count);

        return result.AsReadOnly();
    }

    public IReadOnlyList<ExtractedSection> ExtractAllSections(string specContent)
    {
        ArgumentNullException.ThrowIfNull(specContent);

        var allSections = _parser.ExtractSections(specContent);

        return allSections
            .Select(ToExtracted)
            .ToList()
            .AsReadOnly();
    }

    private static ExtractedSection ToExtracted(DocumentSection section) =>
        new(section.Header, section.Content, EstimateTokens(section.Content));

    internal static int EstimateTokens(string content) =>
        string.IsNullOrEmpty(content) ? 0 : (int)Math.Ceiling((double)content.Length / CharsPerToken);
}
