using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

public class ConsoleInputTests
{
    [Fact]
    public void DefaultConsoleInput_HasCancellationToken()
    {
        var input = new DefaultConsoleInput();
        
        input.CancellationToken.Should().NotBeNull();
        input.CancellationToken.IsCancellationRequested.Should().BeFalse();
    }
}

public class ConsoleInputWithHistoryTests
{
    [Fact]
    public void Constructor_ThrowsOnNullHistory()
    {
        var act = () => new ConsoleInputWithHistory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void History_ReturnsProvidedHistory()
    {
        var history = new CommandHistory();
        var input = new ConsoleInputWithHistory(history);

        input.History.Should().BeSameAs(history);
    }

    [Fact]
    public void CancellationToken_IsNotCancelledByDefault()
    {
        var history = new CommandHistory();
        var input = new ConsoleInputWithHistory(history);

        input.CancellationToken.IsCancellationRequested.Should().BeFalse();
    }
}
