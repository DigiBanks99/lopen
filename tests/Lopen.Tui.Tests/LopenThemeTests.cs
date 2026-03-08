using Spectre.Console;
using Lopen.Tui;

namespace Lopen.Tui.Tests;

public class LopenThemeTests
{
    // ── Colour Tests ────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(LopenTheme.Primary))]
    [InlineData(nameof(LopenTheme.Secondary))]
    [InlineData(nameof(LopenTheme.Accent))]
    [InlineData(nameof(LopenTheme.Success))]
    [InlineData(nameof(LopenTheme.Warning))]
    [InlineData(nameof(LopenTheme.Error))]
    [InlineData(nameof(LopenTheme.Muted))]
    [InlineData(nameof(LopenTheme.Text))]
    public void SemanticColour_FieldExists(string fieldName)
    {
        var field = typeof(LopenTheme).GetField(fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);
        Assert.Equal(typeof(Color), field!.FieldType);
    }

    [Theory]
    [InlineData(nameof(LopenTheme.Primary), "blue")]
    [InlineData(nameof(LopenTheme.Secondary), "purple")]
    [InlineData(nameof(LopenTheme.Accent), "aqua")]
    [InlineData(nameof(LopenTheme.Success), "lime")]
    [InlineData(nameof(LopenTheme.Warning), "yellow")]
    [InlineData(nameof(LopenTheme.Error), "red")]
    [InlineData(nameof(LopenTheme.Muted), "grey")]
    [InlineData(nameof(LopenTheme.Text), "white")]
    public void SemanticColour_MapsToExpectedAnsiColour(string fieldName, string expectedMarkup)
    {
        var field = typeof(LopenTheme).GetField(fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var color = (Color)field!.GetValue(null)!;
        Assert.Equal(expectedMarkup, color.ToMarkup());
    }

    [Fact]
    public void AllEightSemanticColoursAreDefined()
    {
        var colorFields = typeof(LopenTheme)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Color))
            .ToList();

