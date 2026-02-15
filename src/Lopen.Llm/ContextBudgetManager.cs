using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Manages context window budget by selecting high-priority sections and
/// truncating or omitting lower-priority ones when the budget is exceeded.
/// </summary>
internal sealed class ContextBudgetManager : IContextBudgetManager
{
    internal const int CharsPerToken = 4;
    internal const string TruncationSuffix = "\n\n[... truncated to fit context budget]";

    private readonly ILogger<ContextBudgetManager> _logger;

    public ContextBudgetManager(ILogger<ContextBudgetManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ContextSection> FitToBudget(
        IReadOnlyList<ContextSection> sections,
        int budgetTokens)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (budgetTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(budgetTokens), "Budget must be positive.");

        var result = new List<ContextSection>();
        var remainingTokens = budgetTokens;

        foreach (var section in sections)
        {
            if (remainingTokens <= 0)
            {
                _logger.LogDebug("Budget exhausted, omitting '{Title}'", section.Title);
                break;
            }

            if (section.EstimatedTokens <= remainingTokens)
            {
                result.Add(section);
                remainingTokens -= section.EstimatedTokens;
            }
            else
            {
                // Truncate to fit remaining budget
                var truncated = TruncateToTokens(section, remainingTokens);
                if (truncated is not null)
                {
                    result.Add(truncated);
                    _logger.LogDebug(
                        "Truncated '{Title}' from {Original} to {Truncated} tokens",
                        section.Title, section.EstimatedTokens, truncated.EstimatedTokens);
                }
                remainingTokens = 0;
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>Estimates token count from character count using a simple heuristic.</summary>
    public static int EstimateTokens(string content) =>
        string.IsNullOrEmpty(content) ? 0 : (content.Length + CharsPerToken - 1) / CharsPerToken;

    private static ContextSection? TruncateToTokens(ContextSection section, int maxTokens)
    {
        var maxChars = maxTokens * CharsPerToken;
        var suffixLength = TruncationSuffix.Length;

        if (maxChars <= suffixLength)
            return null;

        var truncateAt = Math.Min(maxChars - suffixLength, section.Content.Length);
        var truncatedContent = section.Content[..truncateAt] + TruncationSuffix;
        return new ContextSection(section.Title, truncatedContent, maxTokens);
    }
}
