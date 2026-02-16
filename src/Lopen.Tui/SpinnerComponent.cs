namespace Lopen.Tui;

/// <summary>
/// Data model for async feedback spinner.
/// </summary>
public sealed record SpinnerData
{
    /// <summary>Message to display alongside the spinner.</summary>
    public required string Message { get; init; }

    /// <summary>Current frame index of the spinner animation.</summary>
    public int Frame { get; init; }

    /// <summary>Optional progress percentage (0–100). -1 means indeterminate.</summary>
    public int ProgressPercent { get; init; } = -1;
}

/// <summary>
/// Renders a spinner with optional progress percentage for async feedback.
/// </summary>
public sealed class SpinnerComponent : IPreviewableComponent
{
    /// <summary>Spinner animation frames.</summary>
    internal static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    public IReadOnlyList<string> GetPreviewStates() => ["populated", "loading"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "loading" => new SpinnerData { Message = "Analyzing codebase..." },
            _ => new SpinnerData { Message = "Analyzing codebase...", ProgressPercent = 45 },
        };
        return [Render(data, width)];
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new SpinnerData { Message = "Analyzing codebase...", ProgressPercent = 45 };
        return [Render(data, width)];
    }

    public string Name => "Spinner";
    public string Description => "Async feedback spinner with optional progress percentage";

    /// <summary>
    /// Renders a single-line spinner.
    /// </summary>
    public string Render(SpinnerData data, int width)
    {
        if (width <= 0)
            return string.Empty;

        var frame = Frames[data.Frame % Frames.Length];
        var progress = data.ProgressPercent >= 0
            ? $" {data.ProgressPercent}%"
            : string.Empty;

        var text = $"{frame} {data.Message}{progress}";
        return text.Length >= width ? text[..width] : text.PadRight(width);
    }
}
