using Shouldly;
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

        console.Output.ShouldContain("✓");
        console.Output.ShouldContain("Operation completed");
    }

    [Fact]
    public void Error_WritesRedMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Error("Something failed");

        console.Output.ShouldContain("✗");
        console.Output.ShouldContain("Something failed");
    }

    [Fact]
    public void Warning_WritesYellowMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Warning("Be careful");

        console.Output.ShouldContain("!");
        console.Output.ShouldContain("Be careful");
    }

    [Fact]
    public void Info_WritesBlueMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Info("For your information");

        console.Output.ShouldContain("ℹ");
        console.Output.ShouldContain("For your information");
    }

    [Fact]
    public void KeyValue_WritesFormattedPair()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.KeyValue("Status", "Ready");

        console.Output.ShouldContain("Status");
        console.Output.ShouldContain("Ready");
    }

    [Fact]
    public void WriteLine_WritesPlainText()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.WriteLine("Plain text");

        console.Output.ShouldContain("Plain text");
    }

    [Fact]
    public async Task ShowStatusAsync_ExecutesOperation()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var executed = false;

        await output.ShowStatusAsync("Working...", async () =>
        {
            await Task.Delay(1);
            executed = true;
        });

        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task ShowStatusAsync_ReturnsResult()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        var result = await output.ShowStatusAsync("Computing...", () => Task.FromResult(42));

        result.ShouldBe(42);
    }

    [Fact]
    public async Task ShowStatusAsync_WithSpinnerType_ExecutesOperation()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        var result = await output.ShowStatusAsync(
            "Processing...",
            () => Task.FromResult("done"),
            SpinnerType.Arc);

        result.ShouldBe("done");
    }
}
