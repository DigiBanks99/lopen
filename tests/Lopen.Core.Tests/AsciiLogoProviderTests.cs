using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class AsciiLogoProviderTests
{
    private readonly AsciiLogoProvider _provider = new();

    [Theory]
    [InlineData(120, "Wind Runner")]
    [InlineData(100, "Wind Runner")]
    [InlineData(80, "Wind Runner")]
    public void GetLogo_WideTerminal_ReturnsFullLogo(int width, string expectedContent)
    {
        var logo = _provider.GetLogo(width);

        logo.ShouldContain(expectedContent);
    }

    [Theory]
    [InlineData(79)]
    [InlineData(60)]
    [InlineData(50)]
    public void GetLogo_MediumTerminal_ReturnsCompactLogo(int width)
    {
        var logo = _provider.GetLogo(width);

        logo.ShouldBe("⚡ lopen ⚡");
    }

    [Theory]
    [InlineData(49)]
    [InlineData(40)]
    [InlineData(20)]
    public void GetLogo_NarrowTerminal_ReturnsMinimalLogo(int width)
    {
        var logo = _provider.GetLogo(width);

        logo.ShouldBe("lopen");
    }

    [Fact]
    public void GetFullLogo_ContainsWindRunnerText()
    {
        var logo = AsciiLogoProvider.GetFullLogo();

        logo.ShouldContain("Wind Runner");
        logo.ShouldContain("⚡");
    }

    [Fact]
    public void GetTagline_ReturnsExpectedText()
    {
        AsciiLogoProvider.GetTagline().ShouldBe("Interactive Copilot Agent Loop");
    }

    [Fact]
    public void GetHelpTip_ReturnsExpectedText()
    {
        AsciiLogoProvider.GetHelpTip().ShouldContain("help");
    }
}
