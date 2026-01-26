using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Provides access to terminal capabilities for adaptive TUI rendering.
/// </summary>
public interface ITerminalCapabilities
{
    /// <summary>
    /// Terminal width in characters.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Terminal height in characters.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// The color system supported by the terminal.
    /// </summary>
    ColorSystem ColorSystem { get; }

    /// <summary>
    /// Whether the terminal supports Unicode characters.
    /// </summary>
    bool SupportsUnicode { get; }

    /// <summary>
    /// Whether the terminal is interactive (not redirected).
    /// </summary>
    bool IsInteractive { get; }

    /// <summary>
    /// Whether the NO_COLOR environment variable is set.
    /// </summary>
    bool IsNoColorSet { get; }

    /// <summary>
    /// Whether the terminal supports any color output.
    /// </summary>
    bool SupportsColor { get; }

    /// <summary>
    /// Whether the terminal is wide enough for full layouts (≥120 chars).
    /// </summary>
    bool IsWideTerminal { get; }

    /// <summary>
    /// Whether the terminal is too narrow for complex layouts (&lt;60 chars).
    /// </summary>
    bool IsNarrowTerminal { get; }

    /// <summary>
    /// Whether the terminal supports 256 colors (8-bit color).
    /// </summary>
    bool Supports256Colors { get; }

    /// <summary>
    /// Whether the terminal supports TrueColor (24-bit RGB).
    /// </summary>
    bool SupportsTrueColor { get; }
}
