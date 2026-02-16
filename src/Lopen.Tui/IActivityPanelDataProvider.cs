namespace Lopen.Tui;

/// <summary>
/// Provides live <see cref="ActivityPanelData"/> by collecting workflow events.
/// Thread-safe for concurrent access from the orchestrator and TUI render loop.
/// </summary>
public interface IActivityPanelDataProvider
{
    /// <summary>
    /// Returns the current activity panel data snapshot.
    /// </summary>
    ActivityPanelData GetCurrentData();

    /// <summary>
    /// Adds a new activity entry. Thread-safe.
    /// </summary>
    void AddEntry(ActivityEntry entry);

    /// <summary>
    /// Clears all entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Adds a phase transition entry with summary and section details.
    /// </summary>
    void AddPhaseTransition(string fromPhase, string toPhase, IReadOnlyList<string>? sections = null);

    /// <summary>
    /// Adds a file edit entry with diff details for display in the activity panel.
    /// </summary>
    void AddFileEdit(string filePath, int linesAdded, int linesRemoved, IReadOnlyList<string>? diffLines = null);
}
