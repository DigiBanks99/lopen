namespace Lopen.Core;

/// <summary>
/// Mock verification service for testing.
/// </summary>
public class MockVerificationService : IVerificationService
{
    private VerificationResult _defaultResult = VerificationResult.Passed();

    /// <summary>
    /// Number of times VerifyJobCompletionAsync was called.
    /// </summary>
    public int JobVerificationCount { get; private set; }

    /// <summary>
    /// Number of times VerifyTestsAsync was called.
    /// </summary>
    public int TestVerificationCount { get; private set; }

    /// <summary>
    /// Number of times VerifyDocumentationAsync was called.
    /// </summary>
    public int DocVerificationCount { get; private set; }

    /// <summary>
    /// Number of times VerifyBuildAsync was called.
    /// </summary>
    public int BuildVerificationCount { get; private set; }

    /// <summary>
    /// Number of times VerifyRequirementCodeAsync was called.
    /// </summary>
    public int RequirementVerificationCount { get; private set; }

    /// <summary>
    /// Last job ID verified.
    /// </summary>
    public string? LastJobId { get; private set; }

    /// <summary>
    /// Last requirement code verified.
    /// </summary>
    public string? LastRequirementCode { get; private set; }

    /// <summary>
    /// Configure the mock to return a specific result.
    /// </summary>
    public void SetResult(VerificationResult result)
    {
        _defaultResult = result;
    }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyJobCompletionAsync(
        string jobId,
        string requirementCode,
        CancellationToken ct = default)
    {
        JobVerificationCount++;
        LastJobId = jobId;
        LastRequirementCode = requirementCode;
        return Task.FromResult(_defaultResult);
    }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyTestsAsync(string requirementCode, CancellationToken ct = default)
    {
        TestVerificationCount++;
        LastRequirementCode = requirementCode;
        return Task.FromResult(_defaultResult);
    }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyDocumentationAsync(string requirementCode, CancellationToken ct = default)
    {
        DocVerificationCount++;
        LastRequirementCode = requirementCode;
        return Task.FromResult(_defaultResult);
    }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyBuildAsync(CancellationToken ct = default)
    {
        BuildVerificationCount++;
        return Task.FromResult(_defaultResult);
    }

    /// <inheritdoc />
    public Task<VerificationResult> VerifyRequirementCodeAsync(string requirementCode, CancellationToken ct = default)
    {
        RequirementVerificationCount++;
        LastRequirementCode = requirementCode;
        return Task.FromResult(_defaultResult);
    }

    /// <summary>
    /// Reset all counters and results.
    /// </summary>
    public void Reset()
    {
        JobVerificationCount = 0;
        TestVerificationCount = 0;
        DocVerificationCount = 0;
        BuildVerificationCount = 0;
        RequirementVerificationCount = 0;
        LastJobId = null;
        LastRequirementCode = null;
        _defaultResult = VerificationResult.Passed();
    }
}
