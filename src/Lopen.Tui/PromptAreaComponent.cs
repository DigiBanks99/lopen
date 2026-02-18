namespace Lopen.Tui;

/// <summary>
/// Renders the bottom prompt area with multi-line input and keyboard hints.
/// </summary>
public sealed class PromptAreaComponent : IPreviewableComponent
{
    /// <summary>Default keyboard hints shown below the input.</summary>
    internal static readonly string[] DefaultHints =
    [
        "Enter: Submit",
        "Alt+Enter: New line",
        "1-9: View resource",
        "Tab: Focus panel",
        "Ctrl+P: Pause",
    ];

    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated", "error", "loading"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new PromptAreaData(),
            "error" => new PromptAreaData
            {
                Text = "Error: command not recognized",
                CustomHints = ["Enter: Retry", "Ctrl+C: Cancel"],
            },
            "loading" => new PromptAreaData
            {
                Spinner = new SpinnerData { Message = "Analyzing..." },
            },
            _ => new PromptAreaData
            {
                Text = "lopen build --module auth",
                CursorPosition = 10,
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new PromptAreaData
        {
            Text = "lopen build --module auth",
            CursorPosition = 10,
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string Name => "PromptArea";
    public string Description => "Multi-line prompt area with keyboard hints at bottom";

    /// <summary>
    /// Renders the prompt area as an array of plain-text lines sized to the given region.
    /// Layout: input line(s) at top, keyboard hints at bottom.
    /// </summary>
    public string[] Render(PromptAreaData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        var width = region.Width;

        // Input area: region.Height - 1 for hints row
        var inputHeight = Math.Max(1, region.Height - 1);

        // Spinner replaces input when active
        if (data.Spinner is not null)
        {
            var spinnerComponent = new SpinnerComponent();
            var spinnerLine = spinnerComponent.Render(data.Spinner, width);
            lines.Add(PadToWidth(spinnerLine, width));
            for (int i = 1; i < inputHeight; i++)
                lines.Add(PadToWidth(string.Empty, width));
        }
        else
        {
            // Build input lines
            var inputText = string.IsNullOrEmpty(data.Text)
                ? $"> {data.Placeholder}"
                : $"> {data.Text}";

            var inputLines = WrapText(inputText, width);

            // Take up to inputHeight lines
            for (int i = 0; i < inputHeight; i++)
            {
                lines.Add(i < inputLines.Count
                    ? PadToWidth(inputLines[i], width)
                    : PadToWidth(string.Empty, width));
            }
        }

        // Keyboard hints row (always last if region allows)
        if (region.Height > 1)
        {
            var hints = data.CustomHints ?? DefaultHints;
            var hintsLine = BuildHintsLine(hints, data.IsPaused);
            lines.Add(PadToWidth(hintsLine, width));
        }

        // Ensure exact height
        while (lines.Count < region.Height)
            lines.Add(PadToWidth(string.Empty, width));

        return lines.GetRange(0, region.Height).ToArray();
    }

    /// <summary>
    /// Builds the keyboard hints line with │ separators.
    /// </summary>
    internal static string BuildHintsLine(IReadOnlyList<string> hints, bool isPaused)
    {
        var activeHints = new List<string>(hints);

        if (isPaused)
        {
            // Replace Ctrl+P hint with resume
            for (int i = 0; i < activeHints.Count; i++)
            {
                if (activeHints[i].Contains("Ctrl+P"))
                {
                    activeHints[i] = "Ctrl+P: Resume";
                    break;
                }
            }
        }

        return string.Join(" │ ", activeHints);
    }

    /// <summary>
    /// Wraps text to fit within the specified width.
    /// </summary>
    internal static List<string> WrapText(string text, int width)
    {
        if (width <= 0)
            return [string.Empty];

        var result = new List<string>();
        var textLines = text.Split('\n');

        foreach (var line in textLines)
        {
            if (line.Length <= width)
            {
                result.Add(line);
            }
            else
            {
                for (int i = 0; i < line.Length; i += width)
                {
                    var chunk = line.Substring(i, Math.Min(width, line.Length - i));
                    result.Add(chunk);
                }
            }
        }

        return result;
    }

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width
            ? text[..width]
            : text.PadRight(width);
    }
}
