namespace Lopen.Llm;

/// <summary>
/// Tracks oracle verification results within an SDK invocation for back-pressure enforcement.
/// <c>update_task_status(complete)</c> is rejected unless preceded by a passing verification.
/// </summary>
public interface IVerificationTracker
{
    /// <summary>Records an oracle verification result for the given scope and identifier.</summary>
    void RecordVerification(VerificationScope scope, string identifier, bool passed);

    /// <summary>Returns true if a passing verification exists for the given scope and identifier.</summary>
    bool IsVerified(VerificationScope scope, string identifier);

    /// <summary>Clears all verification state. Called at the start of each SDK invocation.</summary>
    void ResetForInvocation();
}
