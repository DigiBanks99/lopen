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

    [Fact]
    public void ErrorWithSuggestion_ShowsErrorAndSuggestion()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.ErrorWithSuggestion("Auth failed", "lopen auth login");

        console.Output.ShouldContain("Auth failed");
        console.Output.ShouldContain("lopen auth login");
    }

    [Fact]
    public void ErrorPanel_ShowsTitleAndMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.ErrorPanel("Config", "File not found", "Check path", "Create config");

        // Panel header includes "Error: " prefix, so check for key content
        console.Output.ShouldContain("File not found");
        console.Output.ShouldContain("Check path");
    }

    [Fact]
    public void CommandNotFoundError_ShowsCommandAndSuggestions()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.CommandNotFoundError("chatr", "chat", "repl");

        console.Output.ShouldContain("chatr");
        console.Output.ShouldContain("chat");
        console.Output.ShouldContain("Did you mean");
    }

    [Fact]
    public void ValidationError_ShowsInputAndOptions()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.ValidationError("--model xyz", "Invalid model", "gpt-4", "claude");

        console.Output.ShouldContain("Invalid model");
        console.Output.ShouldContain("--model xyz");
        console.Output.ShouldContain("gpt-4");
    }

    [Fact]
    public void Table_RendersItems()
    {
        var console = new TestConsole().Width(120);
        var output = new ConsoleOutput(console);
        var items = new[] { ("Alice", 25), ("Bob", 30) };
        var config = new TableConfig<(string Name, int Age)>
        {
            Columns = new List<TableColumn<(string Name, int Age)>>
            {
                new() { Header = "Name", Selector = x => x.Name },
                new() { Header = "Age", Selector = x => x.Age.ToString() }
            },
            ShowRowCount = false
        };

        output.Table(items, config);

        console.Output.ShouldContain("Alice");
        console.Output.ShouldContain("Bob");
    }

    [Fact]
    public void Metadata_RendersKeyValuePairs()
    {
        var console = new TestConsole().Width(120);
        var output = new ConsoleOutput(console);
        var data = new Dictionary<string, string>
        {
            ["Status"] = "Ready",
            ["Version"] = "1.0"
        };

        output.Metadata(data, "App Info");

        console.Output.ShouldContain("App Info");
        console.Output.ShouldContain("Status");
        console.Output.ShouldContain("Ready");
    }

    [Fact]
    public void Progress_WritesProgressSymbol()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Progress("Loading data");

        console.Output.ShouldContain("Loading data");
        // Symbol will be ⏳ or ... depending on unicode support
    }

    [Fact]
    public void New_WritesNewSymbol()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.New("Feature unlocked");

        console.Output.ShouldContain("Feature unlocked");
    }

    [Fact]
    public void Launch_WritesLaunchSymbol()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Launch("Starting server");

        console.Output.ShouldContain("Starting server");
    }

    [Fact]
    public void Fast_WritesFastSymbol()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Fast("Quick operation");

        console.Output.ShouldContain("Quick operation");
    }

    [Fact]
    public void Tip_WritesTipSymbol()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);

        output.Tip("Try using --help");

        console.Output.ShouldContain("Try using --help");
    }
}
