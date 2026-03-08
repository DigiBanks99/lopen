using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Renders the stats bar after each LLM response with token delta, duration, and model.
/// </summary>
public sealed class StatsBar(IAnsiConsole console)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));

    /// <summary>
    /// Renders the stats bar followed by a rule separator.
    /// </summary>
    public void Render(int tokenDelta, TimeSpan duration, string model)
    {
        string tokenStr = WorkflowOverviewBlock.FormatTokenCount(tokenDelta);
        string durationStr = ToolCallRenderer.FormatDuration(duration);

        _console.MarkupLine(
            LopenTheme.Styled($"tokens: {tokenStr} (+{tokenStr}) | duration: {durationStr} | model: {model}", LopenTheme.Muted));
        _console.Write(new Rule().RuleStyle(new Style(LopenTheme.Muted)));
    }
}
