using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class SymbolProviderTests
{
    [Theory]
    [InlineData(StatusSymbol.Success, "✓")]
    [InlineData(StatusSymbol.Error, "✗")]
    [InlineData(StatusSymbol.Warning, "⚠")]
    [InlineData(StatusSymbol.Info, "ℹ")]
    [InlineData(StatusSymbol.Progress, "⏳")]
    [InlineData(StatusSymbol.New, "✨")]
    [InlineData(StatusSymbol.Launch, "🚀")]
    [InlineData(StatusSymbol.Fast, "⚡")]
    [InlineData(StatusSymbol.Tip, "💡")]
    public void GetSymbol_WithUnicodeSupport_ReturnsUnicodeSymbol(StatusSymbol symbol, string expected)
    {
        var provider = new SymbolProvider(supportsUnicode: true);
        
        var result = provider.GetSymbol(symbol);
        
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(StatusSymbol.Success, "✓")]
    [InlineData(StatusSymbol.Error, "✗")]
    [InlineData(StatusSymbol.Warning, "!")]
    [InlineData(StatusSymbol.Info, "[i]")]
    [InlineData(StatusSymbol.Progress, "...")]
    [InlineData(StatusSymbol.New, "*")]
    [InlineData(StatusSymbol.Launch, ">>")]
    [InlineData(StatusSymbol.Fast, "!")]
    [InlineData(StatusSymbol.Tip, "?")]
    public void GetSymbol_WithoutUnicodeSupport_ReturnsFallback(StatusSymbol symbol, string expected)
    {
        var provider = new SymbolProvider(supportsUnicode: false);
        
        var result = provider.GetSymbol(symbol);
        
        result.ShouldBe(expected);
    }

    [Fact]
    public void Constructor_WithTerminalCapabilities_UsesSupportsUnicode()
    {
        var capabilities = new MockTerminalCapabilities
        {
            SupportsUnicode = true
        };
        var provider = new SymbolProvider(capabilities);
        
        var result = provider.GetSymbol(StatusSymbol.Launch);
        
        result.ShouldBe("🚀");
    }

    [Fact]
    public void Constructor_WithNullCapabilities_DefaultsToNoUnicode()
    {
        var provider = new SymbolProvider((ITerminalCapabilities)null!);
        
        var result = provider.GetSymbol(StatusSymbol.Launch);
        
        result.ShouldBe(">>");
    }
}
