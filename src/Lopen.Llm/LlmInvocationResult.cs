namespace Lopen.Llm;

/// <summary>
/// Result of a single LLM SDK invocation.
/// </summary>
public sealed record LlmInvocationResult(
    string Output,
    TokenUsage TokenUsage,
    int ToolCallsMade,
    bool IsComplete);
