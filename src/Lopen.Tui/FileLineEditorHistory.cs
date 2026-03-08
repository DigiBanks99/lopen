using RadLine;

namespace Lopen.Tui;

/// <summary>
/// File-backed line editor history that persists across sessions.
/// Implements RadLine's ILineEditorHistory with file storage.
/// </summary>
public sealed class FileLineEditorHistory : ILineEditorHistory
{
    private readonly string _filePath;
    private readonly List<string> _entries = [];
    private readonly object _lock = new();

    public FileLineEditorHistory(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        LoadFromFile();
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_lock)
        {
            // Skip consecutive duplicates
            if (_entries.Count > 0 && _entries[^1] == text)
                return;

            _entries.Add(text);
            AppendToFile(text);
        }
    }

    private void LoadFromFile()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var lines = File.ReadAllLines(_filePath);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Skip consecutive duplicates during load too
                    if (_entries.Count == 0 || _entries[^1] != line)
                    {
                        _entries.Add(line);
                    }
                }
            }
        }
        catch (IOException)
        {
            // If we can't read history, start fresh
        }
    }

    private void AppendToFile(string text)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllLines(_filePath, [text]);
        }
        catch (IOException)
        {
            // Best effort - don't crash if we can't write history
        }
    }
}
