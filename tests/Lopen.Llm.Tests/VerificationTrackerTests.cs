namespace Lopen.Llm.Tests;

public sealed class VerificationTrackerTests
{
    private readonly VerificationTracker _tracker = new();

    [Fact]
    public void IsVerified_NoRecords_ReturnsFalse()
    {
        Assert.False(_tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public void RecordVerification_Passing_MakesIsVerifiedTrue()
    {
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: true);

        Assert.True(_tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public void RecordVerification_Failing_KeepsIsVerifiedFalse()
    {
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: false);

        Assert.False(_tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public void RecordVerification_FailThenPass_IsVerified()
    {
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: false);
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: true);

        Assert.True(_tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public void RecordVerification_PassThenFail_IsNotVerified()
    {
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: true);
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: false);

        Assert.False(_tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public void IsVerified_ScopeIsolation_DifferentScopes()
    {
        _tracker.RecordVerification(VerificationScope.Task, "item-1", passed: true);

        Assert.True(_tracker.IsVerified(VerificationScope.Task, "item-1"));
        Assert.False(_tracker.IsVerified(VerificationScope.Component, "item-1"));
        Assert.False(_tracker.IsVerified(VerificationScope.Module, "item-1"));
    }

    [Fact]
    public void IsVerified_IdentifierIsolation_DifferentIdentifiers()
    {
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: true);

        Assert.True(_tracker.IsVerified(VerificationScope.Task, "task-1"));
        Assert.False(_tracker.IsVerified(VerificationScope.Task, "task-2"));
    }

    [Fact]
    public void ResetForInvocation_ClearsAllState()
    {
        _tracker.RecordVerification(VerificationScope.Task, "task-1", passed: true);
        _tracker.RecordVerification(VerificationScope.Component, "comp-1", passed: true);

        _tracker.ResetForInvocation();

        Assert.False(_tracker.IsVerified(VerificationScope.Task, "task-1"));
        Assert.False(_tracker.IsVerified(VerificationScope.Component, "comp-1"));
    }

    [Fact]
    public void RecordVerification_NullIdentifier_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _tracker.RecordVerification(VerificationScope.Task, null!, true));
    }

    [Fact]
    public void RecordVerification_EmptyIdentifier_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => _tracker.RecordVerification(VerificationScope.Task, "", true));
    }

    [Fact]
    public void IsVerified_NullIdentifier_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _tracker.IsVerified(VerificationScope.Task, null!));
    }

    [Fact]
    public void IsVerified_EmptyIdentifier_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => _tracker.IsVerified(VerificationScope.Task, ""));
    }

    [Fact]
    public void MultipleScopes_IndependentTracking()
    {
        _tracker.RecordVerification(VerificationScope.Task, "t1", passed: true);
        _tracker.RecordVerification(VerificationScope.Component, "c1", passed: true);
        _tracker.RecordVerification(VerificationScope.Module, "m1", passed: false);

        Assert.True(_tracker.IsVerified(VerificationScope.Task, "t1"));
        Assert.True(_tracker.IsVerified(VerificationScope.Component, "c1"));
        Assert.False(_tracker.IsVerified(VerificationScope.Module, "m1"));
    }
}
