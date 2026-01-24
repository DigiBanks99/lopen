namespace Lopen.Core;

/// <summary>
/// Interface for command history management.
/// </summary>
public interface ICommandHistory
{
    /// <summary>
    /// Adds a command to history.
    /// </summary>
    void Add(string command);

    /// <summary>
    /// Gets the previous command in history (Up arrow).
    /// Returns null if at the beginning of history.
    /// </summary>
    string? GetPrevious();

    /// <summary>
    /// Gets the next command in history (Down arrow).
    /// Returns null if at the end of history (current input).
    /// </summary>
    string? GetNext();

    /// <summary>
    /// Resets navigation position to end of history.
    /// Call this when user starts typing a new command.
    /// </summary>
    void ResetPosition();

    /// <summary>
    /// Gets all commands in history.
    /// </summary>
    IReadOnlyList<string> GetAll();

    /// <summary>
    /// Gets the maximum number of entries to store.
    /// </summary>
    int MaxSize { get; }

    /// <summary>
    /// Gets the current number of entries in history.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Clears all history.
    /// </summary>
    void Clear();
}

/// <summary>
/// In-memory command history with optional file persistence.
/// </summary>
public class CommandHistory : ICommandHistory
{
    private readonly List<string> _history = [];
    private int _position = -1;

    /// <summary>
    /// Default maximum history size.
    /// </summary>
    public const int DefaultMaxSize = 1000;

    public int MaxSize { get; }

    public int Count => _history.Count;

    public CommandHistory(int maxSize = DefaultMaxSize)
    {
        if (maxSize < 1)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be at least 1");
        MaxSize = maxSize;
    }

    public void Add(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        // Don't add duplicates of the last command
        if (_history.Count > 0 && _history[^1] == command)
        {
            ResetPosition();
            return;
        }

        // Remove oldest if at capacity
        if (_history.Count >= MaxSize)
        {
            _history.RemoveAt(0);
        }

        _history.Add(command);
        ResetPosition();
    }

    public string? GetPrevious()
    {
        if (_history.Count == 0)
            return null;

        // Start from end if at initial position
        if (_position == -1)
        {
            _position = _history.Count - 1;
            return _history[_position];
        }

        // Move up if not at beginning
        if (_position > 0)
        {
            _position--;
            return _history[_position];
        }

        // At beginning, return first item
        return _history[0];
    }

    public string? GetNext()
    {
        if (_history.Count == 0 || _position == -1)
            return null;

        // Move down if not at end
        if (_position < _history.Count - 1)
        {
            _position++;
            return _history[_position];
        }

        // At end, reset position and return null (back to empty input)
        _position = -1;
        return null;
    }

    public void ResetPosition()
    {
        _position = -1;
    }

    public IReadOnlyList<string> GetAll() => _history.AsReadOnly();

    public void Clear()
    {
        _history.Clear();
        ResetPosition();
    }
}

/// <summary>
/// Command history with file persistence.
/// </summary>
public class PersistentCommandHistory : ICommandHistory
{
    private readonly CommandHistory _history;
    private readonly string _historyFilePath;

    public int MaxSize => _history.MaxSize;
    public int Count => _history.Count;

    public PersistentCommandHistory(string? historyFilePath = null, int maxSize = CommandHistory.DefaultMaxSize)
    {
        _historyFilePath = historyFilePath ?? GetDefaultHistoryPath();
        _history = new CommandHistory(maxSize);
        LoadHistory();
    }

    private static string GetDefaultHistoryPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".lopen", "history");
    }

    private void LoadHistory()
    {
        if (!File.Exists(_historyFilePath))
            return;

        try
        {
            var lines = File.ReadAllLines(_historyFilePath);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _history.Add(line);
                }
            }
        }
        catch
        {
            // Ignore errors reading history file
        }
    }

    private void SaveHistory()
    {
        try
        {
            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(_historyFilePath, _history.GetAll());
        }
        catch
        {
            // Ignore errors saving history file
        }
    }

    public void Add(string command)
    {
        _history.Add(command);
        SaveHistory();
    }

    public string? GetPrevious() => _history.GetPrevious();

    public string? GetNext() => _history.GetNext();

    public void ResetPosition() => _history.ResetPosition();

    public IReadOnlyList<string> GetAll() => _history.GetAll();

    public void Clear()
    {
        _history.Clear();
        SaveHistory();
    }
}
