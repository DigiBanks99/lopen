using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public sealed class FailureHandlerTests
{
    private readonly FailureHandler _handler =
        new(NullLogger<FailureHandler>.Instance, failureThreshold: 3);

    [Fact]
    public void RecordFailure_FirstTime_ReturnsSelfCorrect()
    {
        var result = _handler.RecordFailure("task-1", "Build failed");

        Assert.Equal(FailureSeverity.TaskFailure, result.Severity);
        Assert.Equal(FailureAction.SelfCorrect, result.Action);
        Assert.Equal("task-1", result.TaskId);
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_SecondTime_StillSelfCorrect()
    {
        _handler.RecordFailure("task-1", "fail 1");
        var result = _handler.RecordFailure("task-1", "fail 2");

        Assert.Equal(FailureSeverity.TaskFailure, result.Severity);
        Assert.Equal(FailureAction.SelfCorrect, result.Action);
        Assert.Equal(2, result.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_ThirdTime_EscalatesToPromptUser()
    {
        _handler.RecordFailure("task-1", "fail 1");
        _handler.RecordFailure("task-1", "fail 2");
        var result = _handler.RecordFailure("task-1", "fail 3");

        Assert.Equal(FailureSeverity.RepeatedFailure, result.Severity);
        Assert.Equal(FailureAction.PromptUser, result.Action);
        Assert.Equal(3, result.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_BeyondThreshold_StillPromptUser()
    {
        _handler.RecordFailure("task-1", "fail 1");
        _handler.RecordFailure("task-1", "fail 2");
        _handler.RecordFailure("task-1", "fail 3");
        var result = _handler.RecordFailure("task-1", "fail 4");

        Assert.Equal(FailureSeverity.RepeatedFailure, result.Severity);
        Assert.Equal(4, result.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_DifferentTasks_IndependentCounts()
    {
        _handler.RecordFailure("task-1", "fail");
        _handler.RecordFailure("task-1", "fail");
        var result2 = _handler.RecordFailure("task-2", "fail");

        Assert.Equal(1, result2.ConsecutiveFailures);
    }

    [Fact]
    public void ResetFailureCount_ResetsCounter()
    {
        _handler.RecordFailure("task-1", "fail");
        _handler.RecordFailure("task-1", "fail");
        _handler.ResetFailureCount("task-1");

        var result = _handler.RecordFailure("task-1", "fail again");
        Assert.Equal(1, result.ConsecutiveFailures);
    }

    [Fact]
    public void GetFailureCount_NoFailures_ReturnsZero()
    {
        Assert.Equal(0, _handler.GetFailureCount("task-1"));
    }

    [Fact]
    public void GetFailureCount_AfterFailures_ReturnsCount()
    {
        _handler.RecordFailure("task-1", "fail");
        _handler.RecordFailure("task-1", "fail");

        Assert.Equal(2, _handler.GetFailureCount("task-1"));
    }

    [Fact]
    public void RecordCriticalError_ReturnsBlock()
    {
        var result = _handler.RecordCriticalError("Disk full");

        Assert.Equal(FailureSeverity.Critical, result.Severity);
        Assert.Equal(FailureAction.Block, result.Action);
        Assert.Equal("Disk full", result.Message);
    }

    [Fact]
    public void RecordWarning_ReturnsSelfCorrect()
    {
        var result = _handler.RecordWarning("Minor issue");

        Assert.Equal(FailureSeverity.Warning, result.Severity);
        Assert.Equal(FailureAction.SelfCorrect, result.Action);
    }

    [Fact]
    public void RecordFailure_NullTaskId_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _handler.RecordFailure(null!, "error"));
    }

    [Fact]
    public void RecordFailure_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _handler.RecordFailure("task", null!));
    }

    [Fact]
    public void RecordCriticalError_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _handler.RecordCriticalError(null!));
    }

    [Fact]
    public void RecordWarning_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _handler.RecordWarning(null!));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FailureHandler(null!));
    }

    [Fact]
    public void Constructor_ZeroThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FailureHandler(NullLogger<FailureHandler>.Instance, 0));
    }

    [Fact]
    public void Constructor_NegativeThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FailureHandler(NullLogger<FailureHandler>.Instance, -1));
    }

    [Fact]
    public void Constructor_CustomThreshold_RespectedInFailures()
    {
        var handler = new FailureHandler(NullLogger<FailureHandler>.Instance, failureThreshold: 2);
        handler.RecordFailure("t", "f");
        var result = handler.RecordFailure("t", "f");

        Assert.Equal(FailureSeverity.RepeatedFailure, result.Severity);
        Assert.Equal(FailureAction.PromptUser, result.Action);
    }
}
