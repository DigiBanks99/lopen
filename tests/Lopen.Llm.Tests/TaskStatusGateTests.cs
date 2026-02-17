using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public class TaskStatusGateTests
{
    private readonly VerificationTracker _tracker;
    private readonly TaskStatusGate _gate;

    public TaskStatusGateTests()
    {
        _tracker = new VerificationTracker();
        _gate = new TaskStatusGate(_tracker, NullLogger<TaskStatusGate>.Instance);
    }

    [Fact]
    public void ValidateCompletion_NoVerification_ReturnsRejected()
    {
        var result = _gate.ValidateCompletion(VerificationScope.Task, "build-ui");

        Assert.False(result.IsAllowed);
        Assert.Contains("verify_task_completion", result.RejectionReason!);
        Assert.Contains("build-ui", result.RejectionReason!);
    }

    [Fact]
    public void ValidateCompletion_WithPassingVerification_ReturnsAllowed()
    {
        _tracker.RecordVerification(VerificationScope.Task, "build-ui", passed: true);

        var result = _gate.ValidateCompletion(VerificationScope.Task, "build-ui");

        Assert.True(result.IsAllowed);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void ValidateCompletion_WithFailingVerification_ReturnsRejected()
    {
        _tracker.RecordVerification(VerificationScope.Task, "build-ui", passed: false);

        var result = _gate.ValidateCompletion(VerificationScope.Task, "build-ui");

        Assert.False(result.IsAllowed);
        Assert.Contains("no passing oracle verification", result.RejectionReason!);
    }

    [Fact]
    public void ValidateCompletion_Component_UsesComponentToolName()
    {
        var result = _gate.ValidateCompletion(VerificationScope.Component, "auth-service");

        Assert.False(result.IsAllowed);
        Assert.Contains("verify_component_completion", result.RejectionReason!);
    }

    [Fact]
    public void ValidateCompletion_Module_UsesModuleToolName()
    {
        var result = _gate.ValidateCompletion(VerificationScope.Module, "auth");

        Assert.False(result.IsAllowed);
        Assert.Contains("verify_module_completion", result.RejectionReason!);
    }

    [Fact]
    public void ValidateCompletion_DifferentScope_DoesNotCrossMatch()
    {
        _tracker.RecordVerification(VerificationScope.Task, "build-ui", passed: true);

        var result = _gate.ValidateCompletion(VerificationScope.Component, "build-ui");

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void ValidateCompletion_DifferentIdentifier_DoesNotCrossMatch()
    {
        _tracker.RecordVerification(VerificationScope.Task, "build-ui", passed: true);

        var result = _gate.ValidateCompletion(VerificationScope.Task, "build-api");

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void ValidateCompletion_AfterReset_ReturnsRejected()
    {
        _tracker.RecordVerification(VerificationScope.Task, "build-ui", passed: true);
        _tracker.ResetForInvocation();

        var result = _gate.ValidateCompletion(VerificationScope.Task, "build-ui");

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void ValidateCompletion_NullIdentifier_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _gate.ValidateCompletion(VerificationScope.Task, null!));
    }

    [Fact]
    public void ValidateCompletion_EmptyIdentifier_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _gate.ValidateCompletion(VerificationScope.Task, ""));
    }

    [Fact]
    public void Constructor_NullTracker_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TaskStatusGate(null!, NullLogger<TaskStatusGate>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TaskStatusGate(new VerificationTracker(), null!));
    }

    [Fact]
    public void TaskStatusGateResult_Allowed_HasCorrectValues()
    {
        var result = TaskStatusGateResult.Allowed();

        Assert.True(result.IsAllowed);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void TaskStatusGateResult_Rejected_HasCorrectValues()
    {
        var result = TaskStatusGateResult.Rejected("test reason");

        Assert.False(result.IsAllowed);
        Assert.Equal("test reason", result.RejectionReason);
    }
}
