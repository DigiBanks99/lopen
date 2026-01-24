using FluentAssertions;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Core.Tests;

public class ConsoleOutputTests
{
    [Fact]
    public void Success_WritesGreenMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Success("Operation completed");

        console.Output.Should().Contain("✓");
        console.Output.Should().Contain("Operation completed");
    }

    [Fact]
    public void Error_WritesRedMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Error("Something failed");

        console.Output.Should().Contain("✗");
        console.Output.Should().Contain("Something failed");
    }

    [Fact]
    public void Warning_WritesYellowMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Warning("Be careful");

        console.Output.Should().Contain("!");
        console.Output.Should().Contain("Be careful");
    }

    [Fact]
    public void Info_WritesBlueMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Info("For your information");

        console.Output.Should().Contain("ℹ");
        console.Output.Should().Contain("For your information");
    }

    [Fact]
    public void KeyValue_WritesFormattedPair()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.KeyValue("Status", "Ready");

        console.Output.Should().Contain("Status");
        console.Output.Should().Contain("Ready");
    }

    [Fact]
    public void WriteLine_WritesPlainText()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.WriteLine("Plain text");

        console.Output.Should().Contain("Plain text");
    }
}
