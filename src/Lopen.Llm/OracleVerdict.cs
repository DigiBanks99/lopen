namespace Lopen.Llm;

/// <summary>
/// Result of an oracle verification check.
/// </summary>
public sealed record OracleVerdict(
    bool Passed,
    IReadOnlyList<string> Gaps,
    VerificationScope Scope);
