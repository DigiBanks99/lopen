using GitHub.Copilot.SDK;

namespace Lopen.Llm;

/// <summary>
/// Manages the lifecycle of a <see cref="CopilotClient"/> instance.
/// Provides an abstraction layer for testability and centralized auth configuration.
/// </summary>
public interface ICopilotClientProvider : IAsyncDisposable
{
    /// <summary>
    /// Gets or creates a started <see cref="CopilotClient"/> ready for session creation.
    /// The client is lazily initialized and reused across invocations.
    /// </summary>
    Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the Copilot SDK is authenticated.
    /// </summary>
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
}
