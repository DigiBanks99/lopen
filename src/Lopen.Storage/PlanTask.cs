namespace Lopen.Storage;

/// <summary>
/// Represents a single task item parsed from a plan markdown file.
/// </summary>
public sealed record PlanTask
{
    /// <summary>The task text (without the checkbox prefix).</summary>
    public required string Text { get; init; }

    /// <summary>Whether the checkbox is checked.</summary>
    public required bool IsCompleted { get; init; }

    /// <summary>Nesting level (0 = top-level, 1 = first indent, etc.).</summary>
    public required int Level { get; init; }
}
