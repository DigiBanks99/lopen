using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Detects and provides terminal capabilities for adaptive TUI rendering.
/// Uses Spectre.Console detection with Console fallbacks.
/// </summary>
public class TerminalCapabilities : ITerminalCapabilities
{
    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public ColorSystem ColorSystem { get; }

    /// <inheritdoc />
    public bool SupportsUnicode { get; }

    /// <inheritdoc />
    public bool IsInteractive { get; }

    /// <inheritdoc />
    public bool IsNoColorSet { get; }

    /// <inheritdoc />
    public bool SupportsColor => !IsNoColorSet && ColorSystem != ColorSystem.NoColors;

    /// <inheritdoc />
    public bool IsWideTerminal => Width >= 120;

    /// <inheritdoc />
    public bool IsNarrowTerminal => Width < 60;

    /// <inheritdoc />
    public bool Supports256Colors => ColorSystem is ColorSystem.EightBit or ColorSystem.TrueColor;

    /// <inheritdoc />
    public bool SupportsTrueColor => ColorSystem == ColorSystem.TrueColor;

    private TerminalCapabilities(
        int width,
        int height,
        ColorSystem colorSystem,
        bool supportsUnicode,
        bool isInteractive,
        bool isNoColorSet)
    {
        Width = width;
        Height = height;
        ColorSystem = colorSystem;
        SupportsUnicode = supportsUnicode;
        IsInteractive = isInteractive;
        IsNoColorSet = isNoColorSet;
    }

    /// <summary>
    /// Detects the current terminal capabilities.
    /// </summary>
    /// <returns>A new instance with detected capabilities.</returns>
    public static ITerminalCapabilities Detect()
    {
        return Detect(AnsiConsole.Console);
    }

    /// <summary>
    /// Detects terminal capabilities using the specified console.
    /// </summary>
    /// <param name="console">The console to detect capabilities from.</param>
    /// <returns>A new instance with detected capabilities.</returns>
    public static ITerminalCapabilities Detect(IAnsiConsole console)
    {
        // Priority 1: Check NO_COLOR environment variable
        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var isNoColorSet = !string.IsNullOrEmpty(noColor);

        // Priority 2: Detect console dimensions with fallback
        int width, height;
        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
        }
        catch
        {
            // Fallback for non-interactive or piped output
            width = 80;
            height = 24;
        }

        // Priority 3: Use Spectre.Console detection
        var colorSystem = console.Profile.Capabilities.ColorSystem;
        var supportsUnicode = console.Profile.Capabilities.Unicode;

        // Priority 4: Check for interactive mode
        var isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected;

        return new TerminalCapabilities(
            width,
            height,
            colorSystem,
            supportsUnicode,
            isInteractive,
            isNoColorSet
        );
    }
}
