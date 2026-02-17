namespace Lopen.Core.BackPressure;

/// <summary>
/// Context provided to guardrails for evaluation.
/// </summary>
/// <param name="ModuleName">The current module being worked on.</param>
/// <param name="TaskName">The current task name, if any.</param>
/// <param name="IterationCount">The current iteration count for the task.</param>
/// <param name="ToolCallCount">The number of tool calls in the current iteration.</param>
/// <param name="FileReadCounts">Per-file read counts in the current iteration (file path → count).</param>
/// <param name="CommandRetryCounts">Per-command retry counts in the current iteration (command → count).</param>
public sealed record GuardrailContext(
    string ModuleName,
    string? TaskName,
    int IterationCount,
    int ToolCallCount,
    IReadOnlyDictionary<string, int>? FileReadCounts = null,
    IReadOnlyDictionary<string, int>? CommandRetryCounts = null);
