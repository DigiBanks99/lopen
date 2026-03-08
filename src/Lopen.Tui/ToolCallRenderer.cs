using Spectre.Console;
using System.Diagnostics;

namespace Lopen.Tui;

/// <summary>
/// Renders tool call execution with spinner, result, and timing.
/// </summary>
public sealed class ToolCallRenderer(IAnsiConsole console)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));

    /// <summary>
    /// Renders a tool call with a spinner while executing, then shows result.
    /// </summary>
    public async Task<T> RenderToolCallAsync<T>(string toolName, Func<Task<T>> execute)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            T? result = await execute();
            sw.Stop();
            RenderSuccess(toolName, sw.Elapsed);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RenderFailure(toolName, ex.Message, sw.Elapsed);
            throw;
        }
    }

    /// <summary>
    /// Renders a successful tool call completion.
    /// </summary>
    public void RenderSuccess(string toolName, TimeSpan duration, string? output = null)
    {
        _console.MarkupLine(
            $"  {LopenTheme.Styled(LopenTheme.ToolSuccess, LopenTheme.Success)} {LopenTheme.Styled(toolName, LopenTheme.Accent)} {LopenTheme.Dim($"({FormatDuration(duration)})", LopenTheme.Muted)}");

        if (output is not null && !string.IsNullOrWhiteSpace(output))
        {
            Panel panel = new Panel(Markup.Escape(TruncateOutput(output)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(LopenTheme.Muted),
                Padding = new Padding(1, 0),
            };
            _console.Write(new Padder(panel).PadLeft(2));
        }
    }

    /// <summary>
    /// Renders a failed tool call.
    /// </summary>
    public void RenderFailure(string toolName, string reason, TimeSpan duration)
    {
        _console.MarkupLine(
            $"  {LopenTheme.Styled(LopenTheme.ToolFailure, LopenTheme.Error)} {LopenTheme.Styled(toolName, LopenTheme.Error)} {LopenTheme.Dim($"(failed: {Markup.Escape(reason)})", LopenTheme.Muted)}");
    }

    internal static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:0.#}s"
            : $"{duration.TotalMilliseconds:0}ms";
    }

    private static string TruncateOutput(string output, int maxLines = 20)
    {
        string[] lines = output.Split('\n');
        if (lines.Length <= maxLines)
            return output;

        return string.Join("\n", lines.Take(maxLines)) + $"\n... ({lines.Length - maxLines} more lines)";
    }
}
