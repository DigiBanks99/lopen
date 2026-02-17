using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Handles authentication errors during Copilot SDK sessions.
/// Recoverable errors (expired tokens) trigger a retry; non-recoverable
/// errors (revoked tokens) save session state and abort.
/// </summary>
internal sealed class AuthErrorHandler : IAuthErrorHandler
{
    private static readonly string[] AuthKeywords =
        ["401", "403", "unauthorized", "forbidden", "authentication", "auth"];

    private const int MaxRetries = 1;

    private readonly ISessionStateSaver _stateSaver;
    private readonly ILogger<AuthErrorHandler> _logger;

    private int _retryCount;

    public AuthErrorHandler(
        ISessionStateSaver stateSaver,
        ILogger<AuthErrorHandler> logger)
    {
        _stateSaver = stateSaver ?? throw new ArgumentNullException(nameof(stateSaver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ErrorOccurredHookOutput?> HandleErrorAsync(
        ErrorOccurredHookInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!IsAuthError(input))
        {
            return null;
        }

        _logger.LogWarning("Auth error detected: {Error} (Recoverable={Recoverable})",
            input.Error, input.Recoverable);

        // Recoverable and within retry budget → retry (SDK refreshes token)
        if (input.Recoverable && _retryCount < MaxRetries)
        {
            _retryCount++;
            _logger.LogInformation("Attempting transparent token renewal (retry {Count}/{Max})",
                _retryCount, MaxRetries);

            return new ErrorOccurredHookOutput
            {
                ErrorHandling = "retry",
                RetryCount = 1,
            };
        }

        // Non-recoverable or retries exhausted → save state and abort
        _logger.LogError("Non-recoverable auth error. Saving session state and aborting.");

        try
        {
            await _stateSaver.SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session state during auth error handling");
        }

        return new ErrorOccurredHookOutput
        {
            ErrorHandling = "abort",
            UserNotification = "Authentication failed: your credentials have been revoked or are no longer valid. "
                + "Session state has been saved. Run 'lopen auth login' or set GH_TOKEN to re-authenticate.",
        };
    }

    /// <summary>
    /// Resets the retry counter. Call at the start of each new session.
    /// </summary>
    internal void ResetRetryCount() => _retryCount = 0;

    private static bool IsAuthError(ErrorOccurredHookInput input)
    {
        var error = input.Error;
        if (string.IsNullOrEmpty(error))
            return false;

        foreach (var keyword in AuthKeywords)
        {
            if (error.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
