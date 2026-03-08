using Spectre.Console;

namespace Lopen.Tui.Tests;

public class StatsBarTests
{
    [Fact]
    public void Render_DoesNotThrow()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var statsBar = new StatsBar(console);

        statsBar.Render(142, TimeSpan.FromSeconds(1.2), "gpt-4.1");
    }

    [Fact]
    public void Render_LargeTokenCount_DoesNotThrow()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var statsBar = new StatsBar(console);

        statsBar.Render(50000, TimeSpan.FromSeconds(10.5), "claude-sonnet-4");
    }
}
