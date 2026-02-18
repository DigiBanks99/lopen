using System.Text.Json.Serialization;

namespace Lopen.Storage;

/// <summary>
/// Represents the persisted state of a workflow session.
/// </summary>
public sealed record SessionState
{
    /// <summary>The unique session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>The current workflow phase (e.g., "req-gathering", "planning", "building").</summary>
    public required string Phase { get; init; }

    /// <summary>The current workflow step within the phase.</summary>
    public required string Step { get; init; }

    /// <summary>The module being worked on.</summary>
    public required string Module { get; init; }

    /// <summary>The current component within the module, if any.</summary>
    public string? Component { get; init; }

    /// <summary>The timestamp when the session was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The timestamp of the last state update.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Whether the session has been completed.</summary>
    public bool IsComplete { get; init; }

    /// <summary>The models used during this session.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ModelsUsed { get; init; }

    /// <summary>The commit SHA of the last task-completion auto-commit, used for revert.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastTaskCompletionCommitSha { get; init; }

    /// <summary>The full task hierarchy tree (module → component → task → subtask) with states.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TaskHierarchyNode>? TaskHierarchy { get; init; }
}
