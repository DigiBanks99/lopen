using Lopen.Core.Tasks;

namespace Lopen.Core.Tests.Tasks;

public class WorkNodeStateTests
{
    [Fact]
    public void WorkNodeState_HasFourStates()
    {
        var values = Enum.GetValues<WorkNodeState>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(WorkNodeState.Pending, 0)]
    [InlineData(WorkNodeState.InProgress, 1)]
    [InlineData(WorkNodeState.Complete, 2)]
    [InlineData(WorkNodeState.Failed, 3)]
    public void WorkNodeState_HasExpectedValues(WorkNodeState state, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)state);
    }

    [Theory]
    [InlineData(WorkNodeState.Pending)]
    [InlineData(WorkNodeState.InProgress)]
    [InlineData(WorkNodeState.Complete)]
    [InlineData(WorkNodeState.Failed)]
    public void WorkNodeState_IsDefined(WorkNodeState state)
    {
        Assert.True(Enum.IsDefined(state));
    }
}
