using Shouldly;
using Spectre.Console;
using Xunit;

namespace Lopen.Core.Tests;

public class MockTerminalCapabilitiesTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var caps = new MockTerminalCapabilities();

        caps.Width.ShouldBe(80);
        caps.Height.ShouldBe(24);
        caps.ColorSystem.ShouldBe(ColorSystem.TrueColor);
        caps.SupportsUnicode.ShouldBeTrue();
        caps.IsInteractive.ShouldBeTrue();
        caps.IsNoColorSet.ShouldBeFalse();
    }

    [Fact]
    public void DimensionConstructor_SetsWidthAndHeight()
    {
        var caps = new MockTerminalCapabilities(100, 40);

        caps.Width.ShouldBe(100);
        caps.Height.ShouldBe(40);
    }

    [Fact]
    public void NoColor_ReturnsNoColorConfiguration()
    {
        var caps = MockTerminalCapabilities.NoColor();

        caps.IsNoColorSet.ShouldBeTrue();
        caps.ColorSystem.ShouldBe(ColorSystem.NoColors);
        caps.SupportsColor.ShouldBeFalse();
    }

    [Fact]
    public void Narrow_ReturnsNarrowConfiguration()
    {
        var caps = MockTerminalCapabilities.Narrow();

        caps.Width.ShouldBe(50);
        caps.IsNarrowTerminal.ShouldBeTrue();
        caps.IsWideTerminal.ShouldBeFalse();
    }

    [Fact]
    public void Wide_ReturnsWideConfiguration()
    {
        var caps = MockTerminalCapabilities.Wide();

        caps.Width.ShouldBe(140);
        caps.IsWideTerminal.ShouldBeTrue();
        caps.IsNarrowTerminal.ShouldBeFalse();
    }

    [Fact]
    public void NonInteractive_ReturnsNonInteractiveConfiguration()
    {
        var caps = MockTerminalCapabilities.NonInteractive();

        caps.IsInteractive.ShouldBeFalse();
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var caps = new MockTerminalCapabilities
        {
            Width = 150,
            Height = 50,
            ColorSystem = ColorSystem.Standard,
            SupportsUnicode = false,
            IsInteractive = false,
            IsNoColorSet = true
        };

        caps.Width.ShouldBe(150);
        caps.Height.ShouldBe(50);
        caps.ColorSystem.ShouldBe(ColorSystem.Standard);
        caps.SupportsUnicode.ShouldBeFalse();
        caps.IsInteractive.ShouldBeFalse();
        caps.IsNoColorSet.ShouldBeTrue();
    }

    [Fact]
    public void HelperProperties_ComputeCorrectly()
    {
        var caps = new MockTerminalCapabilities { Width = 100 };

        // Width 100: not narrow (<60), not wide (>=120)
        caps.IsNarrowTerminal.ShouldBeFalse();
        caps.IsWideTerminal.ShouldBeFalse();
        caps.SupportsColor.ShouldBeTrue();
    }

    [Fact]
    public void SupportsColor_FalseWhenNoColorSetEvenWithTrueColor()
    {
        var caps = new MockTerminalCapabilities
        {
            ColorSystem = ColorSystem.TrueColor,
            IsNoColorSet = true
        };

        caps.SupportsColor.ShouldBeFalse();
    }
}
