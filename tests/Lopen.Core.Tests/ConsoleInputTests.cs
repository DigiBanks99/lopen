using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class ConsoleInputTests
{
    [Fact]
    public void DefaultConsoleInput_HasCancellationToken()
    {
        var input = new DefaultConsoleInput();
        
        // CancellationToken is a struct, so just verify it's usable
        input.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    }
}

public class ConsoleInputWithHistoryTests
{
    [Fact]
    public void Constructor_ThrowsOnNullHistory()
    {
        var act = () => new ConsoleInputWithHistory(null!);

        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void History_ReturnsProvidedHistory()
    {
        var history = new CommandHistory();
        var input = new ConsoleInputWithHistory(history);

        input.History.ShouldBeSameAs(history);
    }

    [Fact]
    public void CancellationToken_IsNotCancelledByDefault()
    {
        var history = new CommandHistory();
        var input = new ConsoleInputWithHistory(history);

        input.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    }
}
