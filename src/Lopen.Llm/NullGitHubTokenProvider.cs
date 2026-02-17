namespace Lopen.Llm;

/// <summary>
/// Default token provider that returns null, allowing the Copilot SDK
/// to resolve credentials from its built-in chain.
/// </summary>
internal sealed class NullGitHubTokenProvider : IGitHubTokenProvider
{
    public string? GetToken() => null;
}
