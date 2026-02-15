namespace Lopen.Llm;

/// <summary>
/// Service for invoking the Copilot SDK.
/// Each call represents a single SDK invocation with a fresh context window.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Invokes the Copilot SDK with the given prompt, model, and tool definitions.
    /// </summary>
    Task<LlmInvocationResult> InvokeAsync(
        string systemPrompt,
        string model,
        IReadOnlyList<LopenToolDefinition> tools,
        CancellationToken cancellationToken = default);
}
