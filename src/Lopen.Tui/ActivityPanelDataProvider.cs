using System.Collections.Concurrent;

namespace Lopen.Tui;

/// <summary>
/// Collects activity entries from workflow events and provides snapshots for the activity panel.
/// Thread-safe for concurrent writes (orchestrator) and reads (render loop).
/// </summary>
internal sealed class ActivityPanelDataProvider : IActivityPanelDataProvider
{
    private readonly ConcurrentQueue<ActivityEntry> _entries = new();
    private volatile int _scrollOffset = -1; // -1 = auto-scroll

    public ActivityPanelData GetCurrentData()
    {
        var entries = _entries.ToArray();
        return new ActivityPanelData
        {
            Entries = entries,
            ScrollOffset = _scrollOffset
        };
    }

    public void AddEntry(ActivityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Enqueue(entry);
        // Reset to auto-scroll when new entry is added
        _scrollOffset = -1;
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
