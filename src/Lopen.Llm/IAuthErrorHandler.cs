using GitHub.Copilot.SDK;

namespace Lopen.Llm;

/// <summary>
/// Handles authentication errors from the Copilot SDK's OnErrorOccurred hook.
/// Decides whether to retry (recoverable) or abort (non-recoverable).
/// </summary>
public interface IAuthErrorHandler
{
    /// <summary>
    /// Evaluates an SDK error and returns the appropriate hook output.
    /// Returns null if the error is not auth-related.
    /// </summary>
    Task<ErrorOccurredHookOutput?> HandleErrorAsync(
        ErrorOccurredHookInput input,
        CancellationToken cancellationToken = default);
}
