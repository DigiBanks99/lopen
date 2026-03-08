using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Renders the stats bar after each LLM response with token delta, duration, and model.
/// </summary>
public sealed class StatsBar
{
    private readonly IAnsiConsole _console;

    public StatsBar(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    /// <summary>
    /// Renders the stats bar followed by a rule separator.
    /// </summary>
    public void Render(int tokenDelta, TimeSpan duration, string model)
    {
        var tokenStr = WorkflowOverviewBlock.FormatTokenCount(tokenDelta);
        var durationStr = ToolCallRenderer.FormatDuration(duration);

        _console.MarkupLine(
            LopenTheme.Styled($"tokens: {tokenStr} (+{tokenStr}) | duration: {durationStr} | model: {model}", LopenTheme.Muted));
        _console.Write(new Rule().RuleStyle(new Style(LopenTheme.Muted)));
    }
}
