namespace Lopen.Core;

/// <summary>
/// Result of a verification check.
/// </summary>
public record VerificationResult
{
    /// <summary>
    /// Whether the verification passed.
    /// </summary>
    public bool Complete { get; init; }

    /// <summary>
    /// Whether tests exist and pass.
    /// </summary>
    public bool TestsPass { get; init; }

    /// <summary>
    /// Whether documentation exists (Divio model).
    /// </summary>
    public bool DocumentationExists { get; init; }

    /// <summary>
    /// Whether the build succeeds.
    /// </summary>
    public bool BuildSucceeds { get; init; }

    /// <summary>
    /// Whether the requirement code is valid.
    /// </summary>
    public bool RequirementValid { get; init; }

    /// <summary>
    /// List of issues found during verification.
    /// </summary>
    public IReadOnlyList<string> Issues { get; init; } = [];

    /// <summary>
    /// Creates a failed verification result with a single issue.
    /// </summary>
    public static VerificationResult Failed(string issue) =>
        new() { Complete = false, Issues = [issue] };

    /// <summary>
    /// Creates a passed verification result.
    /// </summary>
    public static VerificationResult Passed() =>
        new()
        {
            Complete = true,
            TestsPass = true,
            DocumentationExists = true,
            BuildSucceeds = true,
            RequirementValid = true,
            Issues = []
        };
}
