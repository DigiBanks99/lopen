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

        // Auto-expand errors/warnings and the latest entry
        var shouldExpand = entry.Kind == ActivityEntryKind.Error || entry.Details.Count > 0;
        var entryToAdd = entry with { IsExpanded = shouldExpand };

        // Collapse previous entries (except errors which stay expanded)
        CollapseNonErrorEntries();

        _entries.Enqueue(entryToAdd);
        // Reset to auto-scroll when new entry is added
        _scrollOffset = -1;
    }

    private void CollapseNonErrorEntries()
    {
        // ConcurrentQueue doesn't support in-place mutation, so we drain and re-enqueue
        var existing = new List<ActivityEntry>();
        while (_entries.TryDequeue(out var e))
            existing.Add(e);

        foreach (var e in existing)
        {
            var collapsed = e.Kind == ActivityEntryKind.Error
                ? e // Errors stay expanded
                : e with { IsExpanded = false };
            _entries.Enqueue(collapsed);
        }
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
