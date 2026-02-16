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

    public void AddPhaseTransition(string fromPhase, string toPhase, IReadOnlyList<string>? sections = null)
    {
        var details = new List<string>();
        if (sections is { Count: > 0 })
        {
            foreach (var section in sections)
                details.Add(section);
        }

        AddEntry(new ActivityEntry
        {
            Summary = $"Phase: {fromPhase} → {toPhase}",
            Kind = ActivityEntryKind.PhaseTransition,
            Details = details
        });
    }

    public void AddFileEdit(string filePath, int linesAdded, int linesRemoved, IReadOnlyList<string>? diffLines = null)
    {
        var details = new List<string>();
        if (diffLines is { Count: > 0 })
        {
            foreach (var line in diffLines)
            {
                var marker = line.Length > 0 ? line[0] : ' ';
                var prefix = marker switch
                {
                    '+' => "+",
                    '-' => "-",
                    _ => " "
                };
                details.Add($"{prefix} {line}");
            }
        }

        AddEntry(new ActivityEntry
        {
            Summary = $"Edit {filePath} (+{linesAdded} -{linesRemoved})",
            Kind = ActivityEntryKind.FileEdit,
            Details = details,
            FullDocumentContent = diffLines is { Count: > 0 } ? string.Join("\n", diffLines) : null
        });
    }
}
