namespace Lopen.Storage;

/// <summary>
/// Manages plan files stored as markdown with checkbox task hierarchies.
/// Plans are stored at .lopen/modules/{module}/plan.md.
/// </summary>
public interface IPlanManager
{
    /// <summary>Writes the full plan content for a module.</summary>
    Task WritePlanAsync(string module, string content, CancellationToken cancellationToken = default);

    /// <summary>Reads the full plan content for a module. Returns null if no plan exists.</summary>
    Task<string?> ReadPlanAsync(string module, CancellationToken cancellationToken = default);

    /// <summary>Returns true if a plan file exists for the module.</summary>
    Task<bool> PlanExistsAsync(string module, CancellationToken cancellationToken = default);

    /// <summary>
    /// Programmatically updates a checkbox in the plan by matching the task text.
    /// Returns true if the checkbox was found and updated.
    /// </summary>
    Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all task items from the plan, returning their text, completion state, and indentation level.
    /// </summary>
    Task<IReadOnlyList<PlanTask>> ReadTasksAsync(string module, CancellationToken cancellationToken = default);
}
