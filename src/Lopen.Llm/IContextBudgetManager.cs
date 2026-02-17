namespace Lopen.Llm;

/// <summary>
/// Manages context window budget by selecting and truncating document sections
/// to fit within token limits.
/// </summary>
public interface IContextBudgetManager
{
    /// <summary>
    /// Selects sections that fit within the budget, truncating lower-priority sections as needed.
    /// Sections are ordered by priority (first = highest priority).
    /// </summary>
    /// <param name="sections">Sections ordered by priority (highest first).</param>
    /// <param name="budgetTokens">Maximum token budget for context.</param>
    /// <returns>Sections that fit within budget, with lower-priority ones truncated or omitted.</returns>
    IReadOnlyList<ContextSection> FitToBudget(
        IReadOnlyList<ContextSection> sections,
        int budgetTokens);
}

/// <summary>
/// A section of context with metadata for budget management.
/// </summary>
/// <param name="Title">Section title for display.</param>
/// <param name="Content">Full section content.</param>
/// <param name="EstimatedTokens">Estimated token count for this section.</param>
public sealed record ContextSection(string Title, string Content, int EstimatedTokens);
