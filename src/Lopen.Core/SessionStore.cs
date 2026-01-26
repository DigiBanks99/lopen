using System.Text.Json;

namespace Lopen.Core;

/// <summary>
/// Serializable session state for persistence.
/// </summary>
public record PersistableSessionState
{
    /// <summary>
    /// Session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// When the session was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When the session was last saved.
    /// </summary>
    public DateTimeOffset SavedAt { get; init; }

    /// <summary>
    /// Number of commands executed.
    /// </summary>
    public int CommandCount { get; init; }

    /// <summary>
    /// Conversation history entries.
    /// </summary>
    public List<string> ConversationHistory { get; init; } = [];

    /// <summary>
    /// User preferences.
    /// </summary>
    public Dictionary<string, string> Preferences { get; init; } = [];

    /// <summary>
    /// User-provided session name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Creates a persistable state from a SessionState.
    /// </summary>
    public static PersistableSessionState FromSessionState(SessionState state, string? name = null) =>
        new()
        {
            SessionId = state.SessionId,
            StartedAt = state.StartedAt,
            SavedAt = DateTimeOffset.UtcNow,
            CommandCount = state.CommandCount,
            ConversationHistory = [.. state.ConversationHistory],
            Preferences = new Dictionary<string, string>(state.Preferences),
            Name = name
        };

    /// <summary>
    /// Restores a SessionState from this persistable state.
    /// </summary>
    public SessionState ToSessionState()
    {
        var state = new SessionState
        {
            SessionId = SessionId,
            StartedAt = StartedAt,
            CommandCount = CommandCount
        };
        foreach (var entry in ConversationHistory)
        {
            state.ConversationHistory.Add(entry);
        }
        foreach (var (key, value) in Preferences)
        {
            state.Preferences[key] = value;
        }
        return state;
    }
}

/// <summary>
/// Session summary for listing.
/// </summary>
public record SessionSummary
{
    /// <summary>
    /// Session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// User-provided session name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// When the session was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When the session was last saved.
    /// </summary>
    public DateTimeOffset SavedAt { get; init; }

    /// <summary>
    /// Number of commands executed.
    /// </summary>
    public int CommandCount { get; init; }

    /// <summary>
    /// Number of conversation entries.
    /// </summary>
    public int ConversationCount { get; init; }
}

/// <summary>
/// Interface for session persistence.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Saves a session state.
    /// </summary>
    Task SaveAsync(PersistableSessionState session);

    /// <summary>
    /// Loads a session state by ID or name.
    /// </summary>
    Task<PersistableSessionState?> LoadAsync(string sessionIdOrName);

    /// <summary>
    /// Deletes a session by ID or name.
    /// </summary>
    Task<bool> DeleteAsync(string sessionIdOrName);

    /// <summary>
    /// Lists all saved sessions.
    /// </summary>
    Task<IReadOnlyList<SessionSummary>> ListAsync();

    /// <summary>
    /// Checks if a session exists.
    /// </summary>
    Task<bool> ExistsAsync(string sessionIdOrName);
}

/// <summary>
/// File-based session storage using JSON files.
/// </summary>
public class FileSessionStore : ISessionStore
{
    private readonly string _sessionsDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileSessionStore() : this(GetDefaultSessionsDirectory())
    {
    }

    public FileSessionStore(string sessionsDirectory)
    {
        _sessionsDirectory = sessionsDirectory ?? throw new ArgumentNullException(nameof(sessionsDirectory));
    }

