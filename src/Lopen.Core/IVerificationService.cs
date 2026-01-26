namespace Lopen.Core;

/// <summary>
/// Service for verifying task completion and quality.
/// Uses a dedicated sub-agent via Copilot SDK.
/// </summary>
public interface IVerificationService
{
    /// <summary>
    /// Verify that a job is complete with tests, docs, and valid requirements.
    /// </summary>
    /// <param name="jobId">The job identifier (e.g., JTBD-025)</param>
    /// <param name="requirementCode">The requirement code (e.g., REQ-036)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Verification result with pass/fail status and issues</returns>
    Task<VerificationResult> VerifyJobCompletionAsync(
        string jobId,
        string requirementCode,
        CancellationToken ct = default);

    /// <summary>
    /// Verify that tests exist and pass for a requirement.
    /// </summary>
    Task<VerificationResult> VerifyTestsAsync(string requirementCode, CancellationToken ct = default);

    /// <summary>
    /// Verify that documentation exists for a requirement.
    /// </summary>
    Task<VerificationResult> VerifyDocumentationAsync(string requirementCode, CancellationToken ct = default);

    /// <summary>
    /// Verify that the build succeeds.
    /// </summary>
    Task<VerificationResult> VerifyBuildAsync(CancellationToken ct = default);

    /// <summary>
    /// Verify that a requirement code exists in SPECIFICATION.md files.
    /// </summary>
    Task<VerificationResult> VerifyRequirementCodeAsync(string requirementCode, CancellationToken ct = default);
}
