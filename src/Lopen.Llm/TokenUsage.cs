namespace Lopen.Llm;

/// <summary>
/// Token usage from a single SDK invocation.
/// </summary>
public sealed record TokenUsage(
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    int ContextWindowSize,
    bool IsPremiumRequest);
