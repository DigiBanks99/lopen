using Shouldly;
using Spectre.Console;
using Xunit;

namespace Lopen.Core.Tests;

public class ColorProviderTests
{
    [Fact]
    public void GetColor_WithNoColorSupport_ReturnsDefault()
    {
        var capabilities = MockTerminalCapabilities.NoColor();
        var provider = new ColorProvider(capabilities);

        var result = provider.GetColor(ColorCategory.Success);

        result.ShouldBe(Color.Default);
    }

    [Theory]
    [InlineData(ColorCategory.Success)]
    [InlineData(ColorCategory.Error)]
    [InlineData(ColorCategory.Warning)]
    [InlineData(ColorCategory.Info)]
    [InlineData(ColorCategory.Muted)]
    [InlineData(ColorCategory.Highlight)]
    [InlineData(ColorCategory.Accent)]
    public void GetColor_WithTrueColor_ReturnsColor(ColorCategory category)
    {
        var capabilities = new MockTerminalCapabilities { ColorSystem = ColorSystem.TrueColor };
        var provider = new ColorProvider(capabilities);

        var result = provider.GetColor(category);

        result.ShouldNotBe(Color.Default);
    }

    [Theory]
    [InlineData(ColorCategory.Success)]
    [InlineData(ColorCategory.Error)]
    [InlineData(ColorCategory.Warning)]
    [InlineData(ColorCategory.Info)]
    [InlineData(ColorCategory.Muted)]
    [InlineData(ColorCategory.Highlight)]
    [InlineData(ColorCategory.Accent)]
    public void GetColor_With256Color_ReturnsColor(ColorCategory category)
    {
        var capabilities = MockTerminalCapabilities.TwoFiftySixColor();
        var provider = new ColorProvider(capabilities);

        var result = provider.GetColor(category);

        result.ShouldNotBe(Color.Default);
    }

    [Theory]
    [InlineData(ColorCategory.Success)]
    [InlineData(ColorCategory.Error)]
    [InlineData(ColorCategory.Warning)]
    [InlineData(ColorCategory.Info)]
    [InlineData(ColorCategory.Muted)]
    [InlineData(ColorCategory.Highlight)]
    [InlineData(ColorCategory.Accent)]
    public void GetColor_With16Color_ReturnsColor(ColorCategory category)
    {
        var capabilities = MockTerminalCapabilities.SixteenColor();
        var provider = new ColorProvider(capabilities);

        var result = provider.GetColor(category);

        result.ShouldNotBe(Color.Default);
    }

    [Fact]
    public void GetColor_Success_WithTrueColor_ReturnsBrightGreen()
    {
        var capabilities = new MockTerminalCapabilities { ColorSystem = ColorSystem.TrueColor };
        var provider = new ColorProvider(capabilities);

        var result = provider.GetColor(ColorCategory.Success);

        // Bright green RGB
        result.R.ShouldBe((byte)0);
        result.G.ShouldBe((byte)255);
        result.B.ShouldBe((byte)0);
    }

    [Fact]
    public void GetColor_Info_WithTrueColor_ReturnsBrightBlue()
    {
        var capabilities = new MockTerminalCapabilities { ColorSystem = ColorSystem.TrueColor };
        var provider = new ColorProvider(capabilities);

        var result = provider.GetColor(ColorCategory.Info);

        // Bright blue RGB (0, 153, 255)
        result.R.ShouldBe((byte)0);
        result.G.ShouldBe((byte)153);
        result.B.ShouldBe((byte)255);
    }

    [Fact]
    public void Constructor_WithNullCapabilities_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ColorProvider(null!));
    }
}

public class TerminalCapabilitiesColorDepthTests
{
    [Theory]
    [InlineData(ColorSystem.TrueColor, true)]
    [InlineData(ColorSystem.EightBit, true)]
    [InlineData(ColorSystem.Standard, false)]
    [InlineData(ColorSystem.Legacy, false)]
    [InlineData(ColorSystem.NoColors, false)]
    public void Supports256Colors_ReturnsExpected(ColorSystem colorSystem, bool expected)
    {
        var capabilities = new MockTerminalCapabilities { ColorSystem = colorSystem };

        capabilities.Supports256Colors.ShouldBe(expected);
    }

    [Theory]
    [InlineData(ColorSystem.TrueColor, true)]
    [InlineData(ColorSystem.EightBit, false)]
    [InlineData(ColorSystem.Standard, false)]
    [InlineData(ColorSystem.Legacy, false)]
    [InlineData(ColorSystem.NoColors, false)]
    public void SupportsTrueColor_ReturnsExpected(ColorSystem colorSystem, bool expected)
    {
        var capabilities = new MockTerminalCapabilities { ColorSystem = colorSystem };

        capabilities.SupportsTrueColor.ShouldBe(expected);
    }

    [Fact]
    public void SixteenColor_Factory_Returns16ColorTerminal()
    {
        var capabilities = MockTerminalCapabilities.SixteenColor();

        capabilities.ColorSystem.ShouldBe(ColorSystem.Standard);
        capabilities.Supports256Colors.ShouldBeFalse();
        capabilities.SupportsTrueColor.ShouldBeFalse();
    }

    [Fact]
    public void TwoFiftySixColor_Factory_Returns256ColorTerminal()
    {
        var capabilities = MockTerminalCapabilities.TwoFiftySixColor();

        capabilities.ColorSystem.ShouldBe(ColorSystem.EightBit);
        capabilities.Supports256Colors.ShouldBeTrue();
        capabilities.SupportsTrueColor.ShouldBeFalse();
    }
}
