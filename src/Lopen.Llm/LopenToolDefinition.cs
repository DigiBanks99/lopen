namespace Lopen.Llm;

/// <summary>
/// Definition of a Lopen-managed tool registered with the SDK.
/// </summary>
public sealed record LopenToolDefinition(
    string Name,
    string Description,
    string? ParameterSchema = null,
    IReadOnlyList<WorkflowPhase>? AvailableInPhases = null);
