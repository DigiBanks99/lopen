namespace Lopen.Core.BackPressure;

/// <summary>
/// Result of a guardrail evaluation. Represents pass, warn, or block outcomes.
/// </summary>
public abstract record GuardrailResult
{
    private GuardrailResult() { }

    /// <summary>Guardrail passed with no issues.</summary>
    public sealed record Pass() : GuardrailResult;

    /// <summary>Guardrail issued a warning but does not block progress.</summary>
    public sealed record Warn(string Message) : GuardrailResult;

    /// <summary>Guardrail blocks progress and requires intervention.</summary>
    public sealed record Block(string Message) : GuardrailResult;
}
