using Lopen.Core.Tasks;

namespace Lopen.Core.Tests.Tasks;

public class InvalidStateTransitionExceptionTests
{
    [Fact]
    public void Constructor_SetsDefaultMessage()
    {
        var ex = new InvalidStateTransitionException(WorkNodeState.Pending, WorkNodeState.Complete);

        Assert.Equal("Cannot transition from Pending to Complete.", ex.Message);
        Assert.Equal(WorkNodeState.Pending, ex.CurrentState);
        Assert.Equal(WorkNodeState.Complete, ex.TargetState);
    }

    [Fact]
    public void Constructor_WithCustomMessage_SetsMessage()
    {
        var ex = new InvalidStateTransitionException(
            "Custom message", WorkNodeState.Failed, WorkNodeState.Complete);

        Assert.Equal("Custom message", ex.Message);
        Assert.Equal(WorkNodeState.Failed, ex.CurrentState);
        Assert.Equal(WorkNodeState.Complete, ex.TargetState);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsProperties()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new InvalidStateTransitionException(
            "msg", WorkNodeState.Pending, WorkNodeState.Failed, inner);

        Assert.Equal("msg", ex.Message);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(WorkNodeState.Pending, ex.CurrentState);
        Assert.Equal(WorkNodeState.Failed, ex.TargetState);
    }

    [Fact]
    public void InheritsFromException()
    {
        var ex = new InvalidStateTransitionException(WorkNodeState.Pending, WorkNodeState.Complete);
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
