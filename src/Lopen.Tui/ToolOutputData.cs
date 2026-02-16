namespace Lopen.Tui;

/// <summary>
/// Data model for a diff viewer showing file changes with line numbers.
/// </summary>
public sealed record DiffViewerData
{
    /// <summary>File path being changed.</summary>
    public required string FilePath { get; init; }

    /// <summary>Lines added count.</summary>
    public int LinesAdded { get; init; }

    /// <summary>Lines removed count.</summary>
    public int LinesRemoved { get; init; }

    /// <summary>Diff hunks to display.</summary>
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];
}

/// <summary>A hunk in a diff.</summary>
public sealed record DiffHunk
{
    /// <summary>Starting line number in the original file.</summary>
    public int StartLine { get; init; }

    /// <summary>Lines in the hunk (prefixed with +/-/space).</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];
}

/// <summary>
/// Data model for a phase transition summary.
/// </summary>
public sealed record PhaseTransitionData
{
    /// <summary>Phase being transitioned from.</summary>
    public required string FromPhase { get; init; }

    /// <summary>Phase being transitioned to.</summary>
    public required string ToPhase { get; init; }

    /// <summary>Summary sections (collapsible).</summary>
    public IReadOnlyList<TransitionSection> Sections { get; init; } = [];
}

/// <summary>A section in a phase transition summary.</summary>
public sealed record TransitionSection(string Title, IReadOnlyList<string> Items);

/// <summary>
/// Data model for inline research display.
/// </summary>
public sealed record ResearchDisplayData
{
    /// <summary>Research topic.</summary>
    public required string Topic { get; init; }

    /// <summary>Inline findings.</summary>
    public IReadOnlyList<string> Findings { get; init; } = [];

    /// <summary>Whether to show full document link.</summary>
    public bool HasFullDocument { get; init; }
}
