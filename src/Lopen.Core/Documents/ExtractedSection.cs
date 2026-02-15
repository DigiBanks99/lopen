namespace Lopen.Core.Documents;

/// <summary>
/// A document section with a token estimate, suitable for context budgeting.
/// </summary>
/// <param name="Header">Section header text.</param>
/// <param name="Content">Section body content.</param>
/// <param name="EstimatedTokens">Estimated token count (approx 4 chars/token).</param>
public sealed record ExtractedSection(string Header, string Content, int EstimatedTokens);
