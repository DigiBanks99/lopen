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
