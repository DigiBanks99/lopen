namespace Lopen.Llm;

/// <summary>
/// Tracks oracle verification results within an SDK invocation.
/// Used to enforce back-pressure: <c>update_task_status(complete)</c> is rejected
/// unless <see cref="IsVerified"/> returns true for the corresponding scope.
/// </summary>
internal sealed class VerificationTracker : IVerificationTracker
{
    private readonly Dictionary<(VerificationScope Scope, string Identifier), bool> _results = [];

    public void RecordVerification(VerificationScope scope, string identifier, bool passed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        _results[(scope, identifier)] = passed;
    }

    public bool IsVerified(VerificationScope scope, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return _results.TryGetValue((scope, identifier), out var passed) && passed;
    }

    public void ResetForInvocation()
    {
        _results.Clear();
    }
}
