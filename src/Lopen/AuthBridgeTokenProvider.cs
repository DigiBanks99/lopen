using Lopen.Auth;
using Lopen.Llm;

namespace Lopen;

/// <summary>
/// Bridges <see cref="ITokenSourceResolver"/> from the Auth module to
/// <see cref="IGitHubTokenProvider"/> consumed by the LLM module.
/// </summary>
internal sealed class AuthBridgeTokenProvider : IGitHubTokenProvider
{
    private readonly ITokenSourceResolver _resolver;

    public AuthBridgeTokenProvider(ITokenSourceResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public string? GetToken()
    {
        var result = _resolver.Resolve();
        return result.Token;
    }
}
