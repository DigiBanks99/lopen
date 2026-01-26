using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Mock implementation of terminal capabilities for testing.
/// All properties are settable to simulate different terminal environments.
/// </summary>
public class MockTerminalCapabilities : ITerminalCapabilities
{
    /// <inheritdoc />
    public int Width { get; set; } = 80;

    /// <inheritdoc />
    public int Height { get; set; } = 24;

    /// <inheritdoc />
    public ColorSystem ColorSystem { get; set; } = ColorSystem.TrueColor;

    /// <inheritdoc />
    public bool SupportsUnicode { get; set; } = true;

    /// <inheritdoc />
    public bool IsInteractive { get; set; } = true;

    /// <inheritdoc />
    public bool IsNoColorSet { get; set; } = false;

    /// <inheritdoc />
    public bool SupportsColor => !IsNoColorSet && ColorSystem != ColorSystem.NoColors;

    /// <inheritdoc />
    public bool IsWideTerminal => Width >= 120;

    /// <inheritdoc />
    public bool IsNarrowTerminal => Width < 60;

    /// <summary>
    /// Creates a mock with default values (80x24, TrueColor, interactive).
    /// </summary>
    public MockTerminalCapabilities()
    {
    }

    /// <summary>
    /// Creates a mock with specified dimensions.
    /// </summary>
    public MockTerminalCapabilities(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Creates a mock simulating NO_COLOR mode.
    /// </summary>
    public static MockTerminalCapabilities NoColor() => new()
    {
        IsNoColorSet = true,
        ColorSystem = ColorSystem.NoColors
    };

    /// <summary>
    /// Creates a mock simulating a narrow terminal (&lt;60 chars).
    /// </summary>
    public static MockTerminalCapabilities Narrow() => new()
    {
        Width = 50
    };

    /// <summary>
    /// Creates a mock simulating a wide terminal (≥120 chars).
    /// </summary>
    public static MockTerminalCapabilities Wide() => new()
    {
        Width = 140
    };

    /// <summary>
    /// Creates a mock simulating a non-interactive (piped) terminal.
    /// </summary>
    public static MockTerminalCapabilities NonInteractive() => new()
    {
        IsInteractive = false
    };
}
