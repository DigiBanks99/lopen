using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Manages a singleton <see cref="CopilotClient"/> lifecycle with auth token injection
/// from <see cref="IGitHubTokenProvider"/>.
/// </summary>
internal sealed class CopilotClientProvider : ICopilotClientProvider
{
    private readonly IGitHubTokenProvider _tokenProvider;
    private readonly ILogger<CopilotClientProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CopilotClient? _client;
    private bool _disposed;

    public CopilotClientProvider(
        IGitHubTokenProvider tokenProvider,
        ILogger<CopilotClientProvider> logger)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is { State: ConnectionState.Connected })
        {
            return _client;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_client is { State: ConnectionState.Connected })
            {
                return _client;
            }

            // Dispose previous client if in error state
            if (_client is not null)
            {
                await DisposeClientAsync();
            }

            _client = CreateClient();

            _logger.LogInformation("Starting Copilot SDK client");
            await _client.StartAsync(cancellationToken);
            _logger.LogInformation("Copilot SDK client started successfully");

            return _client;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to start Copilot SDK client");
            throw new LlmException("Failed to start Copilot SDK client", model: null, ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var client = await GetClientAsync(cancellationToken);
            var authStatus = await client.GetAuthStatusAsync(cancellationToken);
            return authStatus.IsAuthenticated;
        }
        catch (LlmException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth status check failed");
            return false;
        }
    }

    internal CopilotClient CreateClient()
    {
        var token = _tokenProvider.GetToken();
        var options = new CopilotClientOptions
        {
            AutoRestart = true,
            UseStdio = true,
        };

        if (!string.IsNullOrEmpty(token))
        {
            options.GithubToken = token;
            _logger.LogDebug("Copilot client configured with explicit GitHub token");
        }
        else
        {
            _logger.LogDebug("Copilot client using built-in credential chain");
        }

        return new CopilotClient(options);
    }

    private async Task DisposeClientAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Copilot client");
            }

            _client = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisposeClientAsync();
        _lock.Dispose();
    }
}