        Assert.Equal(8, colorFields.Count);
    }

    [Fact]
    public void SemanticColours_MatchExpectedConstants()
    {
        Assert.Equal(Color.Blue, LopenTheme.Primary);
        Assert.Equal(Color.Purple, LopenTheme.Secondary);
        Assert.Equal(Color.Aqua, LopenTheme.Accent);
        Assert.Equal(Color.Lime, LopenTheme.Success);
        Assert.Equal(Color.Yellow, LopenTheme.Warning);
        Assert.Equal(Color.Red, LopenTheme.Error);
        Assert.Equal(Color.Grey, LopenTheme.Muted);
        Assert.Equal(Color.White, LopenTheme.Text);
    }

    // ── Style Tests ──────────────────────────────────────────────────────

    [Fact]
    public void PrimaryBold_HasBoldDecoration()
    {
        Assert.Equal(Decoration.Bold, LopenTheme.PrimaryBold.Decoration);
    }

    [Fact]
    public void PrimaryBold_UsesPrimaryForeground()
    {
        Assert.Equal(LopenTheme.Primary, LopenTheme.PrimaryBold.Foreground);
    }

    [Fact]
    public void MutedDim_HasDimDecoration()
    {
        Assert.Equal(Decoration.Dim, LopenTheme.MutedDim.Decoration);
    }

    [Fact]
    public void MutedDim_UsesMutedForeground()
    {
        Assert.Equal(LopenTheme.Muted, LopenTheme.MutedDim.Foreground);
    }

    [Fact]
    public void ErrorBold_HasBoldDecoration()
    {
        Assert.Equal(Decoration.Bold, LopenTheme.ErrorBold.Decoration);
    }

    [Fact]
    public void ErrorBold_UsesErrorForeground()
    {
        Assert.Equal(LopenTheme.Error, LopenTheme.ErrorBold.Foreground);
    }

    [Fact]
    public void AccentStyle_UsesAccentForeground()
    {
        Assert.Equal(LopenTheme.Accent, LopenTheme.AccentStyle.Foreground);
    }

    [Fact]
    public void SuccessStyle_UsesSuccessForeground()
    {
        Assert.Equal(LopenTheme.Success, LopenTheme.SuccessStyle.Foreground);
    }

    [Fact]
    public void WarningStyle_UsesWarningForeground()
    {
        Assert.Equal(LopenTheme.Warning, LopenTheme.WarningStyle.Foreground);
    }

    [Fact]
    public void SecondaryStyle_UsesSecondaryForeground()
    {
        Assert.Equal(LopenTheme.Secondary, LopenTheme.SecondaryStyle.Foreground);
    }

    [Fact]
    public void TextStyle_UsesTextForeground()
    {
        Assert.Equal(LopenTheme.Text, LopenTheme.TextStyle.Foreground);
    }

    [Fact]
    public void AllEightStylesAreDefined()
    {
        var styleFields = typeof(LopenTheme)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Style))
            .ToList();

        Assert.Equal(8, styleFields.Count);
    }

    // ── Glyph Tests ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(LopenTheme.PromptChar), "❯")]
    [InlineData(nameof(LopenTheme.SectionMarker), "◆")]
    [InlineData(nameof(LopenTheme.PhaseComplete), "✓")]
    [InlineData(nameof(LopenTheme.PhaseActive), "●")]
    [InlineData(nameof(LopenTheme.PhasePending), "○")]
    [InlineData(nameof(LopenTheme.Bullet), "•")]
    [InlineData(nameof(LopenTheme.ToolSuccess), "✔")]
    [InlineData(nameof(LopenTheme.ToolFailure), "✘")]
    [InlineData(nameof(LopenTheme.PauseIndicator), "⏸")]
    [InlineData(nameof(LopenTheme.InfoHint), "ℹ")]
    public void Glyph_HasExpectedUnicodeCharacter(string fieldName, string expectedChar)
    {
        var field = typeof(LopenTheme).GetField(fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);
        var value = (string)field!.GetValue(null)!;
        Assert.Equal(expectedChar, value);
    }

    [Fact]
    public void AllTenGlyphsAreDefined()
    {
        var glyphFields = typeof(LopenTheme)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
            .ToList();

        Assert.Equal(10, glyphFields.Count);
    }

    // ── Markup Helper Tests ─────────────────────────────────────────────

    [Fact]
    public void Styled_EscapesSquareBrackets()
    {
        var result = LopenTheme.Styled("test [bold] text", LopenTheme.Primary);
        Assert.Contains("[[bold]]", result);
        // Ensure original unescaped [bold] is not present
        Assert.DoesNotContain("[bold]", result.Replace("[[bold]]", ""));
    }

    [Fact]
    public void Styled_ProducesValidMarkup()
    {
        var result = LopenTheme.Styled("hello", LopenTheme.Accent);
        Assert.StartsWith("[", result);
        Assert.EndsWith("[/]", result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public void Styled_IncludesColourMarkup()
    {
        var result = LopenTheme.Styled("hello", LopenTheme.Accent);
        Assert.Contains("aqua", result);
    }

    [Fact]
    public void Styled_CanBeParsedBySpectre()
    {
        var result = LopenTheme.Styled("hello world", LopenTheme.Primary);
        // Should not throw - validates markup is well-formed
        var markup = new Markup(result);
        Assert.NotNull(markup);
    }

    [Fact]
    public void Bold_AppliesBoldDecoration()
    {
        var result = LopenTheme.Bold("important", LopenTheme.Error);
        Assert.Contains("bold", result);
        Assert.Contains("important", result);
    }

    [Fact]
    public void Bold_EscapesSpecialCharacters()
    {
        var result = LopenTheme.Bold("[square]", LopenTheme.Primary);
        Assert.Contains("[[square]]", result);
    }

    [Fact]
    public void Bold_CanBeParsedBySpectre()
    {
        var result = LopenTheme.Bold("test [markup] text", LopenTheme.Error);
        var markup = new Markup(result);
        Assert.NotNull(markup);
    }

    [Fact]
    public void Dim_AppliesDimDecoration()
    {
        var result = LopenTheme.Dim("secondary", LopenTheme.Muted);
        Assert.Contains("dim", result);
        Assert.Contains("secondary", result);
    }

    [Fact]
    public void Dim_CanBeParsedBySpectre()
    {
        var result = LopenTheme.Dim("test [markup] text", LopenTheme.Muted);
        var markup = new Markup(result);
        Assert.NotNull(markup);
    }
}
