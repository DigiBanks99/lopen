using GitHub.Copilot.SDK;
using Lopen.Auth;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Lopen.Llm;

/// <summary>
/// Manages a singleton <see cref="CopilotClient"/> lifecycle with auth token injection
/// from <see cref="IAuthTokenProvider"/>.
/// </summary>
internal sealed class CopilotClientProvider : ICopilotClientProvider
{
    private const string CopilotCliEnvironmentVariable = "COPILOT_CLI";
    private readonly IAuthTokenProvider _tokenProvider;
    private readonly ILogger<CopilotClientProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CopilotClient? _client;
    private bool _disposed;

    public CopilotClientProvider(
        IAuthTokenProvider tokenProvider,
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

            _client = await CreateClientAsync(cancellationToken);

            _logger.LogInformation("Starting Copilot SDK client");
            await _client.StartAsync(cancellationToken);
            _logger.LogInformation("Copilot SDK client started successfully");

            return _client;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to start Copilot SDK client");
            throw CopilotStartupFailureMapper.CreateDiagnosticException("Failed to start Copilot SDK client", ex);
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
            CopilotClient client = await GetClientAsync(cancellationToken);
            GetAuthStatusResponse authStatus = await client.GetAuthStatusAsync(cancellationToken);
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

    internal async Task<CopilotClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        EnsureCopilotCliPathConfigured();

        string? token = await _tokenProvider.GetTokenAsync(cancellationToken);
        CopilotClientOptions options = new()
        {
            UseStdio = true,
        };

        if (!string.IsNullOrEmpty(token))
        {
            options.GitHubToken = token;
            _logger.LogDebug("Copilot client configured with explicit GitHub token");
        }
        else
        {
            _logger.LogDebug("Copilot client using built-in credential chain");
        }

        return new CopilotClient(options);
    }

    internal void EnsureCopilotCliPathConfigured()
    {
        string? explicitCliPath = Environment.GetEnvironmentVariable(CopilotCliEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitCliPath))
        {
            if (!File.Exists(explicitCliPath))
            {
                throw new InvalidOperationException(
                    $"{CopilotCliEnvironmentVariable} is set but points to a missing file: '{explicitCliPath}'. " +
                    "Set COPILOT_CLI to a valid executable path.");
            }

            _logger.LogDebug("Using Copilot CLI path from {EnvironmentVariable}", CopilotCliEnvironmentVariable);
            return;
        }

        string? discoveredCliPath = FindCopilotCliInPath(Environment.GetEnvironmentVariable("PATH"));
        if (string.IsNullOrWhiteSpace(discoveredCliPath))
        {
            throw new InvalidOperationException(
                "Copilot CLI executable was not found on PATH. " +
                "Install Copilot CLI and ensure 'copilot' is on PATH, or set COPILOT_CLI to an absolute executable path.");
        }

        Environment.SetEnvironmentVariable(CopilotCliEnvironmentVariable, discoveredCliPath);
        _logger.LogInformation("Configured {EnvironmentVariable} from PATH discovery", CopilotCliEnvironmentVariable);
    }

    internal static string? FindCopilotCliInPath(string? pathValue)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string executableName = isWindows ? "copilot.exe" : "copilot";

        string[] pathSegments = pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string directory in pathSegments)
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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
