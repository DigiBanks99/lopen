using Shouldly;
using Spectre.Console;
using Xunit;

namespace Lopen.Core.Tests;

public class TerminalCapabilitiesTests
{
    [Fact]
    public void Detect_ReturnsValidWidth()
    {
        var caps = TerminalCapabilities.Detect();

        caps.Width.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Detect_ReturnsValidHeight()
    {
        var caps = TerminalCapabilities.Detect();

        caps.Height.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Detect_ReturnsColorSystem()
    {
        var caps = TerminalCapabilities.Detect();

        // Should be a valid enum value
        Enum.IsDefined(typeof(ColorSystem), caps.ColorSystem).ShouldBeTrue();
    }

    [Fact]
    public void SupportsColor_FalseWhenNoColorSet()
    {
        // Use mock to test the logic without environment variable manipulation
        var caps = MockTerminalCapabilities.NoColor();

        caps.SupportsColor.ShouldBeFalse();
    }

    [Fact]
    public void SupportsColor_FalseWhenColorSystemIsNoColors()
    {
        var caps = new MockTerminalCapabilities
        {
            ColorSystem = ColorSystem.NoColors,
            IsNoColorSet = false
        };

        caps.SupportsColor.ShouldBeFalse();
    }

    [Fact]
    public void SupportsColor_TrueWhenColorAvailable()
    {
        var caps = new MockTerminalCapabilities
        {
            ColorSystem = ColorSystem.TrueColor,
            IsNoColorSet = false
        };

        caps.SupportsColor.ShouldBeTrue();
    }

    [Fact]
    public void IsWideTerminal_TrueWhenWidthAtLeast120()
    {
        var caps = new MockTerminalCapabilities { Width = 120 };

        caps.IsWideTerminal.ShouldBeTrue();
    }

    [Fact]
    public void IsWideTerminal_FalseWhenWidthBelow120()
    {
        var caps = new MockTerminalCapabilities { Width = 119 };

        caps.IsWideTerminal.ShouldBeFalse();
    }

    [Fact]
    public void IsNarrowTerminal_TrueWhenWidthBelow60()
    {
        var caps = new MockTerminalCapabilities { Width = 59 };

        caps.IsNarrowTerminal.ShouldBeTrue();
    }

    [Fact]
    public void IsNarrowTerminal_FalseWhenWidthAtLeast60()
    {
        var caps = new MockTerminalCapabilities { Width = 60 };

        caps.IsNarrowTerminal.ShouldBeFalse();
    }
}
