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
}
