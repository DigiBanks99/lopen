using NSubstitute;
using RadLine;

namespace Lopen.Tui.Tests;

public class ExitOnEmptyCommandTests
{
    [Fact]
    public void ExitRequestedFlag_DefaultsToFalse()
    {
        LopenLineEditor.ExitRequestedFlag flag = new();
        Assert.False(flag.IsSet);
    }

    [Fact]
    public void ExitRequestedFlag_CanBeSetToTrue()
    {
        LopenLineEditor.ExitRequestedFlag flag = new();
        flag.IsSet = true;
        Assert.True(flag.IsSet);
    }

    [Fact]
    public void Execute_WhenBufferEmpty_SetsExitFlag()
    {
        LopenLineEditor.ExitRequestedFlag flag = new();
        ExitOnEmptyCommand command = new(flag);

        LineBuffer buffer = new();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        LineEditorContext context = new(buffer, provider);

        command.Execute(context);

        Assert.True(flag.IsSet);
    }

    [Fact]
    public void Execute_WhenBufferHasContent_DoesNotSetExitFlag()
    {
        LopenLineEditor.ExitRequestedFlag flag = new();
        ExitOnEmptyCommand command = new(flag);

        LineBuffer buffer = new();
        buffer.Insert('x');
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        LineEditorContext context = new(buffer, provider);

        command.Execute(context);

        Assert.False(flag.IsSet);
    }
}
