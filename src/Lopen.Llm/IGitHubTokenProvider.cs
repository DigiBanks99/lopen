namespace Lopen.Llm;

/// <summary>
/// Provides a GitHub token for Copilot SDK authentication.
/// Decouples the LLM module from the Auth module.
/// </summary>
public interface IGitHubTokenProvider
{
    /// <summary>
    /// Returns the GitHub token if available, or null to let the SDK
    /// resolve credentials from its built-in chain (env vars, gh CLI stored credentials).
    /// </summary>
    string? GetToken();
}
