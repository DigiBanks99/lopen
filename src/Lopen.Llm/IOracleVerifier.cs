namespace Lopen.Llm;

/// <summary>
/// Dispatches cheap/fast oracle sub-agents for verification.
/// </summary>
public interface IOracleVerifier
{
    /// <summary>
    /// Verifies that work at the given scope meets acceptance criteria.
    /// </summary>
    Task<OracleVerdict> VerifyAsync(
        VerificationScope scope,
        string evidence,
        string acceptanceCriteria,
        CancellationToken cancellationToken = default);
}
