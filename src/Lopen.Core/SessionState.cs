namespace Lopen.Core;

/// <summary>
/// Represents the current session state maintained between REPL commands.
/// </summary>
public class SessionState
{
    /// <summary>
    /// Unique identifier for this session.
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// When the session was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// The authentication source (e.g., "environment variable", "stored credentials").
    /// </summary>
    public string? AuthSource { get; set; }

    /// <summary>
    /// Authenticated username, if known.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Number of commands executed in this session.
    /// </summary>
    public int CommandCount { get; set; }

    /// <summary>
    /// Conversation history for Copilot context (list of message IDs or summaries).
    /// </summary>
    public List<string> ConversationHistory { get; } = [];

    /// <summary>
    /// User preferences for this session.
    /// </summary>
    public Dictionary<string, string> Preferences { get; } = [];
}

/// <summary>
/// Service for managing session state.
/// </summary>
public interface ISessionStateService
{
    /// <summary>
    /// Gets the current session state.
    /// </summary>
    SessionState CurrentState { get; }

    /// <summary>
    /// Initializes a new session, optionally refreshing auth status.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Refreshes authentication status from the auth service.
    /// </summary>
    Task RefreshAuthStatusAsync();

    /// <summary>
    /// Records a command execution.
    /// </summary>
    void RecordCommand(string command);

    /// <summary>
    /// Adds a conversation entry to history.
    /// </summary>
    void AddConversationEntry(string entry);

    /// <summary>
    /// Sets a user preference.
    /// </summary>
    void SetPreference(string key, string value);

    /// <summary>
    /// Gets a user preference.
    /// </summary>
    string? GetPreference(string key);

    /// <summary>
    /// Resets the session to a fresh state.
    /// </summary>
    Task ResetAsync();

    /// <summary>
    /// Saves the current session with an optional name.
    /// </summary>
    Task SaveSessionAsync(string? name = null);

    /// <summary>
    /// Loads a saved session by ID or name.
    /// </summary>
    Task<bool> LoadSessionAsync(string sessionIdOrName);

    /// <summary>
    /// Deletes a saved session by ID or name.
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionIdOrName);

    /// <summary>
    /// Lists all saved sessions.
    /// </summary>
    Task<IReadOnlyList<SessionSummary>> ListSessionsAsync();
}

/// <summary>
/// Default implementation of session state management.
/// </summary>
public class SessionStateService : ISessionStateService
{
    private readonly IAuthService _authService;
    private readonly ISessionStore? _sessionStore;
    private SessionState _state;

    public SessionStateService(IAuthService authService) : this(authService, null)
    {
    }

    public SessionStateService(IAuthService authService, ISessionStore? sessionStore)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _sessionStore = sessionStore;
        _state = new SessionState();
    }

    public SessionState CurrentState => _state;

    public async Task InitializeAsync()
    {
        _state = new SessionState();
        await RefreshAuthStatusAsync();
    }

    public async Task RefreshAuthStatusAsync()
    {
        var authStatus = await _authService.GetStatusAsync();
        _state.IsAuthenticated = authStatus.IsAuthenticated;
        _state.AuthSource = authStatus.Source;
        _state.Username = authStatus.Username;
    }

    public void RecordCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;
        _state.CommandCount++;
    }

    public void AddConversationEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return;
        _state.ConversationHistory.Add(entry);
    }

    public void SetPreference(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _state.Preferences[key] = value;
    }

    public string? GetPreference(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _state.Preferences.TryGetValue(key, out var value) ? value : null;
    }

    public async Task ResetAsync()
    {
        await InitializeAsync();
    }

    public async Task SaveSessionAsync(string? name = null)
    {
        if (_sessionStore is null)
            throw new InvalidOperationException("Session store not configured");

        var persistable = PersistableSessionState.FromSessionState(_state, name);
        await _sessionStore.SaveAsync(persistable);
    }

    public async Task<bool> LoadSessionAsync(string sessionIdOrName)
    {
        if (_sessionStore is null)
            throw new InvalidOperationException("Session store not configured");

        var persistable = await _sessionStore.LoadAsync(sessionIdOrName);
        if (persistable is null)
            return false;

        _state = persistable.ToSessionState();
        await RefreshAuthStatusAsync();
        return true;
    }

    public async Task<bool> DeleteSessionAsync(string sessionIdOrName)
    {
        if (_sessionStore is null)
            throw new InvalidOperationException("Session store not configured");

        return await _sessionStore.DeleteAsync(sessionIdOrName);
    }

    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync()
    {
        if (_sessionStore is null)
            throw new InvalidOperationException("Session store not configured");

        return await _sessionStore.ListAsync();
    }
}
