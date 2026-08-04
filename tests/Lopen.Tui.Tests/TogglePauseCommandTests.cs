using Lopen.Core.Workflow;
using NSubstitute;
using RadLine;

namespace Lopen.Tui.Tests;

public class TogglePauseCommandTests
{
    [Fact]
    public void Execute_CallsToggleOnPauseController()
    {
        IPauseController pauseController = Substitute.For<IPauseController>();
        var command = new TogglePauseCommand(pauseController);

        LineBuffer buffer = new();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        LineEditorContext context = new(buffer, provider);

        command.Execute(context);

        pauseController.Received(1).Toggle();
    }

    [Fact]
    public void Execute_WithNonEmptyBuffer_StillCallsToggle()
    {
        IPauseController pauseController = Substitute.For<IPauseController>();
        var command = new TogglePauseCommand(pauseController);

        LineBuffer buffer = new();
        buffer.Insert("some input");
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        LineEditorContext context = new(buffer, provider);

        command.Execute(context);

        pauseController.Received(1).Toggle();
    }
}
