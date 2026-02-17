namespace Lopen.Llm;

/// <summary>
/// Assembles structured system prompts for SDK invocations.
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Builds a system prompt for the given workflow context.
    /// </summary>
    string BuildSystemPrompt(
        WorkflowPhase phase,
        string module,
        string? component,
        string? task,
        IReadOnlyDictionary<string, string>? contextSections = null);
}
