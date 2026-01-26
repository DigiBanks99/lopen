namespace Lopen.Core.Testing;

/// <summary>
/// Validation result containing success status and matched pattern.
/// </summary>
public sealed record ValidationResult(bool IsValid, string? MatchedPattern = null);

/// <summary>
/// Strategy interface for validating test responses.
/// </summary>
public interface ITestValidator
{
    /// <summary>
    /// Validate a response against expected patterns.
    /// </summary>
    /// <param name="response">The response text to validate.</param>
    /// <returns>Validation result with success status and matched pattern.</returns>
    ValidationResult Validate(string response);
}