    private static string GetDefaultSessionsDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".lopen", "sessions");
    }

    public async Task SaveAsync(PersistableSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        EnsureDirectoryExists();

        var filePath = GetSessionFilePath(session.SessionId);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<PersistableSessionState?> LoadAsync(string sessionIdOrName)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrName))
            return null;

        // First try exact file match by ID
        var filePath = GetSessionFilePath(sessionIdOrName);
        if (File.Exists(filePath))
        {
            return await LoadFromFileAsync(filePath);
        }

        // Search by name
        var allSessions = await ListAllAsync();
        var match = allSessions.FirstOrDefault(s =>
            string.Equals(s.Name, sessionIdOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.SessionId, sessionIdOrName, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            var matchPath = GetSessionFilePath(match.SessionId);
            return await LoadFromFileAsync(matchPath);
        }

        return null;
    }

    public async Task<bool> DeleteAsync(string sessionIdOrName)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrName))
            return false;

        // First try exact file match by ID
        var filePath = GetSessionFilePath(sessionIdOrName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }

        // Search by name
        var allSessions = await ListAllAsync();
        var match = allSessions.FirstOrDefault(s =>
            string.Equals(s.Name, sessionIdOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.SessionId, sessionIdOrName, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            var matchPath = GetSessionFilePath(match.SessionId);
            if (File.Exists(matchPath))
            {
                File.Delete(matchPath);
                return true;
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<SessionSummary>> ListAsync()
    {
        var sessions = await ListAllAsync();
        return sessions
            .Select(s => new SessionSummary
            {
                SessionId = s.SessionId,
                Name = s.Name,
                StartedAt = s.StartedAt,
                SavedAt = s.SavedAt,
                CommandCount = s.CommandCount,
                ConversationCount = s.ConversationHistory.Count
            })
            .OrderByDescending(s => s.SavedAt)
            .ToList();
    }

    public async Task<bool> ExistsAsync(string sessionIdOrName)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrName))
            return false;

        var filePath = GetSessionFilePath(sessionIdOrName);
        if (File.Exists(filePath))
            return true;

        var allSessions = await ListAllAsync();
        return allSessions.Any(s =>
            string.Equals(s.Name, sessionIdOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.SessionId, sessionIdOrName, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_sessionsDirectory))
        {
            Directory.CreateDirectory(_sessionsDirectory);
        }
    }

    private string GetSessionFilePath(string sessionId) =>
        Path.Combine(_sessionsDirectory, $"{sessionId}.json");

    private async Task<PersistableSessionState?> LoadFromFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<PersistableSessionState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<PersistableSessionState>> ListAllAsync()
    {
        if (!Directory.Exists(_sessionsDirectory))
            return [];

        var sessions = new List<PersistableSessionState>();
        var files = Directory.GetFiles(_sessionsDirectory, "*.json");

        foreach (var file in files)
        {
            var session = await LoadFromFileAsync(file);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }
}

/// <summary>
/// Mock session store for testing.
/// </summary>
public class MockSessionStore : ISessionStore
{
    private readonly Dictionary<string, PersistableSessionState> _sessions = [];

    public bool SaveWasCalled { get; private set; }
    public bool LoadWasCalled { get; private set; }
    public bool DeleteWasCalled { get; private set; }
    public bool ListWasCalled { get; private set; }
    public string? LastRequestedIdOrName { get; private set; }

    public Task SaveAsync(PersistableSessionState session)
    {
        SaveWasCalled = true;
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<PersistableSessionState?> LoadAsync(string sessionIdOrName)
    {
        LoadWasCalled = true;
        LastRequestedIdOrName = sessionIdOrName;

        // Try by ID first
        if (_sessions.TryGetValue(sessionIdOrName, out var session))
            return Task.FromResult<PersistableSessionState?>(session);

        // Try by name
        var byName = _sessions.Values.FirstOrDefault(s =>
            string.Equals(s.Name, sessionIdOrName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(byName);
    }

    public Task<bool> DeleteAsync(string sessionIdOrName)
    {
        DeleteWasCalled = true;
        LastRequestedIdOrName = sessionIdOrName;

        // Try by ID first
        if (_sessions.Remove(sessionIdOrName))
            return Task.FromResult(true);

        // Try by name
        var key = _sessions
            .Where(kv => string.Equals(kv.Value.Name, sessionIdOrName, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .FirstOrDefault();

        if (key is not null)
        {
            _sessions.Remove(key);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<SessionSummary>> ListAsync()
    {
        ListWasCalled = true;
        var summaries = _sessions.Values
            .Select(s => new SessionSummary
            {
                SessionId = s.SessionId,
                Name = s.Name,
                StartedAt = s.StartedAt,
                SavedAt = s.SavedAt,
                CommandCount = s.CommandCount,
                ConversationCount = s.ConversationHistory.Count
            })
            .OrderByDescending(s => s.SavedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionSummary>>(summaries);
    }

    public Task<bool> ExistsAsync(string sessionIdOrName)
    {
        if (_sessions.ContainsKey(sessionIdOrName))
            return Task.FromResult(true);

        var exists = _sessions.Values.Any(s =>
            string.Equals(s.Name, sessionIdOrName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    /// <summary>
    /// Adds a session for testing.
    /// </summary>
    public MockSessionStore WithSession(PersistableSessionState session)
    {
        _sessions[session.SessionId] = session;
        return this;
    }
}
