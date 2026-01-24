using GitHub.Copilot.SDK;

namespace Lopen.Core;

/// <summary>
/// Service for interacting with GitHub Copilot via the SDK.
/// </summary>
public class CopilotService : ICopilotService
{
    private readonly CopilotClient _client;
    private bool _disposed;

    /// <summary>
    /// Creates a new CopilotService with default options.
    /// </summary>
    public CopilotService() : this(null)
    {
    }

    /// <summary>
    /// Creates a new CopilotService with custom options.
    /// </summary>
    public CopilotService(CopilotClientOptions? options)
    {
        _client = new CopilotClient(options);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureStartedAsync(ct);
            var ping = await _client.PingAsync(cancellationToken: ct);
            return ping != null;
        }
        catch (FileNotFoundException)
        {
            // Copilot CLI not found
            return false;
        }
        catch (InvalidOperationException)
        {
            // Connection failed
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureStartedAsync(ct);
            var status = await _client.GetAuthStatusAsync(ct);
            return new CopilotAuthStatus(
                IsAuthenticated: status.IsAuthenticated,
                AuthType: status.AuthType,
                Login: status.Login);
        }
        catch (FileNotFoundException)
        {
            return new CopilotAuthStatus(false);
        }
        catch (InvalidOperationException)
        {
            return new CopilotAuthStatus(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        var models = await _client.ListModelsAsync(ct);
        return models.Select(m => m.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<ICopilotSession> CreateSessionAsync(
        CopilotSessionOptions? options = null,
        CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);

        var sessionConfig = new SessionConfig
        {
            SessionId = options?.SessionId,
            Model = options?.Model ?? "gpt-5",
            Streaming = options?.Streaming ?? true
        };

        var session = await _client.CreateSessionAsync(sessionConfig, ct);
        return new CopilotSession(session);
    }

    /// <inheritdoc />
    public async Task<ICopilotSession> ResumeSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));

        await EnsureStartedAsync(ct);
        var session = await _client.ResumeSessionAsync(sessionId, cancellationToken: ct);
        return new CopilotSession(session);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CopilotSessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        await EnsureStartedAsync(ct);
        var sessions = await _client.ListSessionsAsync(ct);
        return sessions.Select(s => new CopilotSessionInfo(
            SessionId: s.SessionId,
            StartTime: s.StartTime,
            ModifiedTime: s.ModifiedTime,
            Summary: s.Summary
        )).ToList();
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));

        await EnsureStartedAsync(ct);
        await _client.DeleteSessionAsync(sessionId, ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _client.StopAsync();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task EnsureStartedAsync(CancellationToken ct)
    {
        if (_client.State == ConnectionState.Disconnected)
        {
            await _client.StartAsync(ct);
        }
    }
}
